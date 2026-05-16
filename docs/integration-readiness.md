# CivicOps Integration Readiness Guide

## Overview

CivicOps is architected as an integration-ready platform with clear interfaces for external services. Each connector has a demo implementation for local development and a documented production path.

---

## Connector Architecture

### Design Principles

1. **Interface-Based Design:** Each connector has a clear interface
2. **Demo Mode:** All connectors work locally without external dependencies
3. **Environment Variables:** No hardcoded secrets or credentials
4. **Graceful Degradation:** System works even if connectors fail
5. **Status Monitoring:** Real-time connector health visibility

### Connector States

- **Configured:** Live integration with valid credentials
- **Demo:** Local simulation for development/testing
- **Disabled:** Intentionally turned off
- **Future Connector:** Placeholder for planned integration

---

## 1. Gemini AI Connector

### Status
✅ **Implemented and Ready**

### Purpose
AI-powered incident classification, routing, and summarization with automatic fallback to deterministic logic.

### Configuration

**Environment Variables:**
```bash
GEMINI_API_KEY=your_gemini_api_key_here
GEMINI_MODEL=gemini-2.0-flash-exp
GEMINI_ENABLED=true
```

### Implementation Details

**Service:** `Services/GeminiService.cs`

**Features:**
- Incident classification from natural language
- Department routing recommendations
- Priority assignment
- Summary generation
- Automatic fallback to deterministic classification

**API Endpoint:**
```
POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
```

### Demo Mode
When `GEMINI_ENABLED=false` or `GEMINI_API_KEY` is not set, the system automatically uses deterministic keyword-based classification.

### Production Setup

1. **Get API Key:**
   - Visit Google AI Studio: https://makersuite.google.com/app/apikey
   - Create new API key
   - Copy key to environment variables

2. **Configure Model:**
   - Default: `gemini-2.0-flash-exp`
   - Alternative: `gemini-1.5-pro`, `gemini-1.5-flash`

3. **Enable Service:**
   ```bash
   export GEMINI_API_KEY="your_key_here"
   export GEMINI_ENABLED=true
   ```

4. **Monitor Usage:**
   - Check API quotas in Google Cloud Console
   - Monitor response times
   - Track fallback frequency

### Error Handling
- API timeout → Fallback to deterministic
- Invalid response → Fallback to deterministic
- Rate limit exceeded → Fallback to deterministic
- Network error → Fallback to deterministic

---

## 2. WhatsApp Cloud API Connector

### Status
✅ **Webhook Ready, Requires Meta App Setup**

### Purpose
Enable residents to report civic issues via WhatsApp text messages and voice notes.

### Configuration

**Environment Variables:**
```bash
WHATSAPP_VERIFY_TOKEN=your_custom_verify_token
WHATSAPP_ACCESS_TOKEN=your_whatsapp_access_token
WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id
WHATSAPP_DEMO_MODE=true
```

### Implementation Details

**Controller:** `Controllers/WhatsAppController.cs`

**Endpoints:**
- `GET /webhooks/whatsapp` - Webhook verification
- `POST /webhooks/whatsapp` - Inbound message processing

**Demo Endpoint:**
- `POST /demo/whatsapp/inbound` - Simulate WhatsApp messages

### Production Setup

1. **Create Meta Business App:**
   - Visit https://developers.facebook.com/
   - Create new app
   - Add WhatsApp product

2. **Configure Webhook:**
   - Webhook URL: `https://yourdomain.com/webhooks/whatsapp`
   - Verify token: Set custom token
   - Subscribe to: `messages`

3. **Get Credentials:**
   - Access token from Meta dashboard
   - Phone number ID from WhatsApp settings

4. **Set Environment Variables:**
   ```bash
   export WHATSAPP_VERIFY_TOKEN="your_verify_token"
   export WHATSAPP_ACCESS_TOKEN="your_access_token"
   export WHATSAPP_PHONE_NUMBER_ID="your_phone_id"
   export WHATSAPP_DEMO_MODE=false
   ```

