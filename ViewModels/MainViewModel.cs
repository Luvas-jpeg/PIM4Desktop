using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using PIM4AdminDesktop.Models;
using PIM4AdminDesktop.Services;

namespace PIM4AdminDesktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ApiClient _apiClient;
    private bool _isAuthenticated;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _administratorName = string.Empty;
    private DateTime? _lastUpdatedAt;

    public MainViewModel(ApiClient apiClient) => _apiClient = apiClient;

    public ObservableCollection<CourseModel> Courses { get; } = [];
    public ObservableCollection<OrderModel> Orders { get; } = [];
    public ObservableCollection<StudentModel> Students { get; } = [];

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set { _isAuthenticated = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string AdministratorName { get => _administratorName; private set { _administratorName = value; OnPropertyChanged(); } }
    public string ApiAddress => _apiClient.BaseUri.ToString();
    public int ActiveCourses => Courses.Count(course => course.IsActive);
    public int PendingOrders => Orders.Count(order => order.PaymentStatus == "pending");
    public int ActiveStudents => Students.Count(student => student.Status == "active");
    public decimal PaidRevenue => Orders.Where(order => order.PaymentStatus == "paid").Sum(order => order.Total);
    public string LastUpdated => _lastUpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Ainda nao atualizado";

    public async Task LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Informe e-mail e senha.";
            return;
        }

        await ExecuteAsync(async () =>
        {
            var user = await _apiClient.LoginAsync(email, password);
            AdministratorName = user.Nome;
            IsAuthenticated = true;
            await LoadDataCoreAsync();
        });
    }

    public Task RefreshAsync() => ExecuteAsync(LoadDataCoreAsync);

    public Task LogoutAsync() => ExecuteAsync(async () =>
    {
        await _apiClient.LogoutAsync();
        ResetSession();
    });

    private async Task LoadDataCoreAsync()
    {
        var coursesTask = _apiClient.GetCoursesAsync();
        var ordersTask = _apiClient.GetOrdersAsync();
        var studentsTask = _apiClient.GetStudentsAsync();
        await Task.WhenAll(coursesTask, ordersTask, studentsTask);

        Replace(Courses, await coursesTask);
        Replace(Orders, await ordersTask);
        Replace(Students, await studentsTask);
        _lastUpdatedAt = DateTime.Now;
        NotifyDashboard();
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await action();
        }
        catch (UnauthorizedAccessException exception)
        {
            _apiClient.ClearSession();
            ResetSession();
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = $"Nao foi possivel acessar a API. {exception.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "A API demorou demais para responder.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetSession()
    {
        IsAuthenticated = false;
        AdministratorName = string.Empty;
        Courses.Clear();
        Orders.Clear();
        Students.Clear();
        _lastUpdatedAt = null;
        NotifyDashboard();
    }

    private void NotifyDashboard()
    {
        OnPropertyChanged(nameof(ActiveCourses));
        OnPropertyChanged(nameof(PendingOrders));
        OnPropertyChanged(nameof(ActiveStudents));
        OnPropertyChanged(nameof(PaidRevenue));
        OnPropertyChanged(nameof(LastUpdated));
    }

    private static void Replace<T>(ObservableCollection<T> destination, IEnumerable<T> source)
    {
        destination.Clear();
        foreach (var item in source) destination.Add(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose() => _apiClient.Dispose();
}
