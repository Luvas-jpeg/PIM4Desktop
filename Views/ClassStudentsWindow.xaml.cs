using System.ComponentModel;
using System.Windows;
using PIM4AdminDesktop.ViewModels;

namespace PIM4AdminDesktop.Views;

public partial class ClassStudentsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AdminActions _actions;
    public ClassStudentsWindow(Window owner, MainViewModel vm)
    {
        InitializeComponent(); Owner = owner; DataContext = _vm = vm;
        _actions = new(this, vm);
        vm.PropertyChanged += SessionChanged;
        Loaded += async (_, _) => await vm.LoadClassStudentsAsync();
        Closed += (_, _) => vm.PropertyChanged -= SessionChanged;
    }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAuthenticated) && !_vm.IsAuthenticated)
            Dispatcher.BeginInvoke(new Action(Close));
        if (e.PropertyName == nameof(MainViewModel.IsBusy))
        {
            ActionsBar.IsEnabled = !_vm.IsBusy;
            BusyOverlay.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private async void Refresh_OnClick(object sender, RoutedEventArgs e) => await _vm.LoadClassStudentsAsync();
    private async void Status_OnClick(object sender, RoutedEventArgs e) => await _actions.EditEnrollmentAsync(false);
    private async void Transfer_OnClick(object sender, RoutedEventArgs e) => await _actions.EditEnrollmentAsync(true);
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.IsBusy) e.Cancel = true;
        base.OnClosing(e);
    }
}
