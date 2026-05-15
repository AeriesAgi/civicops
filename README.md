# CivicOps - Integration-Ready Civic Operations Platform

**Version**: 1.0 (Pilot-Ready)  
**Built with**: IBM Bob AI Agent  
**Framework**: ASP.NET Core MVC (.NET 10)  
**Status**: ✅ Build Passing

---

## Overview

CivicOps is an integration-ready civic operations platform that transforms how municipalities handle citizen reporting, incident routing, and public service delivery. The platform provides multi-channel intake (Web, Android, WhatsApp, voice notes), AI-powered classification, department workflows, and transparent public tracking.

**Key Features**:
- 🌐 Multi-channel citizen reporting (Web, Android, WhatsApp, voice notes)
- 🤖 AI-powered incident classification (Gemini AI + deterministic fallback)
- 🏛️ 13 municipal departments with dedicated workflows
- 📊 Professional operations dashboard with KPI tracking
- 🔐 Role-based access control (Admin, Dispatcher, Department Responder, Viewer)
- 📱 Mobile-responsive design
- 🔌 Integration-ready architecture (WhatsApp, Gemini, SMS, GIS, ERP)
- ⚡ Real-time status tracking with public timeline
- 🚨 Area-based alert system

---

## Important Disclaimers

⚠️ **Demo Platform**: CivicOps is a demonstration platform showcasing integration-ready civic operations technology.

⚠️ **Not Official**: Not affiliated with any official municipal government.

⚠️ **Not Emergency Services**: CivicOps does not replace official emergency services. For emergencies, always contact:
- **Police**: 10111
- **Fire/EMS**: 10177

⚠️ **Demo Authentication**: Current authentication is for demonstration only. Production deployment requires proper identity management.

⚠️ **Live Integrations**: Gemini AI, WhatsApp, and other connectors require environment variable configuration and service setup.

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- Git

### Installation

```bash
# Clone repository
git clone <repository-url>
cd civicops

# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run application
dotnet run
```

### Access Application

- **URL**: http://localhost:5000
- **Landing Page**: `/`
- **Login**: `/Auth/Login`

---

## Demo Credentials

| Role | Email | Password | Access |
|------|-------|----------|--------|
| Admin | admin@civicops.demo | CivicOps2026! | Full system access |
| Dispatcher | dispatcher@civicops.demo | CivicOps2026! | Incident management |
| Water Dept | water@civicops.demo | CivicOps2026! | Water & Sanitation queue |
| Electricity Dept | electricity@civicops.demo | CivicOps2026! | Electricity queue |
| Roads Dept | roads@civicops.demo | CivicOps2026! | Roads & Stormwater queue |

---

## Key Routes

### Public Routes (No Login Required)

- `/` - Market-ready landing page
- `/Home/Report` - Submit incident report
- `/Home/Lookup` - Track report by reference number
- `/Home/Alerts` - View area alerts
- `/Home/Mobile` - Mobile app information
- `/Home/Status?reference={ref}` - Report status page

### Protected Routes (Login Required)

- `/Home/Dashboard` - Operations dashboard with KPIs
- `/Home/Department?dept={dept}` - Department-specific queue
- `/Home/Incident?id={id}` - Incident detail and management
- `/Home/Connectors` - Connector status and configuration

### Authentication Routes

- `/Auth/Login` - Demo login page
- `/Auth/Logout` - Logout (POST)
- `/Auth/AccessDenied` - Access denied page

### API Routes

- `POST /api/reports` - Submit new report
- `GET /api/reports/{reference}` - Get report by reference
- `GET /api/alerts` - Get area alerts
- `GET /api/departments` - List departments
- `GET /api/departments/{dept}/queue` - Get department queue
- `GET /api/incidents/{id}` - Get incident details
- `POST /api/incidents/{id}/status` - Update incident status
- `POST /api/incidents/{id}/note` - Add incident note
- `POST /api/incidents/{id}/escalate` - Escalate incident
- `GET /api/connectors/status` - Get connector status

### Demo Routes

- `/demo/whatsapp` - WhatsApp message simulator
- `/demo/voicenote` - Voice note simulator

---

## Environment Variables

### Gemini AI (Optional)

```bash
GEMINI_API_KEY=your_api_key_here
GEMINI_MODEL=gemini-1.5-flash
GEMINI_ENABLED=false
```

### WhatsApp Cloud API (Optional)

```bash
WHATSAPP_VERIFY_TOKEN=your_verify_token
WHATSAPP_ACCESS_TOKEN=your_access_token
WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id
```

**Note**: Without these environment variables, the system uses demo mode with deterministic classification and simulated WhatsApp.

---

## Project Structure