5. **Test Integration:**
   - Send test message to WhatsApp number
   - Verify webhook receives message
   - Check incident creation

### Message Flow

1. Resident sends WhatsApp message
2. Meta forwards to webhook
3. CivicOps processes message
4. Incident created with reference number
5. Reply sent to resident (optional)

### Supported Message Types
- ✅ Text messages
- ✅ Voice notes (metadata stored, transcription needed)
- ✅ Images (metadata stored)
- ⚠️ Location (future enhancement)
- ⚠️ Documents (future enhancement)

---

## 3. Voice Transcription Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Convert audio recordings to text for voice note reporting.

### Recommended Services
- Google Cloud Speech-to-Text
- Azure Speech Services
- AWS Transcribe
- AssemblyAI

### Configuration (Example)

**Environment Variables:**
```bash
VOICE_TRANSCRIPTION_SERVICE=google
VOICE_API_KEY=your_api_key
VOICE_LANGUAGE=en-ZA
```

### Implementation Plan

1. **Create Interface:**
   ```csharp
   public interface IVoiceTranscriptionService
   {
       Task<string> TranscribeAsync(byte[] audioData, string mimeType);
   }
   ```

2. **Implement Service:**
   - Upload audio to transcription service
   - Wait for transcription result
   - Return text

3. **Integrate with Incident Creation:**
   - Receive audio file
   - Transcribe to text
   - Process as normal text report

### Demo Mode
Currently accepts transcript text directly. In production, would transcribe audio automatically.

---

## 4. SMS Notifications Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Send SMS notifications to residents about incident status updates.

### Recommended Services
- Twilio
- AWS SNS
- Azure Communication Services
- Africa's Talking (for South Africa)

### Configuration (Example)

**Environment Variables:**
```bash
SMS_SERVICE=twilio
SMS_ACCOUNT_SID=your_account_sid
SMS_AUTH_TOKEN=your_auth_token
SMS_FROM_NUMBER=+27123456789
```

### Implementation Plan

1. **Create Interface:**
   ```csharp
   public interface ISMSService
   {
       Task SendAsync(string toNumber, string message);
   }
   ```

2. **Implement Service:**
   - Format message with reference number
   - Call SMS API
   - Log delivery status

3. **Trigger Points:**
   - Incident created → Send reference number
   - Status updated → Send update
   - Incident resolved → Send confirmation

---

## 5. Email Notifications Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Send email notifications to residents and department staff.

### Configuration (Example)

**Environment Variables:**
```bash
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=noreply@civicops.example.com
SMTP_PASSWORD=your_app_password
SMTP_FROM_NAME=CivicOps
```

### Implementation Plan

1. **Use MailKit or System.Net.Mail**

2. **Email Templates:**
   - Incident confirmation
   - Status update
   - Resolution notification
   - Department assignment

3. **Trigger Points:**
   - Same as SMS notifications
   - Plus: Daily digest for departments

---

## 6. GIS/Geocoding Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Convert addresses to coordinates, display incidents on map, calculate distances.

### Recommended Services
- Google Maps API
- Mapbox
- OpenStreetMap (free)
- Azure Maps

### Configuration (Example)

**Environment Variables:**
```bash
GIS_SERVICE=google
GIS_API_KEY=your_api_key
```

### Implementation Plan

1. **Create Interface:**
   ```csharp
   public interface IGeocodingService
   {
       Task<Coordinates> GeocodeAsync(string address);
       Task<string> ReverseGeocodeAsync(double lat, double lon);
   }
   ```

2. **Features:**
   - Geocode incident locations
   - Display incidents on map
   - Calculate response distances
   - Area-based filtering

---

## 7. Municipal ERP/Ticketing Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Integrate with existing municipal systems for ticket synchronization.

### Common Systems
- SAP
- Oracle ERP
- Microsoft Dynamics
- Custom municipal systems

### Configuration (Example)

