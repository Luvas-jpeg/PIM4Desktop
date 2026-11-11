using System.IO;
using System.Text.Json;

namespace PIM4AdminDesktop.Configuration;

public static class AppConfiguration
{
    public static Uri ApiBaseUri
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("PIM4_API_BASE_URL");
            if (string.IsNullOrWhiteSpace(configured))
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(path)) configured = "https://pim4-api-demo.onrender.com/";
                else
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    configured = document.RootElement.GetProperty("Api").GetProperty("BaseUrl").GetString();
                }
            }
            return ValidateBaseUri(configured);
        }
    }

    public static Uri ValidateBaseUri(string? configured)
    {
        if (!Uri.TryCreate(configured?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new InvalidOperationException("Configure a URL raiz da API, sem /api, usuário, senha ou parâmetros.");
        if (uri.Scheme != "https" && !uri.IsLoopback)
            throw new InvalidOperationException("Use HTTPS para acessar uma API externa. HTTP é permitido somente para desenvolvimento local.");
        return uri;
    }
}
