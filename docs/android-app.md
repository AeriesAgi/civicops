# CivicOps Citizen Android App

CivicOps now includes a real Android project at `mobile/CivicOpsAndroid`.

## Architecture

- Package: `com.civicops.citizen`
- App name: `CivicOps Citizen`
- Type: polished Android WebView shell for the Citizen App/PWA.
- Backend entry: `/app` on the configured CivicOps host.
- Device permissions: network, camera, location and microphone for report media/location/voice-note readiness.
- Secrets: no Gemini API keys, WhatsApp tokens or backend secrets are stored in the Android project.

## Build commands

```bash
cd mobile/CivicOpsAndroid
./gradlew assembleDebug -PcivicopsBaseUrl=https://your-civicops-host.example
./gradlew copyDebugApkToWeb -PcivicopsBaseUrl=https://your-civicops-host.example
```

Outputs:

- `app/build/outputs/apk/debug/app-debug.apk`
- `../../wwwroot/downloads/CivicOpsCitizenCompanion-debug.apk` after `copyDebugApkToWeb`

## CI

`.github/workflows/android-apk.yml` builds the debug APK, copies it into the web downloads path and publishes the APK artifact.

## Demo checklist

1. Open `/citizen-app` and show install options.
2. Open `/app` and show the app shell: login, submit report, My Reports, Track Reference, Area Alerts, followed areas/profile, Copilot and community thread.
3. If the APK artifact exists, click the download button exposed by the install hub.