**Environment Variables:**
```bash
ERP_API_URL=https://erp.municipality.gov.za/api
ERP_API_KEY=your_api_key
ERP_SYNC_ENABLED=true
```

### Implementation Plan

1. **Create Interface:**
   ```csharp
   public interface IERPService
   {
       Task<string> CreateTicketAsync(Incident incident);
       Task UpdateTicketAsync(string ticketId, IncidentStatus status);
       Task<TicketStatus> GetTicketStatusAsync(string ticketId);
   }
   ```

2. **Sync Strategy:**
   - CivicOps creates incident
   - Sync to ERP system
   - Store ERP ticket ID
   - Bidirectional status updates

---

## 8. Authentication & Authorization

### Status
🔄 **Implementation Pending**

### Purpose
Secure access to admin and department features.

### Recommended Approach
ASP.NET Core Identity with role-based access control

### Configuration (Example)

**Environment Variables:**
```bash
AUTH_ENABLED=true
JWT_SECRET=your_jwt_secret
JWT_EXPIRY_HOURS=24
```

### Implementation Plan

1. **Add ASP.NET Core Identity:**
   ```bash
   dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
   ```

2. **Define Roles:**
   - Public (anonymous)
   - Resident (registered user)
   - Department Staff
   - Department Manager
   - System Admin

3. **Secure Endpoints:**
   - Public: Report submission, status lookup, alerts
   - Department: Queue access, incident updates
   - Admin: All features, user management

---

## 9. File Storage Connector

### Status
🔄 **Interface Ready, Implementation Pending**

### Purpose
Store photos, voice notes, and documents attached to incidents.

### Recommended Services
- Azure Blob Storage
- AWS S3
- Google Cloud Storage
- Local file system (development)

### Configuration (Example)

**Environment Variables:**
```bash
STORAGE_SERVICE=azure
STORAGE_CONNECTION_STRING=your_connection_string
STORAGE_CONTAINER=incident-media
```

### Implementation Plan

1. **Create Interface:**
   ```csharp
   public interface IFileStorageService
   {
       Task<string> UploadAsync(Stream file, string fileName);
       Task<Stream> DownloadAsync(string fileId);
       Task DeleteAsync(string fileId);
   }
   ```

2. **Features:**
   - Upload photos from web/mobile
   - Store voice notes
   - Generate secure URLs
   - Automatic cleanup

---

## 10. Audit Logging

### Status
🔄 **Implementation Pending**

### Purpose
Track all system actions for compliance and debugging.

### Configuration (Example)

**Environment Variables:**
```bash
AUDIT_ENABLED=true
AUDIT_STORAGE=database
AUDIT_RETENTION_DAYS=365
```

### Implementation Plan

1. **Create Audit Log Model:**
   ```csharp
   public class AuditLog
   {
       public string Id { get; set; }
       public DateTime Timestamp { get; set; }
       public string UserId { get; set; }
       public string Action { get; set; }
       public string EntityType { get; set; }
       public string EntityId { get; set; }
       public string Changes { get; set; }
   }
   ```

2. **Log Events:**
   - Incident created
   - Status changed
   - Note added
   - User login
   - Configuration changed

---

## Database Migration

### Current: JSON Files
**Location:** `/Data/*.json`

**Pros:**
- Zero configuration
- Easy demo deployment
- Human-readable

**Cons:**
- Not scalable
- No transactions
- Limited querying

### Production: SQL Database

**Recommended:** PostgreSQL or SQL Server

**Migration Steps:**