```
CivicOps/
├── Controllers/
│   ├── ApiController.cs          # REST API endpoints
│   ├── AuthController.cs         # Demo authentication
│   ├── DemoController.cs         # Demo simulators
│   ├── HomeController.cs         # Main application routes
│   └── WhatsAppController.cs     # WhatsApp webhook
├── Models/
│   ├── Alert.cs                  # Area alert model
│   ├── Department.cs             # Department enum (13 departments)
│   ├── DemoUser.cs               # Demo auth user model
│   ├── Incident.cs               # Core incident model
│   ├── IncidentStatusHistory.cs  # Status change tracking
│   ├── MediaAttachment.cs        # Media metadata
│   └── PublicUpdate.cs           # Public update model
├── Services/
│   ├── DemoAuthService.cs        # Demo authentication
│   ├── DeterministicClassificationService.cs # Fallback classifier
│   ├── GeminiService.cs          # Gemini AI integration
│   ├── JsonDataService.cs        # JSON persistence
│   └── Interfaces/               # Service interfaces
├── Filters/
│   └── DemoAuthorizationFilter.cs # RBAC filter
├── Views/
│   ├── Auth/                     # Login, access denied
│   ├── Home/                     # All application views
│   └── Shared/                   # Layout, error
├── wwwroot/
│   ├── css/
│   │   ├── civicops-theme.css    # Professional theme
│   │   └── site.css              # Additional styles
│   └── lib/                      # Bootstrap, jQuery
├── Data/                         # JSON persistence
├── docs/                         # Documentation
└── mobile/CivicOpsAndroid/       # Android app structure
```

---

## Features

### 1. Multi-Channel Intake

- **Web Form**: Professional incident reporting form
- **Android App**: Mobile app structure (backend APIs ready)
- **WhatsApp**: Text message intake with webhook support
- **Voice Notes**: Audio metadata support (transcription-ready)

### 2. AI-Powered Classification

- **Gemini AI**: Optional AI classification and routing
- **Deterministic Fallback**: Rule-based classifier (always works)
- **Automatic Routing**: Assigns incidents to correct department
- **Priority Assignment**: Sets urgency level automatically

### 3. Department Workflows

**13 Departments**:
- Water & Sanitation
- Electricity
- Roads & Stormwater
- Waste Management
- Parks & Public Spaces
- Housing/Informal Settlements
- Environmental Health
- Disaster Management
- Fire & Rescue
- Metro Police/Public Safety
- SAPS Liaison/Police Referral
- EMS/Medical Referral
- Ward Councillor/Ward Committee

**Workflow Features**:
- Department-specific queues
- Status updates (New, Triaged, Assigned, InProgress, Escalated, Resolved, Closed)
- Internal notes (staff-only)
- Public updates (resident-visible)
- Escalation support
- Timeline/audit trail

### 4. Operations Dashboard

- **KPI Cards**: Total, New, InProgress, Escalated, Resolved incidents
- **Department Breakdown**: Incidents by department (clickable)
- **Source Channel Breakdown**: Web, Android, WhatsApp, Voice, Demo
- **High Priority List**: Urgent incidents requiring attention
- **Recent Incidents**: Latest submissions
- **System Status**: Gemini AI, alerts, connectors

### 5. Public Transparency

- **Reference Tracking**: Unique reference numbers (CIV-2026-XXXX)
- **Status Page**: Current status, timeline, public updates
- **Area Alerts**: Suburb/ward-based alerts
- **Emergency Disclaimer**: Prominent on all pages

### 6. Role-Based Access Control

**Roles**:
- **Admin**: Full system access, user management, configuration
- **Dispatcher**: Incident management, status updates, escalation
- **Department Responder**: Department-specific queue, incident updates
- **Viewer**: Read-only access to incidents and reports

### 7. Integration Readiness

**Connector Interfaces**:
- Gemini AI (classification, summarization)
- WhatsApp Cloud API (message intake)
- Voice Transcription (audio-to-text)
- SMS Notifications (resident updates)
- Email Notifications (staff alerts)
- GIS/Geocoding (location mapping)
- Municipal ERP (ticketing integration)
- Media Storage (file uploads)
- Authentication (identity providers)
- Audit Logging (compliance)

---

## Demo Data

### Incidents (18 total)

- Burst water pipe (High priority, InProgress)
- Power outage (High priority, Assigned)
- Pothole (Medium priority, New)
- Illegal dumping (Medium priority, Triaged)
- Blocked storm drain (Medium priority, New)
- Fire hazard (Urgent priority, Escalated)
- Sewage leak (High priority, InProgress)
- Broken playground equipment (Medium priority, Assigned)
- Refuse not collected (Medium priority, Resolved)
- Public safety concern (High priority, InProgress)
- Street light not working (Low priority, Assigned)
- Missing manhole cover (Urgent priority, Escalated)
- Low water pressure (Medium priority, InProgress)
- Fallen tree (High priority, Resolved)
- Graffiti (Low priority, New)
- Stray dogs (Medium priority, Triaged)
- Noise complaint (Low priority, Assigned)
- Flooding (Urgent priority, Escalated)

