using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;
using PIM4AdminDesktop.Models;
using PIM4AdminDesktop.Services;

namespace PIM4AdminDesktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public ApiClient Api { get; }
    private bool _isAuthenticated, _isBusy;
    private string _errorMessage = "", _successMessage = "", _administratorName = "", _searchText = "";
    private DateTime? _lastUpdatedAt;
    private CourseModel? _selectedCourse;
    private CourseClassModel? _selectedClass;
    private StudentModel? _selectedStudent, _selectedEnrollment;
    private OrderModel? _selectedOrder;

    public MainViewModel(ApiClient api)
    {
        Api = api;
        CoursesView = CollectionViewSource.GetDefaultView(Courses);
        StudentsView = CollectionViewSource.GetDefaultView(Students);
        OrdersView = CollectionViewSource.GetDefaultView(Orders);
        CoursesView.Filter = item => item is CourseModel c && Matches($"{c.Id} {c.Nome} {c.Category}");
        StudentsView.Filter = item => item is StudentModel s && Matches($"{s.Name} {s.Email} {s.CourseName}");
        OrdersView.Filter = item => item is OrderModel o && Matches($"{o.Id} {o.Usuario?.Nome} {o.Usuario?.Email} {o.Status} {o.PaymentStatus}");
    }

    public ObservableCollection<CourseModel> Courses { get; } = [];
    public ObservableCollection<OrderModel> Orders { get; } = [];
    public ObservableCollection<StudentModel> Students { get; } = [];
    public ObservableCollection<StudentModel> ClassStudents { get; } = [];
    public ICollectionView CoursesView { get; }
    public ICollectionView StudentsView { get; }
    public ICollectionView OrdersView { get; }
    public ObservableCollection<CourseClassModel> Classes { get; } = [];
    public bool IsAuthenticated { get => _isAuthenticated; private set { _isAuthenticated = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); } }
    public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string SuccessMessage { get => _successMessage; private set { _successMessage = value; OnPropertyChanged(); } }
    public string AdministratorName { get => _administratorName; private set { _administratorName = value; OnPropertyChanged(); } }
    public string ApiAddress => Api.BaseUri.ToString();
    public int ActiveCourses => Courses.Count(c => c.IsActive);
    public int PendingOrders => Orders.Count(o => o.PaymentStatus == "pending");
    public int ActiveStudents => Students.Count(s => s.Status == "active");
    public decimal PaidRevenue => Orders.Where(o => o.PaymentStatus == "paid").Sum(o => o.Total);
    public string LastUpdated => _lastUpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Ainda não atualizado";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); CoursesView.Refresh(); StudentsView.Refresh(); OrdersView.Refresh(); }
    }
    public CourseModel? SelectedCourse
    {
        get => _selectedCourse;
        set
        {
            if (ReferenceEquals(_selectedCourse, value)) return;
            _selectedCourse = value;
            Classes.Clear();
            SelectedClass = null;
            OnPropertyChanged(); OnPropertyChanged(nameof(Classes)); OnPropertyChanged(nameof(HasCourse));
        }
    }
    public CourseClassModel? SelectedClass
    {
        get => _selectedClass;
        set { _selectedClass = value; ClassStudents.Clear(); SelectedEnrollment = null; OnPropertyChanged(); OnPropertyChanged(nameof(HasClass)); }
    }
    public StudentModel? SelectedStudent { get => _selectedStudent; set { _selectedStudent = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStudent)); } }
    public StudentModel? SelectedEnrollment { get => _selectedEnrollment; set { _selectedEnrollment = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnrollment)); } }
    public OrderModel? SelectedOrder { get => _selectedOrder; set { _selectedOrder = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasOrder)); } }
    public bool HasCourse => SelectedCourse != null;
    public bool HasClass => SelectedClass != null;
    public bool HasStudent => SelectedStudent != null;
    public bool HasEnrollment => SelectedEnrollment != null;
    public bool HasOrder => SelectedOrder != null;

    public Task<bool> LoginAsync(string email, string password) => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Informe e-mail e senha.");
        var user = await Api.LoginAsync(email, password);
        AdministratorName = user.Nome;
        IsAuthenticated = true;
        await LoadDataAsync();
    });

    public Task<bool> RefreshAsync() => RunAsync(LoadDataAsync, "Dados atualizados.");
    public Task<bool> LoadClassesAsync() => RunAsync(() => LoadClassesCoreAsync(SelectedClass?.Id));
    private async Task LoadClassesCoreAsync(int? selectedId)
    {
        var course = SelectedCourse;
        if (course == null) { Classes.Clear(); return; }
        var classes = await Api.GetClassesAsync(course.Id);
        if (SelectedCourse?.Id != course.Id) return;
        Replace(Classes, classes);
        SelectedClass = Classes.FirstOrDefault(c => c.Id == selectedId);
    }

    public Task<bool> LoadClassStudentsAsync() => RunAsync(async () =>
    {
        var course = SelectedCourse ?? throw new ArgumentException("Selecione um curso.");
        var selectedClass = SelectedClass ?? throw new ArgumentException("Selecione uma turma.");
        var students = await Api.GetClassStudentsAsync(course.Id, selectedClass.Id);
        Replace(ClassStudents, students);
    });

    public async Task ReloadAfterMutationAsync()
    {
        if (!await RunAsync(LoadDataAsync, "Alteração salva e dados atualizados."))
            ErrorMessage = "A alteração foi salva, mas não foi possível atualizar os dados. " + ErrorMessage;
    }

    public async Task LogoutAsync()
    {
        if (IsBusy) return;
        await RunAsync(async () =>
        {
            try { await Api.LogoutAsync(); }
            finally { ResetSession(); }
        }, "Sessão encerrada.");
    }

    private async Task LoadDataAsync()
    {
        var courseId = SelectedCourse?.Id;
        var classId = SelectedClass?.Id;
        var orderId = SelectedOrder?.Id;
        var studentId = SelectedStudent?.Id;
        var courses = Api.GetCoursesAsync();
        var orders = Api.GetOrdersAsync();
        var students = Api.GetStudentsAsync();
        await Task.WhenAll(courses, orders, students);
        Replace(Courses, await courses);
        Replace(Orders, await orders);
        Replace(Students, await students);
        SelectedCourse = Courses.FirstOrDefault(c => c.Id == courseId);
        await LoadClassesCoreAsync(classId);
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == orderId);
        SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId);
        _lastUpdatedAt = DateTime.Now;
        NotifyDashboard();
    }

    public async Task<bool> RunAsync(Func<Task> action, string success = "")
    {
        if (IsBusy) return false;
        IsBusy = true;
        ErrorMessage = SuccessMessage = "";
        try
        {
            await action();
            SuccessMessage = success;
            return true;
        }
        catch (UnauthorizedAccessException exception) { Api.ClearSession(); ResetSession(); ErrorMessage = exception.Message; }
        catch (ApiException exception) { ErrorMessage = exception.Message; }
        catch (ArgumentException exception) { ErrorMessage = exception.Message; }
        catch (HttpRequestException) { ErrorMessage = "Não foi possível conectar à API. Verifique sua conexão e tente novamente."; }
        catch (TaskCanceledException) { ErrorMessage = "A API demorou para responder. A operação pode ter sido recebida; atualize os dados antes de repetir."; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        { ErrorMessage = "A resposta da API não corresponde ao esperado. Atualize os dados e confira a configuração do servidor."; }
        finally { IsBusy = false; }
        return false;
    }

    private bool Matches(string value) => value.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase);
    private void ResetSession()
    {
        IsAuthenticated = false;
        AdministratorName = "";
        Courses.Clear(); Orders.Clear(); Students.Clear(); Classes.Clear(); ClassStudents.Clear();
        SelectedCourse = null; SelectedOrder = null; SelectedStudent = null;
        SearchText = "";
        _lastUpdatedAt = null;
        NotifyDashboard();
    }
    private void NotifyDashboard()
    {
        foreach (var name in new[] { nameof(ActiveCourses), nameof(PendingOrders), nameof(ActiveStudents), nameof(PaidRevenue), nameof(LastUpdated) })
            OnPropertyChanged(name);
    }
    private static void Replace<T>(ObservableCollection<T> destination, IEnumerable<T> source)
    {
        destination.Clear();
        foreach (var item in source) destination.Add(item);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose() => Api.Dispose();
}
