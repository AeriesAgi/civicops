# WhatsApp Cloud API Setup Guide

## Overview

CivicOps integrates with WhatsApp Cloud API to enable residents to report civic issues via WhatsApp text messages and voice notes. This guide covers the complete setup process.

---

## Prerequisites

- Meta Business Account
- Facebook Developer Account
- Verified business phone number
- CivicOps application deployed with public HTTPS endpoint

---

## Setup Steps

### Step 1: Create Meta Business App

1. **Visit Facebook Developers:**
   - Go to https://developers.facebook.com/
   - Log in with your Facebook account

2. **Create New App:**
   - Click "Create App"
   - Select "Business" as app type
   - Fill in app details:
     - App Name: "CivicOps"
     - Contact Email: your email
     - Business Account: Select or create

3. **Add WhatsApp Product:**
   - In app dashboard, click "Add Product"
   - Find "WhatsApp" and click "Set Up"

---

### Step 2: Configure WhatsApp Business API

1. **Get Test Number:**
   - WhatsApp provides a test number for development
   - Note the Phone Number ID
   - Add your phone number to test recipients

2. **Generate Access Token:**
   - Go to WhatsApp → Getting Started
   - Copy the temporary access token
   - **Important:** This expires in 24 hours

3. **Get Permanent Access Token:**
   - Go to Settings → Basic
   - Create a System User in Business Settings
   - Generate permanent token with `whatsapp_business_messaging` permission

---

### Step 3: Configure Webhook

1. **Set Webhook URL:**
   - In WhatsApp settings, click "Configuration"
   - Enter Callback URL: `https://yourdomain.com/webhooks/whatsapp`
   - Enter Verify Token: Create a secure random string

2. **Subscribe to Webhooks:**
   - Subscribe to: `messages`
   - This allows receiving inbound messages

3. **Verify Webhook:**
   - Meta will send GET request to verify
   - CivicOps automatically handles verification

---

### Step 4: Configure Environment Variables

Create `.env` file or set environment variables:

```bash
# WhatsApp Configuration
WHATSAPP_VERIFY_TOKEN=your_secure_verify_token_here
WHATSAPP_ACCESS_TOKEN=your_permanent_access_token_here
WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id_here
WHATSAPP_DEMO_MODE=false

# Optional: Business Account ID
WHATSAPP_BUSINESS_ACCOUNT_ID=your_business_account_id
```

**Security Notes:**
- Never commit these values to Git
- Use secure token generation
- Rotate tokens periodically
- Store in secure configuration management

---

## Webhook Implementation

### Verification Endpoint (GET)

CivicOps implements the verification endpoint:

```csharp
[HttpGet]
public IActionResult VerifyWebhook(
    [FromQuery(Name = "hub.mode")] string mode,
    [FromQuery(Name = "hub.verify_token")] string token,
    [FromQuery(Name = "hub.challenge")] string challenge)
{
    var verifyToken = _configuration["WHATSAPP_VERIFY_TOKEN"];
    
    if (mode == "subscribe" && token == verifyToken)
    {
        return Ok(challenge);
    }
    
    return Forbid();
}
```

### Message Endpoint (POST)

Receives inbound messages:

```csharp
[HttpPost]
public async Task<IActionResult> ReceiveMessage(
    [FromBody] WhatsAppWebhookPayload payload)
{
    // Process message
    // Create incident
    // Send reply (optional)
    
    return Ok(new { success = true });
}
```

---

## Message Flow

### Inbound Message Flow

1. **Resident sends WhatsApp message:**
   ```
   "Burst water pipe on Main Road in Chatsworth"
   ```

2. **Meta forwards to webhook:**
   ```json
   {
     "object": "whatsapp_business_account",
     "entry": [{
       "changes": [{
         "value": {
           "messages": [{
             "from": "27821234567",
             "type": "text",
             "text": {
               "body": "Burst water pipe on Main Road in Chatsworth"
             }
           }]
         }
       }]
     }]
   }
   ```

3. **CivicOps processes message:**
   - Extracts text content
   - Classifies incident (Gemini or deterministic)
   - Creates incident record
   - Generates reference number

4. **Optional: Send reply:**
   ```
   "Thank you! Your reference number is CIV-2026-0123. 
   The Water & Sanitation department has been notified."
   ```

---

## Sending Messages (Optional)

### Send Text Message

```csharp
public async Task SendWhatsAppMessage(string to, string message)
{
    var accessToken = _configuration["WHATSAPP_ACCESS_TOKEN"];
    var phoneNumberId = _configuration["WHATSAPP_PHONE_NUMBER_ID"];
    
    var payload = new
    {
        messaging_product = "whatsapp",
        to = to,
        type = "text",
        text = new { body = message }
    };
    
    var url = $"https://graph.facebook.com/v18.0/{phoneNumberId}/messages";
    
    var request = new HttpRequestMessage(HttpMethod.Post, url);
    request.Headers.Add("Authorization", $"Bearer {accessToken}");
    request.Content = new StringContent(
        JsonSerializer.Serialize(payload),
        Encoding.UTF8,
        "application/json"
    );
    
    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
}
```

### Send Template Message

```csharp
var payload = new
{
    messaging_product = "whatsapp",
    to = to,
    type = "template",
    template = new
    {
        name = "incident_confirmation",
        language = new { code = "en" },
        components = new[]
        {
            new
            {
                type = "body",
                parameters = new[]
                {
                    new { type = "text", text = referenceNumber }
                }
            }
        }
    }
};
```

---

## Message Types

### Text Messages
```json
{
  "type": "text",
  "text": {
    "body": "Message content"
  }
}
```

