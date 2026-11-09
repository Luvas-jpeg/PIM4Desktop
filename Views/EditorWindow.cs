using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PIM4AdminDesktop.Views;

// Shared modal layout; each screen provides its own fields and validation.
public sealed class EditorWindow : Window
{
    private readonly StackPanel _fields = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 8) };
    private readonly Button _save = new() { Content = "Salvar", MinWidth = 110, Padding = new Thickness(16, 10, 16, 10), IsDefault = true };
    private readonly Button _cancel = new() { Content = "Cancelar", MinWidth = 100, Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
    private bool _saving, _allowClose;
    public Func<Task<bool>>? SaveAsync { get; set; }
    public Func<string>? ErrorMessage { get; set; }
    public Func<bool>? SessionActive { get; set; }

    public EditorWindow(Window owner, string title)
    {
        Owner = owner; Title = title; Width = 600; MaxHeight = SystemParameters.WorkArea.Height - 48;
        SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize; Background = Brushes.White;
        var layout = new DockPanel { Margin = new Thickness(24) };
        var header = new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) };
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(_cancel); actions.Children.Add(_save); footer.Children.Add(actions);
        DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer { Content = _fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        _save.Click += async (_, _) =>
        {
            if (_saving || SaveAsync == null) return;
            _saving = true; _save.IsEnabled = _cancel.IsEnabled = _fields.IsEnabled = false;
            _error.Text = "Salvando…";
            try
            {
                if (await SaveAsync()) { _allowClose = true; DialogResult = true; return; }
                _error.Text = ErrorMessage?.Invoke() ?? "Não foi possível salvar.";
                if (SessionActive?.Invoke() == false) { _allowClose = true; DialogResult = false; }
            }
            finally { _saving = false; _save.IsEnabled = _cancel.IsEnabled = _fields.IsEnabled = true; }
        };
    }

    public void Note(string text) => _fields.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 12) });
    public TextBox Text(string label, string value = "", bool multiline = false, bool readOnly = false)
    {
        var input = new TextBox { Text = value, Padding = new Thickness(8), IsReadOnly = readOnly, AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 90 : 36 };
        Add(label, input); return input;
    }
    public ComboBox Choice(string label, IEnumerable<KeyValuePair<string, string>> options, string? selected)
    {
        var input = new ComboBox { ItemsSource = options.ToList(), DisplayMemberPath = "Value", SelectedValuePath = "Key", Padding = new Thickness(8), MinHeight = 36 };
        input.SelectedValue = selected;
        Add(label, input); return input;
    }
    public CheckBox Check(string label, bool value)
    {
        var input = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 10, 0, 8) };
        _fields.Children.Add(input); return input;
    }
    private void Add(string label, Control input)
    {
        _fields.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 5) });
        _fields.Children.Add(input);
        System.Windows.Automation.AutomationProperties.SetName(input, label);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_saving && !_allowClose) e.Cancel = true;
        base.OnClosing(e);
    }
}
