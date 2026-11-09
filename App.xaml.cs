using System.Globalization;
using System.Windows;

namespace PIM4AdminDesktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        base.OnStartup(e);
        try { new MainWindow().Show(); }
        catch (Exception error) when (error is InvalidOperationException or System.Text.Json.JsonException or System.IO.IOException or KeyNotFoundException)
        {
            MessageBox.Show("Não foi possível iniciar o painel. Confira Api:BaseUrl em appsettings.json ou PIM4_API_BASE_URL.\n" + error.Message,
                "Configuração do painel", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}