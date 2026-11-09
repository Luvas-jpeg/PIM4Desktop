using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Threading;
using PIM4AdminDesktop;
using PIM4AdminDesktop.Services;
using PIM4AdminDesktop.ViewModels;
using PIM4AdminDesktop.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--screenshots"))
        {
            Console.Error.WriteLine("Uso: DesktopUiChecks [--screenshots DIRETORIO]");
            return 2;
        }
        var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        var exitCode = 0;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await RunAsync();
                if (args.Length == 2) await ScreenshotCapture.RunAsync(args[1]);
            }
            catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; }
            finally { app.Shutdown(); }
        }));
        app.Run();
        return exitCode;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task RunAsync()
    {
        var bindingErrors = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        var offline = false;
        using var api = new ApiClient(new Uri("https://api.example.test/"), new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("revoke") && offline) throw new HttpRequestException("offline");
            object body = path switch
            {
                "/api/Auth/login" => new { accessToken = "access", refreshToken = "refresh", user = new { nome = "Admin", role = "Admin" } },
                "/api/courses" => new[] { new { id = 5, nome = "Curso", preco = 120m, isActive = true,
                    classes = new[] { new { id = 9, courseId = 5, startDate = DateTime.UtcNow.AddDays(20), capacity = 10, availableSeats = 9, status = "scheduled" } } } },
                "/api/courses/5/classes" => new[] { new { id = 9, courseId = 5, startDate = DateTime.UtcNow.AddDays(20), capacity = 10, availableSeats = 9, status = "scheduled" } },
                "/api/Orders" => new[] { new { id = 7, total = 120m, paymentStatus = "paid", status = "processing" } },
                "/api/Students" => new[] { new { id = 3, name = "Ana", email = "ana@example.test", status = "active", courseId = "5" } },
                "/api/courses/5/classes/9/students" => new[] { new { id = 3, enrollmentId = 42, name = "Ana", status = "active", participation = new { status = "confirmed" } } },
                _ => new { message = "Somente pedidos pagos podem avançar de situação." }
            };
            return Task.FromResult(new HttpResponseMessage(path.Contains("/status") ? HttpStatusCode.BadRequest : HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }));
        var window = new MainWindow(api);
        var vm = (MainViewModel)window.DataContext;
        Check(await vm.LoginAsync("admin@example.test", "password"), vm.ErrorMessage);
        window.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)window.FindName("AdminView")).Visibility == Visibility.Visible, "Painel oculto após login.");
        Check(((FrameworkElement)window.FindName("LoginView")).Visibility == Visibility.Collapsed, "Login permanece visível.");
        Check(vm.ActiveCourses == 1 && vm.ActiveStudents == 1 && vm.PaidRevenue == 120m, "Indicadores incorretos.");
        vm.SearchText = "inexistente";
        Check(vm.CoursesView.IsEmpty && vm.StudentsView.IsEmpty && vm.OrdersView.IsEmpty, "Busca não filtra as listas.");
        vm.SearchText = "";
        vm.SelectedCourse = vm.Courses[0];
        if (vm.Classes.Count == 0) Check(await vm.LoadClassesAsync(), vm.ErrorMessage);
        vm.SelectedClass = vm.Classes[0];
        Check(await vm.LoadClassStudentsAsync() && vm.ClassStudents[0].EnrollmentId == 42, "Matrícula não identificada.");
        var roster = new ClassStudentsWindow(window, vm); roster.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(!await vm.RunAsync(() => api.UpdateOrderStatusAsync(7, "completed")), "Esperava rejeição da API.");
        Check(vm.ErrorMessage.Contains("Somente pedidos pagos") && vm.IsAuthenticated, "Erro de negócio encerrou sessão ou perdeu mensagem.");
        var editor = new EditorWindow(window, "Teste de formulário");
        editor.Text("Nome", "Curso"); editor.Choice("Situação", AdminLabels.Choices("active"), "active"); editor.Check("Ativo", true);
        editor.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        editor.Close(); roster.Close();
        offline = true; await vm.LogoutAsync();
        Check(!vm.IsAuthenticated && vm.Courses.Count == 0 && vm.ClassStudents.Count == 0 && vm.Orders.Count == 0, "Logout offline manteve sessão ou dados.");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(((FrameworkElement)window.FindName("AdminView")).Visibility == Visibility.Collapsed, "Painel visível após logout.");
        Check(bindingErrors.Errors.Count == 0, string.Join(Environment.NewLine, bindingErrors.Errors));
        window.Close();
        Console.WriteLine("PASS WPF: login, visibilidade, indicadores, busca, matrículas, mensagens, formulários e logout offline.");
    }
}
internal sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
}
internal sealed class BindingErrors : TraceListener
{
    public List<string> Errors { get; } = [];
    public override void Write(string? message) { if (message != null) Errors.Add(message); }
    public override void WriteLine(string? message) { if (message != null) Errors.Add(message); }
}