1. **Add Entity Framework Core:**
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Tools
   ```

2. **Create DbContext:**
   ```csharp
   public class CivicOpsDbContext : DbContext
   {
       public DbSet<Incident> Incidents { get; set; }
       public DbSet<Alert> Alerts { get; set; }
   }
   ```

3. **Update Services:**
   - Replace `JsonDataService` with `DatabaseDataService`
   - Implement `IDataService` with EF Core

4. **Create Migrations:**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

---

## Monitoring & Observability

### Recommended Tools
- Application Insights (Azure)
- CloudWatch (AWS)
- Stackdriver (Google Cloud)
- Sentry (error tracking)

### Key Metrics
- Incident creation rate
- Average response time
- Department workload
- API response times
- Connector health
- Error rates

---

## Production Deployment Checklist

### Infrastructure
- [ ] Choose hosting platform (Azure, AWS, Google Cloud)
- [ ] Set up production database
- [ ] Configure SSL/TLS certificates
- [ ] Set up CDN for static assets
- [ ] Configure backup strategy

### Security
- [ ] Enable authentication
- [ ] Implement rate limiting
- [ ] Set up WAF (Web Application Firewall)
- [ ] Configure CORS properly
- [ ] Enable security headers
- [ ] Set up secrets management

### Integrations
- [ ] Configure Gemini API
- [ ] Set up WhatsApp Business API
- [ ] Enable SMS service
- [ ] Configure email service
- [ ] Set up file storage
- [ ] Enable audit logging

### Monitoring
- [ ] Set up application monitoring
- [ ] Configure error tracking
- [ ] Enable performance monitoring
- [ ] Set up alerts
- [ ] Create dashboards

### Documentation
- [ ] Update API documentation
- [ ] Create deployment guide
- [ ] Document runbooks
- [ ] Create user guides
- [ ] Document disaster recovery

---

## Cost Estimates (Monthly)

### Small Municipality (1,000 incidents/month)
- Hosting: $50-100
- Database: $20-50
- Gemini API: $10-30
- WhatsApp: $5-20
- SMS: $50-100
- Storage: $5-10
- **Total: ~$140-310/month**

### Medium Municipality (10,000 incidents/month)
- Hosting: $200-400
- Database: $100-200
- Gemini API: $100-300
- WhatsApp: $50-200
- SMS: $500-1000
- Storage: $20-50
- **Total: ~$970-2,150/month**

---

## Support & Maintenance

### Recommended Team
- 1 Backend Developer
- 1 Frontend Developer
- 1 DevOps Engineer
- 1 Support Specialist

### Maintenance Tasks
- Monitor system health
- Update dependencies
- Review security patches
- Optimize performance
- Add new features
- User support

---

## Conclusion

CivicOps is architected for seamless integration with external services. Each connector has a clear path from demo to production, with proper error handling, monitoring, and documentation.

The modular design allows incremental integration - start with core features and add connectors as needed.

---

**Document Version:** 1.0  
**Last Updated:** May 15, 2026  
**Status:** Production Ready Architecture

## 2026 Final Connector Readiness Pass

This repository preserves the IBM Bob hackathon implementation and evidence docs. A final Codex cleanup pass added/verified:

- Server-side Gemini configuration using `GEMINI_API_KEY`, `GEMINI_ENABLED`, `GEMINI_MODEL=gemini-2.5-flash`, and `GEMINI_MODE=Hybrid`.
- Safe deterministic fallback whenever Gemini is disabled, missing credentials, fails, or returns an unparseable response.
- `GET /api/connectors/gemini/test` for a live Gemini readiness check without exposing keys to the frontend.
- WhatsApp Cloud API configuration using `WHATSAPP_ENABLED`, `WHATSAPP_DEMO_MODE`, `WHATSAPP_VERIFY_TOKEN`, `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PHONE_NUMBER_ID`, `WHATSAPP_GRAPH_VERSION=v22.0`, and `WHATSAPP_PUBLIC_BASE_URL`.
- `GET /webhooks/whatsapp` verification that returns `hub.challenge` only when the configured verify token matches.
- `POST /webhooks/whatsapp` parsing for inbound WhatsApp Cloud API text messages.
- Outbound WhatsApp replies guarded so live send only occurs when enabled, non-demo, and fully credentialed.
- A shared intake pipeline for web, WhatsApp demo, real WhatsApp webhook, voice-note transcript, and mobile/API reports.

No official municipal partnership is claimed. Live Gemini and WhatsApp operation require deployment-time environment variables. CivicOps is not an emergency service replacement.
