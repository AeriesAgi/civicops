# CivicOps downloads

Only the submission APK filename should live here:

- `CivicOpsCitizenCompanion-debug.apk`

Rebuild and replace it from the Android project:

```bash
cd mobile/CivicOpsAndroid
./gradlew clean copyDebugApkToWeb
```

Or run the **Android APK** GitHub Actions workflow and download/copy the artifact back to this path.
The ASP.NET app serves `.apk` files with `application/vnd.android.package-archive` and publishes this file when present.
