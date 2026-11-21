using System.Net.Http.Headers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

public class LinkedInOAuthClient : ILinkedInClient
{
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;
    private string _clientId;
    private readonly Action<string>? _browserLauncher;
    private readonly Func<int>? _callbackPortFactory;
    private const string RedirectPrefix = "http://127.0.0.1:";

    public string? CustomBaseUrl { get; set; }

    public LinkedInOAuthClient(
        ISecureStorageService secureStorage,
        HttpClient? httpClient = null,
        string clientId = "",
        Action<string>? browserLauncher = null,
        Func<int>? callbackPortFactory = null)
    {
        _secureStorage = secureStorage;
        _httpClient = httpClient ?? new HttpClient();
        _clientId = clientId;
        _browserLauncher = browserLauncher;
        _callbackPortFactory = callbackPortFactory;
    }

    public string BuildAuthorizationUrl(string state, string redirectUri, string codeChallenge)
    {
        string baseUrl = CustomBaseUrl ?? "https://www.linkedin.com/oauth/native-pkce/authorization";
        return $"{baseUrl}?response_type=code&client_id={Uri.EscapeDataString(_clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}&scope=openid%20profile%20email&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";
    }

    public async Task<LinkedInSessionInfo> ExchangeCodeForTokenAsync(
        string code,
        string state,
        string expectedState,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. CSRF State Validation
        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("OAuth CSRF state mismatch. The callback state does not match the generated state.");
        }

        // 2. User Denial or Cancellation Check
        if (string.IsNullOrWhiteSpace(code) ||
            code.Contains("access_denied", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("user_cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCanceledException("User cancelled or denied the LinkedIn authorization request.");
        }

        // 3. Token Exchange
        string tokenEndpoint = CustomBaseUrl != null
            ? $"{CustomBaseUrl.TrimEnd('/')}/oauth/v2/accessToken"
            : "https://www.linkedin.com/oauth/v2/accessToken";

        var formParams = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _clientId,
            ["code_verifier"] = codeVerifier
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(formParams)
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new HttpRequestException("Network failure communicating with LinkedIn OAuth endpoint.", ex);
        }

        using (response)
        {
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LinkedIn OAuth token exchange failed with status {response.StatusCode}: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            string accessToken = root.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Missing access_token in LinkedIn response.");
            int expiresIn = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 5184000;
            string? memberId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            await _secureStorage.StoreSecretAsync("LINKEDIN_ACCESS_TOKEN", accessToken);
            if (!string.IsNullOrWhiteSpace(memberId))
                await _secureStorage.StoreSecretAsync("LINKEDIN_MEMBER_ID", memberId);
            await _secureStorage.StoreSecretAsync("LINKEDIN_TOKEN_EXPIRY", DateTime.UtcNow.AddSeconds(expiresIn).ToString("o"));

        }

        try
        {
            await StoreUserInfoAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // A successful token exchange remains valid if optional identity metadata is unavailable.
        }

        return await GetSessionInfoAsync(cancellationToken);
    }

    private async Task StoreUserInfoAsync(CancellationToken cancellationToken)
    {
        var token = await _secureStorage.GetSecretAsync("LINKEDIN_ACCESS_TOKEN");
        if (string.IsNullOrWhiteSpace(token)) return;

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;
        string? sub = ReadString(root, "sub");
        string? name = ReadString(root, "name");
        if (string.IsNullOrWhiteSpace(name))
            name = string.Join(" ", new[] { ReadString(root, "given_name"), ReadString(root, "family_name") }.Where(s => !string.IsNullOrWhiteSpace(s)));
        string? picture = ReadString(root, "picture");

        if (!string.IsNullOrWhiteSpace(sub)) await _secureStorage.StoreSecretAsync("LINKEDIN_MEMBER_ID", sub);
        if (!string.IsNullOrWhiteSpace(name)) await _secureStorage.StoreSecretAsync("LINKEDIN_FULL_NAME", name);
        if (!string.IsNullOrWhiteSpace(picture)) await _secureStorage.StoreSecretAsync("LINKEDIN_AVATAR_URL", picture);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public async Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = await _secureStorage.GetSecretAsync("LINKEDIN_ACCESS_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            var existingSession = await GetSessionInfoAsync(cancellationToken);
            if (existingSession.State == LinkedInConnectionState.Connected)
                return existingSession;

            await DisconnectAsync(cancellationToken);
        }

        _clientId = await _secureStorage.GetSecretAsync("LINKEDIN_CLIENT_ID") ?? _clientId;
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            throw new InvalidOperationException("Configure your LinkedIn app Client ID before connecting.");
        }

