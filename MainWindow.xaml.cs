using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PIM4AdminDesktop.Configuration;
using PIM4AdminDesktop.Services;
using PIM4AdminDesktop.ViewModels;

namespace PIM4AdminDesktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new ApiClient(AppConfiguration.ApiBaseUri));
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

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}