### Voice Notes
```json
{
  "type": "audio",
  "audio": {
    "id": "audio_id",
    "mime_type": "audio/ogg; codecs=opus"
  }
}
```

### Images
```json
{
  "type": "image",
  "image": {
    "id": "image_id",
    "mime_type": "image/jpeg",
    "caption": "Optional caption"
  }
}
```

### Location
```json
{
  "type": "location",
  "location": {
    "latitude": -29.8587,
    "longitude": 31.0218,
    "name": "Location name",
    "address": "Address"
  }
}
```

---

## Media Handling

### Download Media Files

```csharp
public async Task<byte[]> DownloadWhatsAppMedia(string mediaId)
{
    var accessToken = _configuration["WHATSAPP_ACCESS_TOKEN"];
    
    // Get media URL
    var urlRequest = $"https://graph.facebook.com/v18.0/{mediaId}";
    var urlResponse = await _httpClient.GetAsync(
        $"{urlRequest}?access_token={accessToken}"
    );
    var urlData = await urlResponse.Content.ReadFromJsonAsync<MediaUrlResponse>();
    
    // Download media
    var mediaRequest = new HttpRequestMessage(HttpMethod.Get, urlData.Url);
    mediaRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
    var mediaResponse = await _httpClient.SendAsync(mediaRequest);
    
    return await mediaResponse.Content.ReadAsByteArrayAsync();
}
```

---

## Testing

### Test with Demo Mode

1. **Enable demo mode:**
   ```bash
   WHATSAPP_DEMO_MODE=true
   ```

2. **Use simulator:**
   - Navigate to `/demo/whatsapp`
   - Enter test message
   - Submit

3. **Verify incident creation:**
   - Check dashboard
   - Verify reference number
   - Check department assignment

### Test with Real WhatsApp

1. **Add test number:**
   - In Meta dashboard, add your phone number
   - Verify with OTP

2. **Send test message:**
   - Open WhatsApp
   - Send message to test number
   - Include incident description

3. **Verify webhook:**
   - Check application logs
   - Verify incident created
   - Check reference number

---

## Production Deployment

### Requirements

1. **Business Verification:**
   - Verify your business with Meta
   - Provide business documents
   - Wait for approval (1-2 weeks)

2. **Phone Number:**
   - Register business phone number
   - Verify ownership
   - Configure display name

3. **Message Templates:**
   - Create message templates
   - Submit for approval
   - Use approved templates only

4. **Rate Limits:**
   - Start with tier 1 (1,000 conversations/day)
   - Request tier increase as needed
   - Monitor usage

### Scaling Considerations

- **Tier 1:** 1,000 unique conversations/24 hours
- **Tier 2:** 10,000 unique conversations/24 hours
- **Tier 3:** 100,000 unique conversations/24 hours
- **Unlimited:** Request from Meta

---

## Monitoring

### Key Metrics

- Message delivery rate
- Response time
- Error rate
- Conversation volume
- Template approval status

### Logging

```csharp
_logger.LogInformation(
    "WhatsApp message received from {From}: {Message}",
    message.From,
    message.Text.Body
);

_logger.LogError(
    "Failed to process WhatsApp message: {Error}",
    ex.Message
);
```

---

## Troubleshooting

### Common Issues

**Issue:** Webhook verification fails
- **Solution:** Check verify token matches
- **Solution:** Ensure HTTPS endpoint is accessible
- **Solution:** Check webhook URL is correct

**Issue:** Messages not received
- **Solution:** Verify webhook subscription
- **Solution:** Check phone number is added to test recipients
- **Solution:** Review Meta dashboard for errors

**Issue:** Cannot send messages
- **Solution:** Verify access token is valid
- **Solution:** Check phone number ID is correct
- **Solution:** Ensure recipient opted in

**Issue:** Media download fails
- **Solution:** Check access token permissions
- **Solution:** Verify media ID is valid
- **Solution:** Check media hasn't expired (30 days)

---

## Security Best Practices

1. **Validate Webhook Signatures:**
   ```csharp
   private bool ValidateSignature(string signature, string payload)
   {
       var appSecret = _configuration["WHATSAPP_APP_SECRET"];
       var hash = ComputeHmacSha256(payload, appSecret);
       return signature == $"sha256={hash}";
   }
   ```

2. **Rate Limiting:**
   - Implement rate limiting on webhook endpoint
   - Prevent abuse and spam

3. **Input Validation:**
   - Sanitize all user input
   - Validate message content
   - Check for malicious content

4. **Token Security:**
   - Store tokens securely
   - Rotate regularly
   - Use environment variables
   - Never log tokens

---

## Cost Considerations

### Pricing (as of 2026)

- **Conversations:** Charged per 24-hour conversation window
- **Business-initiated:** ~$0.05-0.10 per conversation
- **User-initiated:** Free for first 1,000/month
- **Templates:** Free to send

### Cost Optimization

- Use user-initiated conversations when possible
- Batch notifications
- Monitor conversation windows
- Use templates efficiently

---

## Resources

### Documentation
- [WhatsApp Business Platform](https://developers.facebook.com/docs/whatsapp)
- [Cloud API Documentation](https://developers.facebook.com/docs/whatsapp/cloud-api)
- [Webhook Reference](https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks)

### Tools
- [Meta Business Suite](https://business.facebook.com/)
- [WhatsApp Manager](https://business.facebook.com/wa/manage/)
- [API Explorer](https://developers.facebook.com/tools/explorer/)

---

## Support

For issues:
- Check Meta Developer Community
- Review WhatsApp Business API documentation
- Contact Meta Business Support
- Check CivicOps logs and documentation

---

**Document Version:** 1.0  
**Last Updated:** May 15, 2026  
**WhatsApp API Version:** v18.0
