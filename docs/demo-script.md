# CivicOps Demo Script

**Duration**: 3-5 minutes  
**Audience**: Municipal officials, NGOs, smart-city partners, hackathon judges, pilot customers  
**Platform**: CivicOps - Integration-Ready Civic Operations Platform

---

## Opening (30 seconds)

"Good morning/afternoon. I'm here to demonstrate **CivicOps**, an integration-ready civic operations platform that transforms how municipalities handle citizen reporting, incident routing, and public service delivery.

CivicOps addresses a critical challenge: fragmented citizen reporting channels, slow manual routing, and limited public visibility into incident resolution."

---

## Problem Statement (30 seconds)

"Today, municipalities face:
- **Fragmented intake**: Citizens report via phone, email, walk-in, social media
- **Manual routing**: Staff manually classify and assign incidents
- **Limited transparency**: Residents don't know what's happening with their reports
- **Slow response**: Issues get lost or delayed in the system

CivicOps solves this with multi-channel intake, AI-powered classification, and transparent tracking."

---

## Demo Flow (3-4 minutes)

### 1. Citizen Experience (60 seconds)

**Navigate to Landing Page** (`/`)

"Let's start with the citizen experience. This is our landing page - professional, clear value proposition, and multiple ways to engage."

**Click "Report an Issue"** (`/Home/Report`)

"Citizens can report issues through:
- This web form
- Our Android mobile app
- WhatsApp text messages
- Voice notes

Let me submit a report..."

**Fill form**:
- Description: "Large pothole on Main Road near the library, causing traffic issues"
- Category: Road Maintenance
- Suburb: Durban CBD
- Ward: Ward 26

**Submit**

"Notice the confirmation page shows:
- Reference number: **CIV-2026-XXXX**
- Assigned department: **Roads & Stormwater**
- Current status: **New**
- AI-generated summary

The system used either Gemini AI or our deterministic classifier to automatically route this to the correct department."

### 2. Public Tracking (45 seconds)

**Click "Track Your Report"** (`/Home/Lookup`)

"Citizens can track their reports anytime using their reference number."

**Enter reference number from previous step**

"Here's the status page showing:
- Current status and timeline
- Department assignment
- Public updates from staff
- Location and priority information
- What happens next

Notice the **emergency disclaimer** - CivicOps does not replace emergency services."

### 3. Operations Dashboard (60 seconds)

**Navigate to Login** (`/Auth/Login`)

"Now let's see the staff side. This is demo authentication - production would use proper identity management."

**Login as**: `dispatcher@civicops.demo` / `CivicOps2026!`

**Dashboard loads** (`/Home/Dashboard`)

"This is the operations dashboard showing:
- **KPI cards**: Total incidents, new, in progress, escalated, resolved
- **Incidents by department**: Clickable to see department queues
- **Incidents by source**: Web, Android, WhatsApp, voice notes
- **High priority incidents**: Urgent items requiring attention
- **Recent incidents**: Latest submissions
- **System status**: Gemini AI status, active alerts, connector health

Everything is real-time and actionable."

### 4. Department Queue (45 seconds)

**Click on "Roads & Stormwater"** (`/Home/Department?dept=RoadsAndStormwater`)

"Department responders see their queue filtered to their department. Each incident shows:
- Reference number
- Category and summary
- Status and priority
- Location
- Source channel

Let's open one..."

**Click on an incident** (`/Home/Incident?id={id}`)

"The incident detail page shows:
- Complete description and AI summary
- Timeline with all status changes
- Internal notes (staff-only)
- Public updates (visible to residents)
- Status update controls
- Escalation options

Staff can update status, add notes, and communicate with residents."

### 5. Integration Readiness (30 seconds)

**Navigate to Connectors** (`/Home/Connectors`)

"CivicOps is built for integration. We have connector interfaces for:
- **Gemini AI**: Classification and routing (configurable)
- **WhatsApp Cloud API**: Message intake (production-ready structure)
- **Voice transcription**: Audio-to-text (ready for service integration)
- **SMS notifications**: Resident updates (ready for service integration)
- **GIS/mapping**: Location services (ready for service integration)
- **Municipal ERP**: Ticketing system integration (ready for service integration)

Each connector has clear environment variable configuration and documentation."

### 6. Area Alerts (30 seconds)

**Navigate to Alerts** (`/Home/Alerts`)

"Municipalities can publish area-wide alerts for:
- Water outages
- Electricity disruptions
- Road closures
- Flood warnings
- Public safety notices

