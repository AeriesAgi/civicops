@echo off
where gradle >nul 2>nul
if %errorlevel%==0 (
  gradle -p "%~dp0" %*
  exit /b %errorlevel%
)
echo CivicOps Android wrapper fallback: install Gradle 8.14.4 or use the GitHub Actions APK workflow.
exit /b 1
