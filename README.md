# CivicOps

**Integration-Ready Civic Operations Platform for Municipal Incident Intake, Routing, and Public Tracking**

CivicOps is a comprehensive civic operations platform that enables residents to report municipal issues through multiple channels (web, mobile, WhatsApp, voice notes) and provides municipal departments with tools to manage, route, and resolve incidents efficiently.

## 🚀 Features

### Multi-Channel Reporting
- **Web/PWA**: Mobile-friendly web interface for incident reporting
- **Android App**: Native mobile app (source in `/mobile/CivicOpsAndroid`)
- **WhatsApp Integration**: Text-based reporting via WhatsApp Cloud API
- **Voice Notes**: Audio-based reporting with transcription support

### AI-Powered Classification
- **Gemini Integration**: Optional AI-powered incident classification and routing
- **Deterministic Fallback**: Keyword-based classification when AI is unavailable
- **Smart Routing**: Automatic department assignment based on incident type

### Municipal Departments
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

### Public Features
- Reference number tracking (e.g., CIV-2026-0001)
- Status lookup by reference number
- Area-based alerts (suburb/ward filtering)
- Public status updates
- Emergency disclaimers

### Department Features
- Department-specific incident queues
- Status management workflow
- Internal notes and public updates
- Incident escalation
- Priority assignment
- Full incident history

### Integration Readiness
- WhatsApp Cloud API connector
- Gemini AI connector
- Voice transcription connector (placeholder)
- SMS notifications connector (placeholder)
- Email notifications connector (placeholder)
- GIS/mapping connector (placeholder)
- Municipal ERP connector (placeholder)

## 🛠️ Technology Stack

- **Backend**: ASP.NET Core 8 MVC
- **Data**: JSON file persistence (easily replaceable with database)
- **Frontend**: Razor Pages with embedded CSS
- **Mobile**: Android (Kotlin)
- **AI**: Google Gemini API (optional)
- **Integrations**: WhatsApp Cloud API, REST APIs

## 📋 Prerequisites

- .NET 8.0 SDK or later
- For Android development: Android Studio with SDK 24+

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone <repository-url>
cd civicops
```

### 2. Run the Application

```bash
dotnet restore
dotnet build
dotnet run
```

The application will start at `https://localhost:5001` (or `http://localhost:5000`)

### 3. Access the Application

- **Home**: https://localhost:5001
- **Report Issue**: https://localhost:5001/Home/Report
- **Track Report**: https://localhost:5001/Home/Lookup
- **Dashboard**: https://localhost:5001/Home/Dashboard
- **Alerts**: https://localhost:5001/Home/Alerts

## 🔧 Configuration

### Environment Variables

Create a `.env` file or set environment variables:

```bash
# Gemini AI (Optional)
GEMINI_API_KEY=your_gemini_api_key_here
GEMINI_MODEL=gemini-2.0-flash-exp
GEMINI_ENABLED=true

# WhatsApp Cloud API (Optional)
WHATSAPP_VERIFY_TOKEN=your_verify_token
WHATSAPP_ACCESS_TOKEN=your_access_token
WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id
WHATSAPP_DEMO_MODE=true
```

**Important**: Never commit API keys or secrets to the repository. Use environment variables or secure configuration management.

### Demo Mode

The application works fully in demo mode without any external services:
- Deterministic classification is used when Gemini is not configured
- WhatsApp simulator available at `/demo/whatsapp`
- Voice note simulator available at `/demo/voicenote`

## 📱 Android App

The Android app source is located in `/mobile/CivicOpsAndroid`.

### Building the Android App

1. Open the project in Android Studio
2. Sync Gradle files
3. Update the API base URL in the app configuration
4. Build and run on device or emulator

See [docs/android-app.md](docs/android-app.md) for detailed instructions.

## 🔌 API Endpoints

### Public APIs

- `POST /api/reports` - Submit a new report
- `GET /api/reports/{reference}` - Get report by reference number
- `GET /api/alerts` - Get active alerts (with optional area/ward filters)
- `GET /api/departments` - List all departments

### Department APIs