        string state = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string codeVerifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        string codeChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        int callbackPort = _callbackPortFactory?.Invoke() ?? GetAvailableLoopbackPort();
        string redirectUri = $"{RedirectPrefix}{callbackPort}/callback/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException("Could not open a local LinkedIn OAuth callback port. Close applications using loopback ports and retry.", ex);
        }

        try
        {
            string authorizationUrl = BuildAuthorizationUrl(state, redirectUri, codeChallenge);
            if (_browserLauncher is not null)
                _browserLauncher(authorizationUrl);
            else
                Process.Start(new ProcessStartInfo(authorizationUrl) { UseShellExecute = true });
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            if (context.Request.RemoteEndPoint is not { Address: var address } || !IPAddress.IsLoopback(address))
                throw new InvalidOperationException("LinkedIn OAuth callback must come from the local computer.");
            string? callbackState = context.Request.QueryString["state"];
            string? code = context.Request.QueryString["code"];
            string? error = context.Request.QueryString["error"];
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            byte[] page = System.Text.Encoding.UTF8.GetBytes("<!doctype html><title>LIpstick</title><p>LinkedIn sign-in response received. You can return to LIpstick.</p>");
            await context.Response.OutputStream.WriteAsync(page, cancellationToken);
            context.Response.Close();

            if (!string.IsNullOrWhiteSpace(error))
                throw new OperationCanceledException("LinkedIn authorization was declined or cancelled.");
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("LinkedIn returned no authorization code.");

            return await ExchangeCodeForTokenAsync(code, callbackState ?? "", state, redirectUri, codeVerifier, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new HttpRequestException("LinkedIn authorization did not complete. Check network access and try again.", ex);
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int GetAvailableLoopbackPort()
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        return ((IPEndPoint)portProbe.LocalEndpoint).Port;
    }

    public async Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var token = await _secureStorage.GetSecretAsync("LINKEDIN_ACCESS_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            return new LinkedInSessionInfo(
                LinkedInConnectionState.Disconnected,
                null, null, null, null, null,
                "Disconnected"
            );
        }

        var memberId = await _secureStorage.GetSecretAsync("LINKEDIN_MEMBER_ID");
        var expiryStr = await _secureStorage.GetSecretAsync("LINKEDIN_TOKEN_EXPIRY");
        DateTime? expiry = DateTime.TryParse(expiryStr, out var dt) ? dt : null;

        if (expiry.HasValue && expiry.Value <= DateTime.UtcNow)
        {
            return new LinkedInSessionInfo(
                LinkedInConnectionState.TokenExpiring,
                memberId,
                await _secureStorage.GetSecretAsync("LINKEDIN_FULL_NAME"),
                await _secureStorage.GetSecretAsync("LINKEDIN_HEADLINE"),
                await _secureStorage.GetSecretAsync("LINKEDIN_AVATAR_URL"),
                expiry,
                "Access token is expired"
            );
        }

        var fullName = await _secureStorage.GetSecretAsync("LINKEDIN_FULL_NAME");
        var headline = await _secureStorage.GetSecretAsync("LINKEDIN_HEADLINE");
        var avatar = await _secureStorage.GetSecretAsync("LINKEDIN_AVATAR_URL");

        return new LinkedInSessionInfo(
            LinkedInConnectionState.Connected,
            memberId,
            fullName,
            headline,
            avatar,
            expiry,
            "Connected"
        );
    }

    public async Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var token = await _secureStorage.GetSecretAsync("LINKEDIN_ACCESS_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Cannot ingest profile without an active LinkedIn session.");
        }

        string endpoint = "https://api.linkedin.com/v2/userinfo";

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return LinkedInProfileNormalizer.NormalizeJson(responseString);

            throw new HttpRequestException($"LinkedIn profile request failed with status {response.StatusCode}.");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            throw new HttpRequestException("Could not retrieve the LinkedIn profile. Check connectivity and granted permissions.", ex);
        }
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientId = await _secureStorage.GetSecretAsync("LINKEDIN_CLIENT_ID") ?? _clientId;
        await DisconnectAsync(cancellationToken);
        return false;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _secureStorage.DeleteSecretAsync("LINKEDIN_ACCESS_TOKEN");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_REFRESH_TOKEN");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_MEMBER_ID");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_TOKEN_EXPIRY");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_FULL_NAME");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_HEADLINE");
        await _secureStorage.DeleteSecretAsync("LINKEDIN_AVATAR_URL");
    }
}
