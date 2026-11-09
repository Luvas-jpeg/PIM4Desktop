using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PIM4AdminDesktop.Models;

namespace PIM4AdminDesktop.Services;

public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AuthResponse? _session;
    private int _sessionVersion;

    public ApiClient(Uri baseUri, HttpMessageHandler? handler = null)
    {
        _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        _httpClient.BaseAddress = baseUri;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public Uri BaseUri => _httpClient.BaseAddress!;

    public async Task<UserModel> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        ClearSession();
        var version = _sessionVersion;
        using var response = await _httpClient.PostAsJsonAsync("api/Auth/login",
            new LoginRequest { Email = email.Trim(), Senha = password }, JsonOptions, ct);
        var session = await ReadResponseAsync<AuthResponse>(response, ct);
        ValidateSession(session);
        if (version != _sessionVersion) throw SessionExpired();
        _session = session;
        return session.User;
    }

    public Task<List<CourseModel>> GetCoursesAsync(CancellationToken ct = default)
        => GetAsync<List<CourseModel>>("api/courses", ct);
    public Task<List<OrderModel>> GetOrdersAsync(CancellationToken ct = default)
        => GetAsync<List<OrderModel>>("api/Orders", ct);
    public Task<List<StudentModel>> GetStudentsAsync(CancellationToken ct = default)
        => GetAsync<List<StudentModel>>("api/Students", ct);
    public Task<List<CourseClassModel>> GetClassesAsync(int courseId, CancellationToken ct = default)
        => GetAsync<List<CourseClassModel>>($"api/courses/{courseId}/classes?includeInactive=true", ct);
    public Task<List<StudentModel>> GetClassStudentsAsync(int courseId, int classId, CancellationToken ct = default)
        => GetAsync<List<StudentModel>>($"api/courses/{courseId}/classes/{classId}/students", ct);
    public Task SaveCourseAsync(int? id, CourseRequest data)
        => MutateAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/courses/{id}" : "api/courses", data);
    public Task SetCourseArchivedAsync(int id, bool archived)
        => MutateAsync(HttpMethod.Post, $"api/courses/{id}/{(archived ? "archive" : "restore")}");
    public Task SaveClassAsync(int courseId, int? id, CourseClassRequest data)
        => MutateAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            $"api/courses/{courseId}/classes" + (id.HasValue ? $"/{id}" : ""), data);
    public Task SaveStudentAsync(int? id, StudentRequest data)
        => MutateAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/Students/{id}" : "api/Students", data);
    public Task DeleteStudentAsync(int id) => MutateAsync(HttpMethod.Delete, $"api/Students/{id}");
    public Task UpdateOrderStatusAsync(int id, string status)
        => MutateAsync(HttpMethod.Put, $"api/Orders/{id}/status", new { status });
    public Task UpdateEnrollmentStatusAsync(int courseId, int classId, StudentModel student, string status)
        => MutateAsync(HttpMethod.Patch, $"api/courses/{courseId}/classes/{classId}/students/{student.Id}/status",
            new { status, student.EnrollmentId });
    public Task TransferEnrollmentAsync(int courseId, int classId, StudentModel student, int targetClassId)
        => MutateAsync(HttpMethod.Post, $"api/courses/{courseId}/classes/{classId}/students/{student.Id}/transfer",
            new { targetClassId, student.EnrollmentId });

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        // Clear before awaiting: a pending refresh must never restore a logged-out session.
        var session = _session;
        ClearSession();
        if (session == null) return;
        using var response = await _httpClient.PostAsJsonAsync("api/Auth/revoke",
            new RefreshRequest { RefreshToken = session.RefreshToken }, JsonOptions, ct);
        if (!response.IsSuccessStatusCode) await ThrowApiErrorAsync(response, ct);
    }

    public void ClearSession()
    {
        Interlocked.Increment(ref _sessionVersion);
        _session = null;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendAuthenticatedAsync(HttpMethod.Get, path, null, ct);
        return await ReadResponseAsync<T>(response, ct);
    }
    private async Task MutateAsync(HttpMethod method, string path, object? data = null)
    {
        using var response = await SendAuthenticatedAsync(method, path, data, CancellationToken.None);
        if (!response.IsSuccessStatusCode) await ThrowApiErrorAsync(response, CancellationToken.None);
    }
    private async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpMethod method, string path, object? data, CancellationToken ct)
    {
        var session = _session ?? throw SessionExpired();
        var version = _sessionVersion;
        var response = await SendAsync(method, path, data, session.AccessToken, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        response.Dispose();
        await _refreshLock.WaitAsync(ct);
        try
        {
            if (version != _sessionVersion || _session == null) throw SessionExpired();
            // Another request may already have rotated the token while we waited.
            if (ReferenceEquals(session, _session))
            {
                using var refreshResponse = await _httpClient.PostAsJsonAsync("api/Auth/refresh",
                    new RefreshRequest { RefreshToken = session.RefreshToken }, JsonOptions, ct);
                if (refreshResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                {
                    ClearSession();
                    throw SessionExpired();
                }
                var refreshed = await ReadResponseAsync<AuthResponse>(refreshResponse, ct);
                ValidateSession(refreshed);
                if (version != _sessionVersion) throw SessionExpired();
                _session = refreshed;
            }
            session = _session ?? throw SessionExpired();
        }
        finally { _refreshLock.Release(); }
        if (version != _sessionVersion) throw SessionExpired();
        var retry = await SendAsync(method, path, data, session.AccessToken, ct);
        if (retry.StatusCode == HttpStatusCode.Unauthorized)
        {
            retry.Dispose();
            ClearSession();
            throw SessionExpired();
        }
        return retry;
    }
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? data, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data != null) request.Content = JsonContent.Create(data, options: JsonOptions);
        return await _httpClient.SendAsync(request, ct);
    }
    private static void ValidateSession(AuthResponse session)
    {
        if (session.User == null) throw new InvalidOperationException("A API não informou o usuário da sessão.");
        if (!string.Equals(session.User.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Este aplicativo é exclusivo para administradores.");
        if (string.IsNullOrWhiteSpace(session.AccessToken) || string.IsNullOrWhiteSpace(session.RefreshToken))
            throw new InvalidOperationException("A API retornou uma sessão inválida.");
    }
    private static UnauthorizedAccessException SessionExpired() => new("A sessão expirou. Entre novamente.");
    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode) await ThrowApiErrorAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
            ?? throw new InvalidOperationException("A API retornou uma resposta vazia.");
    }
    private static async Task ThrowApiErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var message = response.StatusCode == HttpStatusCode.Forbidden
            ? "Você não tem permissão para executar esta ação."
            : $"A API retornou {(int)response.StatusCode}. Tente novamente.";
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            if (root.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
                message = value.GetString() ?? message;
            else if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var details = errors.EnumerateObject().SelectMany(field => field.Value.ValueKind == JsonValueKind.Array
                    ? field.Value.EnumerateArray().Select(item => item.GetString() ?? "") : []);
                var text = string.Join(Environment.NewLine, details.Where(item => !string.IsNullOrWhiteSpace(item)));
                if (text.Length > 0) message = text;
            }
            else if (root.TryGetProperty("title", out var title)) message = title.GetString() ?? message;
        }
        catch (JsonException) { /* A proxy may return HTML instead of JSON. */ }
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException(message);
        throw new ApiException(message, response.StatusCode);
    }
    public void Dispose() => _httpClient.Dispose();
}

public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