Residents can filter by their suburb or ward to see relevant alerts."

---

## Closing (30 seconds)

"To summarize, CivicOps provides:

✅ **Multi-channel intake**: Web, mobile, WhatsApp, voice  
✅ **AI-powered routing**: Gemini or deterministic classification  
✅ **Department workflows**: Queues, status updates, notes, escalation  
✅ **Public transparency**: Reference tracking, status updates, timeline  
✅ **Integration-ready**: Clean interfaces for production connectors  
✅ **Role-based access**: Admin, dispatcher, department responder, viewer  

**Current Status**: Pilot-ready demo with 18 sample incidents and 8 area alerts

**Next Steps**: 
- Production database (SQLite/PostgreSQL/SQL Server)
- Production authentication (ASP.NET Core Identity)
- Gemini API key configuration
- WhatsApp Business API setup
- Pilot deployment

**Important Disclaimers**:
- This is a demonstration platform
- Not affiliated with any official municipal government
- Does not replace emergency services (Police: 10111, Fire/EMS: 10177)
- Live integrations require environment variable configuration

Questions?"

---

## Demo Credentials

**Login URL**: `/Auth/Login`

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@civicops.demo | CivicOps2026! |
| Dispatcher | dispatcher@civicops.demo | CivicOps2026! |
| Water Dept | water@civicops.demo | CivicOps2026! |
| Electricity Dept | electricity@civicops.demo | CivicOps2026! |
| Roads Dept | roads@civicops.demo | CivicOps2026! |

---

## Key Talking Points

### For Municipal Officials
- "Reduces manual routing time by automating classification"
- "Provides transparency to residents, reducing follow-up calls"
- "Integrates with existing systems via clean APIs"
- "Role-based access ensures proper security"

### For NGOs
- "Enables community-driven reporting"
- "Tracks issues from report to resolution"
- "Supports multiple languages and channels"
- "Can be deployed on-premises or cloud"

### For Smart-City Partners
- "API-first architecture for integration"
- "Connector interfaces for IoT sensors, GIS, analytics"
- "Real-time data for dashboards and reporting"
- "Scalable from pilot to city-wide deployment"

### For Hackathon Judges
- "Built with ASP.NET Core MVC and .NET 10"
- "Gemini AI integration for intelligent classification"
- "WhatsApp Cloud API ready for messaging"
- "Professional UI/UX with responsive design"
- "Complete RBAC and audit trail"

---

## Technical Details (If Asked)

**Stack**:
- ASP.NET Core MVC (.NET 10)
- Bootstrap 5 + Custom CSS
- JSON persistence (demo) → SQLite/PostgreSQL (production)
- Gemini AI (optional)
- WhatsApp Cloud API (optional)

**Architecture**:
- Repository pattern (IDataService)
- Service layer (Classification, Gemini, Auth)
- MVC controllers (Home, API, Auth, Demo, WhatsApp)
- Razor views with shared layout
- RBAC via custom authorization filter

**Deployment**:
- Azure App Service
- AWS Elastic Beanstalk
- On-premises IIS
- Docker container

**Build**:
```bash
dotnet restore
dotnet build
dotnet run
```

---

## Backup Scenarios

### If Gemini is not configured:
"Gemini AI is optional. The system has a deterministic classifier that works reliably without external dependencies. In production, you can enable Gemini by setting the GEMINI_API_KEY environment variable."

### If WhatsApp is not configured:
"WhatsApp integration is production-ready but requires Meta WhatsApp Business API setup. We have a demo simulator to show the workflow. In production, you configure WHATSAPP_ACCESS_TOKEN and WHATSAPP_PHONE_NUMBER_ID."

### If asked about cost:
"CivicOps is a demonstration platform. For pilot deployment, costs depend on:
- Hosting (Azure/AWS/on-premises)
- Gemini API usage (optional, pay-per-use)
- WhatsApp Business API (optional, Meta pricing)
- Database (SQLite free, PostgreSQL/SQL Server licensing)

We can provide detailed cost estimates based on your expected volume."

### If asked about timeline:
"For pilot deployment:
- Week 1-2: Requirements gathering, environment setup
- Week 3-4: Database migration, authentication setup
- Week 5-6: Connector configuration (Gemini, WhatsApp)
- Week 7-8: Testing, training, go-live

Full production deployment: 2-3 months depending on integration complexity."

---

**Demo Prepared By**: IBM Bob  
**Last Updated**: 2026-05-15  
**Version**: 1.0 (Pilot-Ready)