- `GET /api/departments/{dept}/queue` - Get department incident queue
- `GET /api/incidents/{id}` - Get incident details
- `POST /api/incidents/{id}/status` - Update incident status
- `POST /api/incidents/{id}/note` - Add note to incident
- `POST /api/incidents/{id}/escalate` - Escalate incident

### Webhook APIs

- `GET /webhooks/whatsapp` - WhatsApp webhook verification
- `POST /webhooks/whatsapp` - WhatsApp inbound messages

### Demo APIs

- `POST /demo/whatsapp/inbound` - Simulate WhatsApp message
- `POST /demo/voicenote/submit` - Simulate voice note submission

## 📊 Demo Data

The application includes demo data with:
- 10 sample incidents across different departments
- 5 area alerts
- Realistic Durban/eThekwini-style suburbs and wards

Demo data is automatically seeded on first run.

## 🔒 Security & Disclaimers

### Emergency Disclaimer
CivicOps is for **non-emergency civic issues only**. The application displays clear disclaimers that users in immediate danger must contact official emergency services:
- Police: 10111
- Fire/EMS: 10177

### Security Best Practices
- Never commit secrets or API keys
- Use environment variables for all credentials
- Implement authentication/authorization for production
- Enable HTTPS in production
- Validate and sanitize all user inputs
- Implement rate limiting for APIs

### Municipal Partnership
CivicOps is not affiliated with any official municipal government. It is a demonstration platform for civic operations management.

## 📚 Documentation

- [docs/bob-report.md](docs/bob-report.md) - IBM Bob hackathon report
- [docs/build-log.md](docs/build-log.md) - Detailed build log
- [docs/demo-script.md](docs/demo-script.md) - 3-5 minute demo script
- [docs/integration-readiness.md](docs/integration-readiness.md) - Connector details
- [docs/android-app.md](docs/android-app.md) - Android app guide
- [docs/whatsapp-setup.md](docs/whatsapp-setup.md) - WhatsApp integration
- [docs/gemini-setup.md](docs/gemini-setup.md) - Gemini AI configuration
- [docs/ai-agent-submission-notes.md](docs/ai-agent-submission-notes.md) - AI agent framing

## 🎯 Key Routes

- `/` - Landing page
- `/Home/Report` - Submit incident report
- `/Home/Lookup` - Track report by reference number
- `/Home/Alerts` - View area alerts
- `/Home/Dashboard` - Admin dashboard
- `/Home/Department?dept={department}` - Department queue
- `/Home/Incident?id={id}` - Incident details
- `/Home/Connectors` - Connector status
- `/Home/Mobile` - Mobile/PWA information
- `/demo/whatsapp` - WhatsApp simulator
- `/demo/voicenote` - Voice note simulator

## 🏗️ Project Structure

```
civicops/
├── Controllers/          # MVC Controllers
│   ├── HomeController.cs
│   ├── ApiController.cs
│   ├── WhatsAppController.cs
│   └── DemoController.cs
├── Models/              # Data models
│   ├── Incident.cs
│   ├── Alert.cs
│   └── Department.cs
├── Services/            # Business logic
│   ├── JsonDataService.cs
│   ├── DeterministicClassificationService.cs
│   └── GeminiService.cs
├── Views/               # Razor views
│   ├── Home/
│   ├── Demo/
│   └── Shared/
├── Data/                # JSON data files (gitignored)
├── mobile/              # Mobile apps
│   └── CivicOpsAndroid/
├── docs/                # Documentation
└── wwwroot/             # Static files

```

## 🤝 Contributing

This is a hackathon project built with IBM Bob. For production deployment:
1. Replace JSON persistence with a proper database
2. Implement authentication and authorization
3. Set up proper logging and monitoring
4. Configure production-grade hosting
5. Enable all connector integrations
6. Implement comprehensive testing

## 📄 License

This project is a demonstration/hackathon prototype. See license file for details.

## 🙏 Acknowledgments

- Built with IBM Bob for the IBM Bob Hackathon
- Designed for AI Agent hackathon submission
- Inspired by real-world municipal operations needs

## 📞 Support

For questions or issues, please refer to the documentation in the `/docs` folder.

---

**Built with IBM Bob** | **Integration-Ready Municipal Operations System**
