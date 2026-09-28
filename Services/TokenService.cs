using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SapiSkProxy.Models;
using SapiSkProxy.Models.Auth;
using SapiSkProxy.Options;

namespace SapiSkProxy.Services;

public class TokenService
{
    private readonly HttpClient _httpClient;
    private readonly SapiSkOptions _options;
    private readonly ILogger<TokenService> _logger;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiryUtc;
    private readonly object _lock = new();

    public TokenService(HttpClient httpClient, IOptions<SapiSkOptions> options, ILogger<TokenService> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _options = options.Value;
        _logger = logger;
    }

    public string? AccessToken => _accessToken;

    public async Task<string> GetAccessTokenAsync()
    {
        lock (_lock)
        {
            if (_accessToken != null && _tokenExpiryUtc > DateTime.UtcNow.AddSeconds(_options.RefreshAheadSeconds))
                return _accessToken;
        }

        return await RefreshTokenInternalAsync();
    }

    public async Task<string> RefreshTokenInternalAsync()
    {
        lock (_lock)
        {
            if (_accessToken != null && _tokenExpiryUtc > DateTime.UtcNow.AddSeconds(_options.RefreshAheadSeconds))
                return _accessToken!;
        }

        _logger.LogDebug("Acquiring new access token from SAPI-SK");

        // Try refresh token first
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try
            {
                var renewResponse = await TryRefreshWithRefreshTokenAsync();
                if (renewResponse != null)
                    return renewResponse;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Refresh token failed, falling back to client credentials");
            }
        }

        // Fall back to client credentials
        var request = new TokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            GrantType = "client_credentials"
        };

        var response = await _httpClient.PostAsJsonAsync("auth/token", request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to obtain token: {Status} - {Body}", response.StatusCode, responseText);
            throw new InvalidOperationException($"Token acquisition failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseText);
        if (tokenResponse == null) throw new InvalidOperationException("Invalid token response");

        lock (_lock)
        {
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            _refreshToken = null; // client_credentials grant doesn't return refresh token
        }

        _logger.LogDebug("Access token acquired, expires in {Seconds}s", tokenResponse.ExpiresIn);
        return _accessToken!;
    }

    private async Task<string?> TryRefreshWithRefreshTokenAsync()
    {
        var request = new { refresh_token = _refreshToken };
        var response = await _httpClient.PostAsJsonAsync("auth/renew", request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Refresh failed: {response.StatusCode}");

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseText);
        if (tokenResponse == null) return null;

        lock (_lock)
        {
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            _refreshToken = null; // rotation: old refresh token invalidated
        }

        return _accessToken;
    }

    public async Task<bool> RevokeTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            _logger.LogWarning("No refresh token available to revoke");
            return false;
        }

        try
        {
            var request = new { token = _refreshToken, token_type_hint = "refresh_token" };
            var response = await _httpClient.PostAsJsonAsync("auth/revoke", request);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                lock (_lock)
                {
                    _refreshToken = null;
                    _accessToken = null;
                }
                _logger.LogInformation("Refresh token revoked successfully");
                return true;
            }

            _logger.LogWarning("Token revocation failed: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return false;
        }
    }

    public async Task<TokenStatusResponse?> GetTokenStatusAsync()
    {
        var token = await GetAccessTokenAsync();

        var response = await _httpClient.GetAsync("auth/token/status");
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Token status check failed: {Status}", response.StatusCode);
            return null;
        }

        return JsonSerializer.Deserialize<TokenStatusResponse>(responseText);
    }

    public void SetRefreshToken(string refreshToken)
    {
        lock (_lock)
            _refreshToken = refreshToken;
    }
}
