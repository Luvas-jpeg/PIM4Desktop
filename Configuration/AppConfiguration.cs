using System.IO;
using System.Text.Json;

namespace PIM4AdminDesktop.Configuration;

public static class AppConfiguration
{
    public static Uri ApiBaseUri
    {
        get
        {
            var configuredUrl = Environment.GetEnvironmentVariable("PIM4_API_BASE_URL");
            if (string.IsNullOrWhiteSpace(configuredUrl)) configuredUrl = ReadConfiguredUrl();

            if (!Uri.TryCreate(EnsureTrailingSlash(configuredUrl), UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Api:BaseUrl deve ser uma URL absoluta valida.");

            return uri;
        }
    }

    private static string ReadConfiguredUrl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return "http://localhost:5278/";

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("Api").GetProperty("BaseUrl").GetString()
            ?? "http://localhost:5278/";
    }

    private static string EnsureTrailingSlash(string? value)
    {
        var url = value?.Trim() ?? string.Empty;
        return url.EndsWith('/') ? url : $"{url}/";
    }
}