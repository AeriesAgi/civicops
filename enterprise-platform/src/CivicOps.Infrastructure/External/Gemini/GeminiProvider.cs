using CivicOps.Application.Interfaces;
using CivicOps.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CivicOps.Infrastructure.External.Gemini;

/// <summary>
/// Google Gemini Pro API integration implementing ILLMProvider.
/// Swap for ClaudeProvider or OpenAIProvider by registering a different implementation.
/// </summary>
public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiProvider> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const string Model = "gemini-1.5-pro";

    public string ProviderName => "Google Gemini";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public GeminiProvider(IHttpClientFactory httpClientFactory, IConfiguration config,
        ILogger<GeminiProvider> logger)
    {
        _http = httpClientFactory.CreateClient("gemini");
        _apiKey = config["AI:Gemini:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Gemini API key not configured.");

        var request = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = $"{systemPrompt}\n\n{userPrompt}" } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024,
                responseMimeType = "application/json"
            }
        };

        var url = $"{BaseUrl}/models/{Model}:generateContent?key={_apiKey}";

        try
        {
            var response = await _http.PostAsJsonAsync(url, request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
                ?? throw new Exception("Empty response from Gemini.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            throw;
        }
    }

    public async Task<string> CompleteWithHistoryAsync(string systemPrompt,
        IEnumerable<(string role, string content)> history, CancellationToken ct = default)
    {
        var contents = new List<object>();

        // Add system context as first user message
        contents.Add(new { role = "user", parts = new[] { new { text = systemPrompt } } });
        contents.Add(new { role = "model", parts = new[] { new { text = "Understood." } } });

        foreach (var (role, content) in history)
        {
            var geminiRole = role == "assistant" ? "model" : "user";
            contents.Add(new { role = geminiRole, parts = new[] { new { text = content } } });
        }

        var request = new
        {
            contents,
            generationConfig = new { temperature = 0.3, maxOutputTokens = 2048 }
        };

        var url = $"{BaseUrl}/models/{Model}:generateContent?key={_apiKey}";
        var response = await _http.PostAsJsonAsync(url, request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(ct);
        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new { model = "text-embedding-004", content = new { parts = new[] { new { text } } } };
        var url = $"{BaseUrl}/models/text-embedding-004:embedContent?key={_apiKey}";

        var response = await _http.PostAsJsonAsync(url, request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(ct);
        return result?.Embedding?.Values ?? Array.Empty<float>();
    }

    private record GeminiResponse(GeminiCandidate[]? Candidates);
    private record GeminiCandidate(GeminiContent? Content);
    private record GeminiContent(GeminiPart[]? Parts);
    private record GeminiPart(string? Text);
    private record GeminiEmbedResponse(GeminiEmbedding? Embedding);
    private record GeminiEmbedding(float[]? Values);
}

namespace CivicOps.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
        _secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured.");
        _issuer = config["Jwt:Issuer"] ?? "https://civicops.io";
        _audience = config["Jwt:Audience"] ?? "civicops-client";
    }

    public string GenerateAccessToken(User user, Tenant tenant)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("tenant_slug", tenant.Slug),
            new Claim("full_name", user.FullName),
            new Claim("role", user.Role.ToString()),
            new Claim("region_id", user.RegionId?.ToString() ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public bool ValidateAccessToken(string token, out Guid userId, out Guid tenantId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            tenantId = Guid.Parse(principal.FindFirstValue("tenant_id")!);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class PasswordService : IPasswordService
{
    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    public bool IsStrongPassword(string password)
        => password.Length >= 8
           && password.Any(char.IsUpper)
           && password.Any(char.IsLower)
           && password.Any(char.IsDigit)
           && password.Any(c => !char.IsLetterOrDigit(c));
}

public class MfaService : IMfaService
{
    public string GenerateSecret()
    {
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Base32Encoding.ToString(bytes);
    }

    public string GenerateQrCodeUri(string secret, string email, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secret, string code)
    {
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        }
        catch
        {
            return false;
        }
    }

    public string GenerateBackupCode()
    {
        var bytes = new byte[6];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class AuditService : IAuditService
{
    private readonly CivicOpsDbContext _db;

    public AuditService(CivicOpsDbContext db) => _db = db;

    public async Task LogAsync(Guid tenantId, Guid? userId, string action,
        string? entityType = null, string? entityId = null,
        object? oldValues = null, object? newValues = null,
        CancellationToken ct = default)
    {
        var log = AuditLog.Create(tenantId, userId, action, entityType, entityId,
            oldValues is not null ? JsonSerializer.Serialize(oldValues) : null,
            newValues is not null ? JsonSerializer.Serialize(newValues) : null);

        await _db.AuditLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }
}
