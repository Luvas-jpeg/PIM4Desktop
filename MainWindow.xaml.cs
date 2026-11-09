using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PIM4AdminDesktop.Configuration;
using PIM4AdminDesktop.Services;
using PIM4AdminDesktop.ViewModels;
using PIM4AdminDesktop.Views;

namespace PIM4AdminDesktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow() : this(new ApiClient(AppConfiguration.ApiBaseUri)) { }

    public MainWindow(ApiClient api)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(api);
        DataContext = _viewModel;
        EmailTextBox.Focus();
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginAsync(EmailTextBox.Text, PasswordInput.Password);
        if (_viewModel.IsAuthenticated) PasswordInput.Clear();
    }

    private async void PasswordInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await _viewModel.LoginAsync(EmailTextBox.Text, PasswordInput.Password);
        if (_viewModel.IsAuthenticated) PasswordInput.Clear();
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        => await _viewModel.RefreshAsync();

    private async void LogoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.LogoutAsync();
        PasswordInput.Clear();
        EmailTextBox.Focus();
    }

    private async void CourseSelection_OnChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_viewModel.IsBusy && _viewModel.IsAuthenticated) await _viewModel.LoadClassesAsync();
    }

    private AdminActions Actions => new(this, _viewModel);
    private async void NewCourse_OnClick(object sender, RoutedEventArgs e) => await Actions.EditCourseAsync(true);
    private async void EditCourse_OnClick(object sender, RoutedEventArgs e) => await Actions.EditCourseAsync(false);
    private async void ArchiveCourse_OnClick(object sender, RoutedEventArgs e) => await Actions.ArchiveCourseAsync();
    private async void NewClass_OnClick(object sender, RoutedEventArgs e) => await Actions.EditClassAsync(true);
    private async void EditClass_OnClick(object sender, RoutedEventArgs e) => await Actions.EditClassAsync(false);
    private async void NewStudent_OnClick(object sender, RoutedEventArgs e) => await Actions.EditStudentAsync(true);
    private async void EditStudent_OnClick(object sender, RoutedEventArgs e) => await Actions.EditStudentAsync(false);
    private async void DeleteStudent_OnClick(object sender, RoutedEventArgs e) => await Actions.DeleteStudentAsync();
    private async void EditOrder_OnClick(object sender, RoutedEventArgs e) => await Actions.EditOrderAsync();
    private void ClassStudents_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasClass) return;
        new ClassStudentsWindow(this, _viewModel).ShowDialog();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsBusy) { e.Cancel = true; return; }
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}