using CivicOps.Application.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace CivicOps.Infrastructure.External.Firebase;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(IConfiguration config,
        ILogger<FirebasePushNotificationService> logger)
    {
        _logger = logger;

        var credentialPath = config["Firebase:CredentialPath"];
        if (!string.IsNullOrEmpty(credentialPath) && File.Exists(credentialPath))
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(credentialPath)
                });
            }
            _messaging = FirebaseMessaging.DefaultInstance;
        }
        else
        {
            _logger.LogWarning("Firebase credentials not configured. Push notifications disabled.");
            _messaging = null!;
        }
    }

    public async Task SendAsync(string fcmToken, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        if (_messaging is null) return;

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification { Title = title, Body = body },
                Data = data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Sound = "default", Badge = 1 }
                }
            };

            await _messaging.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notification failed for token {Token}", fcmToken[..10]);
        }
    }

    public async Task SendToMultipleAsync(IEnumerable<string> fcmTokens, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        if (_messaging is null) return;

        var tokens = fcmTokens.ToList();
        if (!tokens.Any()) return;

        // Firebase supports max 500 tokens per batch
        foreach (var batch in tokens.Chunk(500))
        {
            var message = new MulticastMessage
            {
                Tokens = batch.ToList(),
                Notification = new Notification { Title = title, Body = body },
                Data = data
            };

            try
            {
                var result = await _messaging.SendEachForMulticastAsync(message, ct);
                if (result.FailureCount > 0)
                    _logger.LogWarning("{Count} push notifications failed in batch", result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch push notification failed");
            }
        }
    }

    public async Task SendToTopicAsync(string topic, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        if (_messaging is null) return;

        var message = new Message
        {
            Topic = topic,
            Notification = new Notification { Title = title, Body = body },
            Data = data
        };

        await _messaging.SendAsync(message, ct);
    }
}

namespace CivicOps.Infrastructure.External.Twilio;

public class TwilioSmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly bool _configured;

    public TwilioSmsService(IConfiguration config, ILogger<TwilioSmsService> logger)
    {
        _config = config;
        _logger = logger;

        var sid = config["Twilio:AccountSid"];
        var token = config["Twilio:AuthToken"];

        if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(token))
        {
            TwilioClient.Init(sid, token);
            _configured = true;
        }
        else
        {
            _logger.LogWarning("Twilio not configured. SMS disabled.");
        }
    }

    public async Task SendAsync(string to, string message, CancellationToken ct = default)
    {
        if (!_configured)
        {
            _logger.LogInformation("SMS to {To}: {Message}", to, message);
            return;
        }

        try
        {
            var from = _config["Twilio:FromNumber"];
            await MessageResource.CreateAsync(
                to: new global::Twilio.Types.PhoneNumber(to),
                from: new global::Twilio.Types.PhoneNumber(from),
                body: message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS send failed to {To}", to);
        }
    }
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Email:FromName"] ?? "CivicOps Command",
                _config["Email:FromAddress"] ?? "noreply@civicops.io"
            ));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:SmtpHost"] ?? "localhost",
                int.Parse(_config["Email:SmtpPort"] ?? "587"),
                MailKit.Security.SecureSocketOptions.StartTls,
                ct
            );

            if (!string.IsNullOrEmpty(_config["Email:SmtpUser"]))
                await client.AuthenticateAsync(_config["Email:SmtpUser"],
                    _config["Email:SmtpPassword"], ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email send failed to {To} with subject {Subject}", to, subject);
        }
    }

    public async Task SendTemplateAsync(string to, string templateId, object templateData,
        CancellationToken ct = default)
    {
        // Template rendering would use a library like Scriban or Handlebars.NET
        // Simplified implementation
        await SendAsync(to, templateId, System.Text.Json.JsonSerializer.Serialize(templateData), ct);
    }
}

// SignalR service — broadcasts real-time events to connected clients
namespace CivicOps.Infrastructure.GPS;

using CivicOps.Api.Hubs;

public class SignalRNotificationService : ISignalRService
{
    private readonly IHubContext<OperationsHub> _hub;

    public SignalRNotificationService(IHubContext<OperationsHub> hub) => _hub = hub;

    public async Task SendToTenantAsync(Guid tenantId, string method, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"tenant:{tenantId}").SendAsync(method, payload, ct);

    public async Task SendToUserAsync(Guid userId, string method, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"user:{userId}").SendAsync(method, payload, ct);

    public async Task SendToGroupAsync(string group, string method, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group(group).SendAsync(method, payload, ct);

    public async Task SendGpsUpdateAsync(Guid tenantId, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"fleet:{tenantId}").SendAsync("GpsUpdate", payload, ct);

    public async Task SendPanicAlertAsync(Guid tenantId, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"tenant:{tenantId}").SendAsync("PanicTriggered", payload, ct);

    public async Task SendIncidentCreatedAsync(Guid tenantId, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"incidents:{tenantId}").SendAsync("IncidentCreated", payload, ct);

    public async Task SendDispatchUpdateAsync(Guid tenantId, object payload, CancellationToken ct = default)
        => await _hub.Clients.Group($"dispatch:{tenantId}").SendAsync("DispatchUpdate", payload, ct);
}
