using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PIM4AdminDesktop.Configuration;
using PIM4AdminDesktop.Models;
using PIM4AdminDesktop.Services;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Renovação única em três consultas concorrentes", ConcurrentRefresh),
    ("Logout limpa sessão mesmo sem conexão", LogoutOffline),
    ("Logout durante refresh não restaura sessão", LogoutDuringRefresh),
    ("Sessão rejeitada é limpa e exige novo login", RejectedRefresh),
    ("Usuário sem role Admin não acessa o painel", NonAdmin),
    ("Regra de negócio da API chega ao usuário sem perder a sessão", BusinessError),
    ("Validações HTTP 400 e respostas HTML têm mensagens úteis", ValidationErrors),
    ("Rotas, métodos e payloads administrativos", MutationContracts),
    ("Reenvio após 401 preserva o corpo da alteração", RetryMutation),
    ("Configuração, dinheiro, e-mail e datas", InputValidation)
};
foreach (var (name, run) in tests) { await run(); Console.WriteLine($"PASS {name}"); }
Console.WriteLine($"{tests.Length} testes aprovados.");

static HttpResponseMessage Json(object data, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = JsonContent.Create(data) };
static object Session(string token = "old", string role = "Admin") => new { accessToken = token, refreshToken = "refresh-" + token, user = new { id = 1, nome = "Admin", role } };
static ApiClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) => new(new Uri("https://api.example.test/"), new FakeHandler(handle));
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task<T> Throws<T>(Func<Task> operation) where T : Exception
{
    try { await operation(); } catch (T exception) { return exception; }
    throw new Exception($"Esperava {typeof(T).Name}.");
}
static async Task ConcurrentRefresh()
{
    var expired = 0; var renewals = 0;
    var allExpired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var api = Client(async request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("login")) return Json(Session());
        if (request.RequestUri.AbsolutePath.EndsWith("refresh")) { Interlocked.Increment(ref renewals); return Json(Session("new")); }
        if (request.Headers.Authorization?.Parameter == "old")
        {
            if (Interlocked.Increment(ref expired) == 3) allExpired.SetResult();
            await allExpired.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return Json(new { message = "expired" }, HttpStatusCode.Unauthorized);
        }
        Check(request.Headers.Authorization?.Parameter == "new", "Token renovado ausente.");
        return Json(Array.Empty<object>());
    });
    await api.LoginAsync("admin@example.test", "password");
    await Task.WhenAll(api.GetCoursesAsync(), api.GetOrdersAsync(), api.GetStudentsAsync());
    Check(renewals == 1, $"Refresh duplicado: {renewals}.");
}
static async Task LogoutOffline()
{
    using var api = Client(request => request.RequestUri!.AbsolutePath.EndsWith("login")
        ? Task.FromResult(Json(Session())) : throw new HttpRequestException("offline"));
    await api.LoginAsync("a", "b");
    await Throws<HttpRequestException>(() => api.LogoutAsync());
    await Throws<UnauthorizedAccessException>(() => api.GetCoursesAsync());
}
static async Task LogoutDuringRefresh()
{
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var api = Client(async request =>
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("login")) return Json(Session());
        if (path.EndsWith("revoke")) return new(HttpStatusCode.NoContent);
        if (path.EndsWith("refresh")) { entered.SetResult(); await release.Task; return Json(Session("new")); }
        return Json(new { message = "expired" }, HttpStatusCode.Unauthorized);
    });
    await api.LoginAsync("a", "b");
    var pending = api.GetCoursesAsync();
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await api.LogoutAsync(); release.SetResult();
    await Throws<UnauthorizedAccessException>(() => pending);
    await Throws<UnauthorizedAccessException>(() => api.GetOrdersAsync());
}
static async Task RejectedRefresh()
{
    var calls = 0;
    using var api = Client(request =>
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Json(Session())
            : Json(new { message = "expired" }, request.RequestUri.AbsolutePath.EndsWith("refresh") ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized));
    });
    await api.LoginAsync("a", "b");
    await Throws<UnauthorizedAccessException>(() => api.GetCoursesAsync());
    var before = calls;
    await Throws<UnauthorizedAccessException>(() => api.GetOrdersAsync());
    Check(calls == before, "Continuou usando a sessão inválida.");
}
static async Task NonAdmin()
{
    using var api = Client(_ => Task.FromResult(Json(Session(role: "User"))));
    await Throws<UnauthorizedAccessException>(() => api.LoginAsync("a", "b"));
    await Throws<UnauthorizedAccessException>(() => api.GetOrdersAsync());
}
static async Task BusinessError()
{
    using var api = Client(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Json(Session())
        : request.Method == HttpMethod.Put ? Json(new { message = "Somente pedidos pagos podem avançar de situação." }, HttpStatusCode.BadRequest)
        : request.RequestUri.AbsolutePath.Contains("Students") ? Json(new { message = "Sem permissão." }, HttpStatusCode.Forbidden) : Json(Array.Empty<object>())));
    await api.LoginAsync("a", "b");
    var error = await Throws<ApiException>(() => api.UpdateOrderStatusAsync(12, "completed"));
    Check(error.Message.Contains("Somente pedidos pagos"), "Perdeu a mensagem de negócio.");
    await Throws<ApiException>(() => api.GetStudentsAsync());
    await api.GetOrdersAsync();
}
static async Task ValidationErrors()
{
    using var api = Client(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Json(Session())
        : request.Method == HttpMethod.Put ? Json(new { errors = new { Nome = new[] { "Nome obrigatório." }, Capacity = new[] { "Capacidade inválida." } } }, HttpStatusCode.BadRequest)
        : new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>indisponível</html>") }));
    await api.LoginAsync("a", "b");
    var error = await Throws<ApiException>(() => api.SaveCourseAsync(1, new()));
    Check(error.Message.Contains("Nome obrigatório") && error.Message.Contains("Capacidade inválida"), "Não exibiu os erros dos campos.");
    error = await Throws<ApiException>(() => api.GetOrdersAsync());
    Check(error.Message.Contains("502"), "Resposta HTML não usa fallback.");
}
static async Task MutationContracts()
{
    var sent = new List<(string Method, string Path, string Body)>();
    using var api = Client(async request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("login")) return Json(Session());
        Check(request.Headers.Authorization?.Parameter == "old" || request.RequestUri.AbsolutePath.EndsWith("revoke"), "JWT ausente.");
        sent.Add((request.Method.Method, request.RequestUri.PathAndQuery, request.Content == null ? "" : await request.Content.ReadAsStringAsync()));
        return request.Method == HttpMethod.Get ? Json(Array.Empty<object>()) : new(HttpStatusCode.NoContent);
    });
    await api.LoginAsync("a", "b");
    await api.SaveCourseAsync(null, new() { Nome = "Curso", Preco = 199.90m });
    await api.SaveCourseAsync(5, new() { Nome = "Curso editado" });
    await api.SetCourseArchivedAsync(5, true); await api.SetCourseArchivedAsync(5, false);
    await api.SaveClassAsync(5, null, new() { Capacity = 20, StartDate = new DateTime(2030, 1, 2, 12, 0, 0, DateTimeKind.Utc) });
    await api.SaveClassAsync(5, 9, new() { Capacity = 25 });
    await api.SaveStudentAsync(null, new() { Name = "Aluno", CourseId = "5" });
    await api.SaveStudentAsync(3, new() { Name = "Editado", CourseId = "5" });
    await api.DeleteStudentAsync(3); await api.UpdateOrderStatusAsync(7, "processing");
    var student = new StudentModel { Id = 3, EnrollmentId = 42 };
    await api.UpdateEnrollmentStatusAsync(5, 9, student, "completed");
    await api.TransferEnrollmentAsync(5, 9, student, 11);
    await api.GetClassesAsync(5); await api.GetClassStudentsAsync(5, 9); await api.LogoutAsync();
    var expected = new[] { "POST /api/courses", "PUT /api/courses/5", "POST /api/courses/5/archive", "POST /api/courses/5/restore",
        "POST /api/courses/5/classes", "PUT /api/courses/5/classes/9", "POST /api/Students", "PUT /api/Students/3", "DELETE /api/Students/3",
        "PUT /api/Orders/7/status", "PATCH /api/courses/5/classes/9/students/3/status", "POST /api/courses/5/classes/9/students/3/transfer",
        "GET /api/courses/5/classes?includeInactive=true", "GET /api/courses/5/classes/9/students", "POST /api/Auth/revoke" };
    Check(sent.Select(s => s.Method + " " + s.Path).SequenceEqual(expected), "Contrato de rota ou método incorreto.");
    using var course = JsonDocument.Parse(sent[0].Body);
    Check(course.RootElement.GetProperty("preco").GetDecimal() == 199.90m, "Preço serializado incorretamente.");
    using var transfer = JsonDocument.Parse(sent[11].Body);
    Check(transfer.RootElement.GetProperty("enrollmentId").GetInt32() == 42 && transfer.RootElement.GetProperty("targetClassId").GetInt32() == 11, "Transferiu sem identificar matrícula e destino.");
    Check(sent[14].Body.Contains("refresh-old"), "Logout não revoga o refresh token.");
}
static async Task RetryMutation()
{
    var bodies = new List<string>();
    using var api = Client(async request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("login")) return Json(Session());
        if (request.RequestUri.AbsolutePath.EndsWith("refresh")) return Json(Session("new"));
        bodies.Add(await request.Content!.ReadAsStringAsync());
        return new(request.Headers.Authorization?.Parameter == "old" ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent);
    });
    await api.LoginAsync("a", "b"); await api.UpdateOrderStatusAsync(1, "processing");
    Check(bodies.Count == 2 && bodies[0] == bodies[1] && bodies[1].Contains("processing"), "Reenvio perdeu o corpo.");
}
static async Task InputValidation()
{
    Check(AppConfiguration.ValidateBaseUri("https://example.test").AbsoluteUri == "https://example.test/", "URL raiz inválida.");
    foreach (var url in new[] { "https://host.test/api", "https://user:pass@host.test/", "ftp://host.test/", "http://host.test/", "https://host.test/?token=x" })
        await Throws<InvalidOperationException>(() => Task.FromResult(AppConfiguration.ValidateBaseUri(url)));
    Check(AdminValidation.Money("1.234,56") == 1234.56m, "Preço pt-BR incorreto.");
    await Throws<ArgumentException>(() => Task.FromResult(AdminValidation.Money("-1")));
    await Throws<ArgumentException>(() => Task.FromResult(AdminValidation.Money("199.90")));
    await Throws<ArgumentException>(() => Task.FromResult(AdminValidation.Email("Aluno <a@example.com>")));
    await Throws<ArgumentException>(() => Task.FromResult(AdminValidation.Integer("0", "Capacidade", 1)));
    await Throws<ArgumentException>(() => Task.FromResult(AdminValidation.LocalDateTime("31/02/2030 09:00", "Início")));
    Check(AdminValidation.LocalDateTime("20/12/2030 09:00", "Início").Kind == DateTimeKind.Utc, "Data enviada sem UTC.");
}

sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
}