### Area Alerts (8 total)

- Planned water maintenance (Warning)
- Load shedding (Urgent)
- Road closure (Warning)
- Waste collection delay (Info)
- Increased patrols (Warning)
- Flood warning (Urgent)
- Air quality advisory (Warning)
- Severe weather warning (Critical)

---

## Documentation

- **docs/build-log.md** - Complete build documentation, routes, features
- **docs/bob-report.md** - Comprehensive transformation report
- **docs/demo-script.md** - 3-5 minute demo presentation script
- **docs/integration-readiness.md** - Connector setup guides
- **docs/whatsapp-setup.md** - WhatsApp Business API setup
- **docs/gemini-setup.md** - Gemini AI configuration
- **docs/android-app.md** - Android app documentation

---

## Production Deployment

### Database Migration

**Current**: JSON files in `Data/` directory  
**Production**: SQLite, PostgreSQL, or SQL Server

**Steps**:
1. Install Entity Framework Core
2. Create DbContext and migrations
3. Update IDataService implementation
4. Configure connection string
5. Run migrations

### Authentication

**Current**: Demo authentication with session-based auth  
**Production**: ASP.NET Core Identity or external provider

**Options**:
- ASP.NET Core Identity (built-in)
- Azure Active Directory
- Auth0
- Okta
- Custom OAuth2/OIDC provider

### Gemini AI Setup

1. Obtain Gemini API key from Google AI Studio
2. Set environment variable: `GEMINI_API_KEY=your_key`
3. Set `GEMINI_ENABLED=true`
4. Configure model: `GEMINI_MODEL=gemini-1.5-flash`

### WhatsApp Setup

1. Create Meta Business Account
2. Set up WhatsApp Business API
3. Configure webhook URL
4. Set environment variables:
   - `WHATSAPP_VERIFY_TOKEN`
   - `WHATSAPP_ACCESS_TOKEN`
   - `WHATSAPP_PHONE_NUMBER_ID`

### Deployment Options

- **Azure App Service**: Recommended for .NET applications
- **AWS Elastic Beanstalk**: Cross-platform deployment
- **Docker**: Containerized deployment
- **On-Premises IIS**: Windows Server deployment

---

## Testing

### Manual Testing

```bash
# Run application
dotnet run

# Test public routes
curl http://localhost:5000/
curl http://localhost:5000/Home/Alerts

# Test API endpoints
curl -X POST http://localhost:5000/api/reports \
  -H "Content-Type: application/json" \
  -d '{"description":"Test incident","category":"Other"}'
```

### Automated Testing (Future)

- Unit tests for services
- Integration tests for controllers
- E2E tests for workflows

---

## Security Considerations

### Current (Demo Mode)

- ⚠️ Simple session-based authentication
- ⚠️ No password hashing (plain text for demo)
- ⚠️ No HTTPS enforcement
- ⚠️ No rate limiting
- ⚠️ No input sanitization beyond basic validation

### Production Requirements

- ✅ ASP.NET Core Identity with password hashing
- ✅ HTTPS enforcement
- ✅ CORS policies
- ✅ Rate limiting
- ✅ Input validation and sanitization
- ✅ SQL injection prevention (parameterized queries)
- ✅ XSS prevention (Razor encoding)
- ✅ CSRF protection (anti-forgery tokens)
- ✅ Audit logging
- ✅ Secrets management (Azure Key Vault, AWS Secrets Manager)

---

## Contributing

This is a demonstration platform built with IBM Bob AI Agent. For production deployment or customization:

1. Review docs/build-log.md for architecture details
2. Review docs/bob-report.md for transformation notes
3. Follow production deployment guide above
4. Implement security hardening
5. Add comprehensive testing
6. Configure monitoring and logging

---

## License

[Specify license here]

---

## Support

For questions or issues:
- Review documentation in `docs/` directory
- Check build log: `docs/build-log.md`
- Review Bob report: `docs/bob-report.md`

---

## Acknowledgments

**Built with**: IBM Bob AI Agent  
**Framework**: ASP.NET Core MVC (.NET 10)  
**UI**: Bootstrap 5 + Custom CSS  
**AI**: Google Gemini (optional)  
**Messaging**: WhatsApp Cloud API (optional)

---

**Last Updated**: 2026-05-15  
**Build Status**: ✅ SUCCESS (12.1s)  
**Version**: 1.0 (Pilot-Ready)