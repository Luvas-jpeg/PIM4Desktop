using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PIM4AdminDesktop;
using PIM4AdminDesktop.Models;
using PIM4AdminDesktop.Services;
using PIM4AdminDesktop.ViewModels;
using PIM4AdminDesktop.Views;

internal sealed class ScreenshotCapture(string directory)
{
    private readonly List<CaptureRecord> _captures = [];

    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        await new ScreenshotCapture(Path.GetFullPath(directory)).CaptureAsync();
    }

    private async Task CaptureAsync()
    {
        using var api = new ApiClient(new Uri("https://api.example.test/"), new DemoHandler());
        var window = new MainWindow(api) { Width = 1280, Height = 860 };
        var vm = (MainViewModel)window.DataContext;
        window.Show();
        try
        {
            await SettleAsync(window);
            ((TextBox)window.FindName("EmailTextBox")).Text = "admin.demonstracao@example.com";
            Save(window, "01-login.png", "Login administrativo", "Tela real de login; credenciais e dados fictícios.");
            if (!await vm.LoginAsync("admin.demonstracao@example.com", "demonstracao-local"))
                throw new InvalidOperationException(vm.ErrorMessage);
            await SelectTabAsync(window, "Cursos");
            Save(window, "02-painel-cursos.png", "Painel e cursos", "Indicadores, busca e ações administrativas com respostas HTTP em memória.");
            vm.SelectedCourse = vm.Courses.Single(c => c.Id == 5);
            await WaitForAsync(() => !vm.IsBusy && vm.Classes.Count == 3);
            var actions = new AdminActions(window, vm);
            await CaptureDialogAsync(window, () => actions.EditCourseAsync(false), "03-editar-curso.png", "Edição de curso");
            await SelectTabAsync(window, "Turmas");
            vm.SelectedClass = vm.Classes.Single(c => c.Id == 9);
            Save(window, "04-lista-turmas.png", "Turmas do curso", "Inclui capacidade, vagas e situação; dados de demonstração.");
            await CaptureDialogAsync(window, () => actions.EditClassAsync(false), "05-editar-turma.png", "Edição de turma");

            var roster = new ClassStudentsWindow(window, vm);
            roster.Show();
            try
            {
                await WaitForAsync(() => !vm.IsBusy && vm.ClassStudents.Count == 2);
                await SettleAsync(roster);
                vm.SelectedEnrollment = vm.ClassStudents[0];
                Save(roster, "06-alunos-turma.png", "Matrículas da turma", "Alunos fictícios, ID da matrícula, situação e participação.");
                var enrollmentActions = new AdminActions(roster, vm);
                await CaptureDialogAsync(roster, () => enrollmentActions.EditEnrollmentAsync(false), "07-situacao-matricula.png", "Situação da matrícula");
                await CaptureDialogAsync(roster, () => enrollmentActions.EditEnrollmentAsync(true), "08-transferir-matricula.png", "Transferência de matrícula", dialog =>
                {
                    var destination = Descendants<ComboBox>(dialog).Single();
                    destination.SelectedValue = "10";
                    return Task.CompletedTask;
                });
            }
            finally { roster.Close(); }

            await SelectTabAsync(window, "Pedidos");
            vm.SelectedOrder = vm.Orders.Single(o => o.Id == 7);
            Save(window, "09-lista-pedidos.png", "Pedidos", "Histórico fictício com pedidos pago e pendente.");
            await CaptureDialogAsync(window, () => actions.EditOrderAsync(), "10-detalhes-pedido.png", "Detalhes e situação do pedido");
            vm.SelectedOrder = vm.Orders.Single(o => o.Id == 8);
            await CaptureDialogAsync(window, () => actions.EditOrderAsync(), "11-erro-pedido-nao-pago.png", "Mensagem de regra de negócio", async dialog =>
            {
                Descendants<ComboBox>(dialog).Single().SelectedValue = "processing";
                Descendants<Button>(dialog).Single(b => Equals(b.Content, "Salvar"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForAsync(() => !vm.IsBusy && vm.ErrorMessage.Contains("Somente pedidos pagos"));
            });
            await SelectTabAsync(window, "Alunos");
            vm.SelectedStudent = vm.Students[0];
            Save(window, "12-lista-alunos.png", "Cadastros de alunos", "Consulta e ações de cadastro com dados fictícios.");
            await CaptureDialogAsync(window, () => actions.EditStudentAsync(false), "13-editar-aluno.png", "Edição do cadastro de aluno");
            WriteManifest();
            Console.WriteLine($"PASS {_captures.Count} capturas WPF gravadas em {directory}");
        }
        finally { window.Close(); }
    }

    private async Task CaptureDialogAsync(Window owner, Func<Task> open, string file, string title, Func<EditorWindow, Task>? prepare = null)
    {
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            EditorWindow? dialog = null;
            try
            {
                dialog = Application.Current.Windows.OfType<EditorWindow>().Single(w => w.Owner == owner);
                await SettleAsync(dialog);
                if (prepare != null) await prepare(dialog);
                await SettleAsync(dialog);
                Save(dialog, file, title, "Formulário real do aplicativo WPF com HTTP substituído por respostas de demonstração; não comprova gravação na API real.");
                var scroll = Descendants<ScrollViewer>(dialog).FirstOrDefault(s => s.ScrollableHeight > 1);
                if (scroll != null)
                {
                    scroll.ScrollToEnd();
                    await SettleAsync(dialog);
                    Save(dialog, Path.GetFileNameWithoutExtension(file) + "-continuacao.png", title + " — continuação", "Parte inferior do mesmo formulário, após rolagem.");
                }
                captured.SetResult();
            }
            catch (Exception error) { captured.TrySetException(error); }
            finally { dialog?.Close(); }
        }));
        await open();
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static async Task SelectTabAsync(Window window, string title)
    {
        var tabs = Descendants<TabControl>(window).Single();
        tabs.SelectedItem = tabs.Items.Cast<TabItem>().Single(t => Equals(t.Header, title));
        await SettleAsync(window);
    }
    private static async Task SettleAsync(Window window)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.Render);
    }
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("A tela não chegou ao estado esperado para a captura.");
            await Task.Delay(25);
        }
    }
    private void Save(Window window, string file, string title, string description)
    {
        var content = (FrameworkElement)window.Content;
        content.UpdateLayout();
        if (content.ActualWidth <= 0 || content.ActualHeight <= 0)
            throw new InvalidOperationException("A janela não foi renderizada.");
        const double scale = 1.5;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * scale),
            (int)Math.Ceiling(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(directory, file);
        using (var output = File.Create(path)) encoder.Save(output);
        _captures.Add(new(file, title, description, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
    }
    private void WriteManifest()
    {
        var commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? Environment.GetEnvironmentVariable("PIM4_CAPTURE_COMMIT") ?? "não informado";
        var manifest = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            commit,
            platform = RuntimeInformation.OSDescription,
            culture = CultureInfo.CurrentCulture.Name,
            timeZone = TimeZoneInfo.Local.Id,
            renderer = "WPF RenderTargetBitmap; janelas e formulários do aplicativo, sem moldura do sistema",
            dataSource = "HTTP substituto em memória; dados fictícios; sem acesso ao Render ou a banco de dados",
            scope = "Evidência visual da interface. Não comprova integração com API real, persistência, pagamento ou homologação de produção.",
            captures = _captures
        };
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        var lines = new List<string>
        {
            "# Capturas do MediShop Admin Desktop", "", $"Revisão: `{commit}`.", "",
            "Telas reais do WPF renderizadas no Windows, com respostas HTTP em memória e dados fictícios. Não são mockups HTML. As imagens não comprovam funcionamento da API real ou gravação em banco.", "",
            "A mensagem de pedido não pago é produzida por uma resposta HTTP 400 de teste, apresentada pelo fluxo real do formulário.", "",
            "As datas das turmas são calculadas na execução. manifest.json registra origem, horário, plataforma, fuso, revisão e SHA-256 dos PNGs.", "",
            "Legenda sugerida: Captura da interface WPF em Windows, com dados fictícios e cliente HTTP substituto, revisão identificada no manifesto. Validação integrada da API deve ser apresentada separadamente.", ""
        };
        foreach (var capture in _captures) lines.Add($"- [{capture.Title}]({capture.File}): {capture.Description}");
        File.WriteAllLines(Path.Combine(directory, "README.md"), lines, Encoding.UTF8);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private sealed record CaptureRecord(string File, string Title, string Description, string Sha256);

    private sealed class DemoHandler : HttpMessageHandler
    {
        private readonly DateTime _start = DateTime.UtcNow.Date.AddDays(14).AddHours(12);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put && path == "/api/Orders/8/status")
                return Task.FromResult(Json(new { message = "Somente pedidos pagos podem avançar de situação." }, HttpStatusCode.BadRequest));
            if (request.Method != HttpMethod.Get && path != "/api/Auth/login")
                throw new InvalidOperationException("O gerador de capturas não grava dados nem simula sucesso de persistência.");
            object body = path switch
            {
                "/api/Auth/login" => new { accessToken = "token-ficticio", refreshToken = "refresh-ficticio", user = new { id = 1, nome = "Administração — Demonstração", role = "Admin" } },
                "/api/courses" => Courses(),
                "/api/courses/5/classes" => Classes(),
                "/api/courses/6/classes" or "/api/courses/7/classes" => Array.Empty<CourseClassModel>(),
                "/api/Orders" => Orders(),
                "/api/Students" or "/api/courses/5/classes/9/students" => Students(),
                _ => throw new InvalidOperationException("Rota não prevista no cenário visual: " + path)
            };
            return Task.FromResult(Json(body));
        }
        private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK)
            => new(code) { Content = JsonContent.Create(value) };
        private CourseModel[] Courses() =>
        [
            new() { Id = 5, Nome = "Primeiros Socorros — Fundamentos", Description = "Capacitação presencial de demonstração para prevenção e primeiros cuidados.", Category = "Primeiros Socorros", Preco = 150m, WorkloadHours = 8, DeliveryMode = "presencial", IsActive = true, Classes = [.. Classes()] },
            new() { Id = 6, Nome = "Biossegurança em Saúde", Description = "Curso fictício para demonstrar a administração do catálogo.", Category = "Segurança em Saúde", Preco = 210m, WorkloadHours = 12, DeliveryMode = "presencial", IsActive = true },
            new() { Id = 7, Nome = "Cuidados e Segurança do Paciente", Category = "Segurança em Saúde", Preco = 240m, WorkloadHours = 16, DeliveryMode = "presencial", IsActive = false }
        ];
        private CourseClassModel[] Classes() =>
        [
            new() { Id = 9, CourseId = 5, StartDate = _start, EndDate = _start.AddHours(8), Local = "Sala de demonstração — São José do Rio Preto", Instructor = "Instrutora de demonstração", Capacity = 20, AvailableSeats = 18, Status = "scheduled" },
            new() { Id = 10, CourseId = 5, StartDate = _start.AddDays(14), EndDate = _start.AddDays(14).AddHours(8), Local = "Laboratório de demonstração", Instructor = "Instrutor de demonstração", Capacity = 15, AvailableSeats = 15, Status = "scheduled" },
            new() { Id = 11, CourseId = 5, StartDate = _start.AddDays(21), Local = "Sala de demonstração", Instructor = "Instrutora de demonstração", Capacity = 20, AvailableSeats = 20, Status = "cancelled" }
        ];
        private static StudentModel[] Students() =>
        [
            new() { Id = 3, EnrollmentId = 42, Name = "Ana — Aluna Demonstração", Email = "ana.demonstracao@example.com", Phone = "(11) 90000-0000", CourseId = "5", CourseName = "Primeiros Socorros — Fundamentos", EnrollmentDate = DateTime.Today.ToString("yyyy-MM-dd"), Status = "active", Participation = new() { Status = "confirmed", ConfirmedAt = DateTime.UtcNow } },
            new() { Id = 4, EnrollmentId = 43, Name = "João — Aluno Demonstração", Email = "joao.demonstracao@example.com", CourseId = "5", CourseName = "Primeiros Socorros — Fundamentos", EnrollmentDate = DateTime.Today.ToString("yyyy-MM-dd"), Status = "active", Participation = new() { Status = "waiting" } }
        ];
        private static OrderModel[] Orders() =>
        [
            new() { Id = 7, DataPedido = DateTime.UtcNow.AddDays(-1), Status = "processing", PaymentStatus = "paid", Total = 150m, PaymentMethod = "pix", Usuario = new() { Nome = "Ana — Aluna Demonstração", Email = "ana.demonstracao@example.com" }, Itens = [new() { Nome = "Primeiros Socorros — Fundamentos", Quantidade = 1, PrecoUnitario = 150m, TurmaId = 9 }] },
            new() { Id = 8, DataPedido = DateTime.UtcNow, Status = "pending", PaymentStatus = "pending", Total = 210m, PaymentMethod = "pix", Usuario = new() { Nome = "João — Aluno Demonstração", Email = "joao.demonstracao@example.com" }, Itens = [new() { Nome = "Biossegurança em Saúde", Quantidade = 1, PrecoUnitario = 210m }] }
        ];
    }
}
