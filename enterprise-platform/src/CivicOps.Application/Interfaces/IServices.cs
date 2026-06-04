using CivicOps.Domain.Entities;

namespace CivicOps.Application.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    void SetTenant(string tenantId);
}

public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Email { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}

public interface ITokenService
{
    string GenerateAccessToken(User user, Tenant tenant);
    string GenerateRefreshToken();
    bool ValidateAccessToken(string token, out Guid userId, out Guid tenantId);
}

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    bool IsStrongPassword(string password);
}

public interface IMfaService
{
    string GenerateSecret();
    string GenerateQrCodeUri(string secret, string email, string issuer);
    bool ValidateCode(string secret, string code);
    string GenerateBackupCode();
}

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType,
        string folder, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string storageKey, int expiryMinutes = 60, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string storageKey, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplateAsync(string to, string templateId, object templateData, CancellationToken ct = default);
}

public interface ISmsService
{
    Task SendAsync(string to, string message, CancellationToken ct = default);
}

public interface IPushNotificationService
{
    Task SendAsync(string fcmToken, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task SendToMultipleAsync(IEnumerable<string> fcmTokens, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task SendToTopicAsync(string topic, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default);
}

public interface IWhatsAppService
{
    Task SendAsync(string to, string message, CancellationToken ct = default);
    Task SendTemplateAsync(string to, string templateName, object parameters, CancellationToken ct = default);
}

public interface ISignalRService
{
    Task SendToTenantAsync(Guid tenantId, string method, object payload, CancellationToken ct = default);
    Task SendToUserAsync(Guid userId, string method, object payload, CancellationToken ct = default);
    Task SendToGroupAsync(string group, string method, object payload, CancellationToken ct = default);
    Task SendGpsUpdateAsync(Guid tenantId, object gpsPayload, CancellationToken ct = default);
    Task SendPanicAlertAsync(Guid tenantId, object panicPayload, CancellationToken ct = default);
    Task SendIncidentCreatedAsync(Guid tenantId, object incidentPayload, CancellationToken ct = default);
    Task SendDispatchUpdateAsync(Guid tenantId, object dispatchPayload, CancellationToken ct = default);
}

public interface ILLMProvider
{
    string ProviderName { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<string> CompleteWithHistoryAsync(string systemPrompt,
        IEnumerable<(string role, string content)> history, CancellationToken ct = default);
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    bool IsAvailable { get; }
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
}

public interface ILiveFleetCache
{
    Task SetVehiclePositionAsync(Guid vehicleId, VehiclePositionCacheItem position, CancellationToken ct = default);
    Task<VehiclePositionCacheItem?> GetVehiclePositionAsync(Guid vehicleId, CancellationToken ct = default);
    Task<IEnumerable<VehiclePositionCacheItem>> GetAllPositionsAsync(Guid tenantId, CancellationToken ct = default);
    Task RemoveVehicleAsync(Guid vehicleId, CancellationToken ct = default);
}

public record VehiclePositionCacheItem(
    Guid VehicleId,
    Guid TenantId,
    string Registration,
    string? Alias,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKmh,
    decimal? HeadingDeg,
    bool? IgnitionOn,
    decimal? FuelLevelPct,
    string Status,
    string VehicleType,
    Guid? AssignedDriverId,
    string? DriverName,
    DateTime UpdatedAt
);

public interface IReportService
{
    Task<byte[]> GeneratePdfReportAsync(string templateName, object data, CancellationToken ct = default);
    Task<byte[]> GenerateExcelReportAsync(string sheetName, IEnumerable<object> data, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(Guid tenantId, Guid? userId, string action, string? entityType = null,
        string? entityId = null, object? oldValues = null, object? newValues = null,
        CancellationToken ct = default);
}
