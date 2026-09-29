using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PIM4AdminDesktop.Models;

namespace PIM4AdminDesktop.Services;

public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private AuthResponse? _session;

    public ApiClient(Uri baseUri)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public Uri BaseUri => _httpClient.BaseAddress!;

    public async Task<UserModel> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/Auth/login",
            new LoginRequest { Email = email.Trim(), Senha = password },
            JsonOptions,
            cancellationToken);

        _session = await ReadResponseAsync<AuthResponse>(response, cancellationToken);
        if (!string.Equals(_session.User.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            _session = null;
            throw new UnauthorizedAccessException("Este aplicativo e exclusivo para administradores.");
        }

        return _session.User;
    }

    public Task<List<CourseModel>> GetCoursesAsync(CancellationToken cancellationToken = default)
        => GetAuthenticatedAsync<List<CourseModel>>("api/courses", cancellationToken);

    public Task<List<OrderModel>> GetOrdersAsync(CancellationToken cancellationToken = default)
        => GetAuthenticatedAsync<List<OrderModel>>("api/Orders", cancellationToken);

    public Task<List<StudentModel>> GetStudentsAsync(CancellationToken cancellationToken = default)
        => GetAuthenticatedAsync<List<StudentModel>>("api/Students", cancellationToken);

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session == null) return;

        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "api/Auth/logout");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        _session = null;

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
            await ThrowApiErrorAsync(response, cancellationToken);
    }

    public void ClearSession() => _session = null;

    private async Task<T> GetAuthenticatedAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var firstRequest = CreateAuthenticatedRequest(HttpMethod.Get, path);
        using var firstResponse = await _httpClient.SendAsync(firstRequest, cancellationToken);
        if (firstResponse.StatusCode != HttpStatusCode.Unauthorized)
            return await ReadResponseAsync<T>(firstResponse, cancellationToken);

        await RefreshAsync(cancellationToken);
        using var retryRequest = CreateAuthenticatedRequest(HttpMethod.Get, path);
        using var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken);
        return await ReadResponseAsync<T>(retryResponse, cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_session == null || string.IsNullOrWhiteSpace(_session.RefreshToken))
            throw new UnauthorizedAccessException("A sessao expirou. Entre novamente.");

        using var response = await _httpClient.PostAsJsonAsync(
            "api/Auth/refresh",
            new RefreshRequest { RefreshToken = _session.RefreshToken },
            JsonOptions,
            cancellationToken);
        _session = await ReadResponseAsync<AuthResponse>(response, cancellationToken);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
    {
        if (_session == null)
            throw new UnauthorizedAccessException("A sessao administrativa nao esta ativa.");

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        return request;
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) await ThrowApiErrorAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("A API retornou uma resposta vazia.");
    }

    private static async Task ThrowApiErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Keep the status-based fallback when an intermediary returns non-JSON content.
        }

        throw response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? new UnauthorizedAccessException(error?.Message ?? "Acesso nao autorizado.")
            : new HttpRequestException(error?.Message ?? $"A API retornou {(int)response.StatusCode}.");
    }

    public void Dispose() => _httpClient.Dispose();
}