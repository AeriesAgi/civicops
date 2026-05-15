# CivicOps Android App Guide

## Overview

The CivicOps Android app provides mobile access to civic reporting and incident tracking. The app source structure is included in `/mobile/CivicOpsAndroid` and is ready for development in Android Studio.

---

## App Features

### Citizen Features
- **Report Submission:** Submit civic issues with photos and location
- **Status Tracking:** Track reports by reference number
- **Area Alerts:** View alerts for your suburb/ward
- **Voice Notes:** Record and submit voice note reports
- **WhatsApp Shortcut:** Quick access to WhatsApp reporting

### Responder Features (Future)
- **Department Queue:** View assigned incidents
- **Incident Management:** Update status and add notes
- **Field Updates:** Update incidents from the field
- **Photo Capture:** Add photos during site visits

---

## Project Structure

```
mobile/CivicOpsAndroid/
├── app/
│   ├── build.gradle                 # App-level build configuration
│   └── src/
│       └── main/
│           ├── AndroidManifest.xml  # App manifest and permissions
│           ├── java/                # Kotlin/Java source code
│           ├── res/                 # Resources (layouts, strings, etc.)
│           └── assets/              # Static assets
├── build.gradle                     # Project-level build configuration
└── settings.gradle                  # Project settings
```

---

## Prerequisites

### Required Software
- **Android Studio:** Arctic Fox (2020.3.1) or later
- **Android SDK:** API Level 24 (Android 7.0) or higher
- **Kotlin:** 1.8.0 or later
- **Gradle:** 8.0 or later

### Development Environment
- **Minimum SDK:** 24 (Android 7.0 Nougat)
- **Target SDK:** 34 (Android 14)
- **Compile SDK:** 34

---

## Setup Instructions

### 1. Open Project in Android Studio

```bash
# Navigate to the Android project
cd /workspaces/civicops/mobile/CivicOpsAndroid

# Open in Android Studio
# File → Open → Select CivicOpsAndroid folder
```

### 2. Sync Gradle Files

Android Studio will automatically prompt to sync Gradle files. If not:
- Click "Sync Project with Gradle Files" in the toolbar
- Or: File → Sync Project with Gradle Files

### 3. Configure API Base URL

Create or update `app/src/main/res/values/config.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <!-- Development -->
    <string name="api_base_url">http://10.0.2.2:5000</string>
    
    <!-- Production -->
    <!-- <string name="api_base_url">https://api.civicops.example.com</string> -->
</resources>
```

**Note:** `10.0.2.2` is the Android emulator's alias for `localhost`

### 4. Build the App

```bash
# Command line build
./gradlew assembleDebug

# Or use Android Studio:
# Build → Make Project (Ctrl+F9)
```

### 5. Run on Emulator or Device

**Emulator:**
- Tools → AVD Manager
- Create Virtual Device (Pixel 5, API 34)
- Run → Run 'app' (Shift+F10)

**Physical Device:**
- Enable Developer Options on device
- Enable USB Debugging
- Connect device via USB
- Run → Run 'app'

---

## App Architecture

### Technology Stack
- **Language:** Kotlin
- **UI:** Material Design 3
- **Networking:** Retrofit 2 + OkHttp
- **Async:** Kotlin Coroutines
- **Architecture:** MVVM (Model-View-ViewModel)

### Key Dependencies

```gradle
dependencies {
    // Core Android
    implementation 'androidx.core:core-ktx:1.12.0'
    implementation 'androidx.appcompat:appcompat:1.6.1'
    implementation 'com.google.android.material:material:1.11.0'
    
    // Networking
    implementation 'com.squareup.retrofit2:retrofit:2.9.0'
    implementation 'com.squareup.retrofit2:converter-gson:2.9.0'
    implementation 'com.squareup.okhttp3:logging-interceptor:4.12.0'
    
    // Coroutines
    implementation 'org.jetbrains.kotlinx:kotlinx-coroutines-android:1.7.3'
}
```

---

## API Integration

### API Service Interface

```kotlin
interface CivicOpsApi {
    @POST("api/reports")
    suspend fun submitReport(@Body report: ReportRequest): ReportResponse
    
    @GET("api/reports/{reference}")
    suspend fun getReport(@Path("reference") reference: String): ReportResponse
    
    @GET("api/alerts")
    suspend fun getAlerts(
        @Query("area") area: String?,
        @Query("ward") ward: String?
    ): AlertsResponse
    
    @GET("api/departments")
    suspend fun getDepartments(): DepartmentsResponse
}
```

### Retrofit Setup

```kotlin
object ApiClient {
    private const val BASE_URL = "http://10.0.2.2:5000/"
    
    private val okHttpClient = OkHttpClient.Builder()
        .addInterceptor(HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BODY
        })
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()
    
    val api: CivicOpsApi by lazy {
        Retrofit.Builder()
            .baseUrl(BASE_URL)
            .client(okHttpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(CivicOpsApi::class.java)
    }
}
```

---

## App Screens

### 1. Main Activity (Home Screen)

**Features:**
- Welcome message
- Quick report button
- Track report button
- View alerts button
- WhatsApp reporting option

**Layout:** `activity_main.xml`

### 2. Report Activity

**Features:**
- Description text field
- Category spinner
- Suburb/ward fields
- Contact information (optional)
- Location notes
- Photo capture/select
- Voice note recording
- Submit button

**Layout:** `activity_report.xml`

**API Call:**
```kotlin
suspend fun submitReport(report: ReportRequest): Result<ReportResponse> {
    return try {
        val response = ApiClient.api.submitReport(report)
        Result.success(response)
    } catch (e: Exception) {
        Result.failure(e)
    }
}
```

### 3. Status Activity

**Features:**
- Reference number input
- Search button
- Status display
- Department information
- Timeline of updates
- Refresh button

**Layout:** `activity_status.xml`

### 4. Alerts Activity

**Features:**
- Alert list
- Filter by suburb/ward
- Severity indicators
- Alert details
- Refresh button

**Layout:** `activity_alerts.xml`

---

## Permissions

### Required Permissions (AndroidManifest.xml)

```xml
<!-- Network access -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />

<!-- Camera for photos -->
<uses-permission android:name="android.permission.CAMERA" />

<!-- Location for incident reporting -->
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />

<!-- Audio recording for voice notes -->
<uses-permission android:name="android.permission.RECORD_AUDIO" />
```

### Runtime Permission Handling

```kotlin
class ReportActivity : AppCompatActivity() {
    private val cameraPermission = 
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted) {
                openCamera()
            } else {
                showPermissionDeniedMessage()
            }
        }
    
    private fun requestCameraPermission() {
        when {
            ContextCompat.checkSelfPermission(
                this, Manifest.permission.CAMERA
            ) == PackageManager.PERMISSION_GRANTED -> {
                openCamera()
            }
            else -> {
                cameraPermission.launch(Manifest.permission.CAMERA)
            }
        }
    }
}
```

---

## Data Models

### Report Request

```kotlin
data class ReportRequest(
    val sourceChannel: String = "Android",
    val description: String,
    val category: String?,
    val suburb: String?,
    val ward: String?,
    val contactName: String?,
    val contactPhone: String?,
    val contactEmail: String?,
    val locationNotes: String?,
    val mediaMetadata: String?,
    val audioMetadata: String?
)
```

### Report Response

```kotlin
data class ReportResponse(
    val success: Boolean,
    val referenceNumber: String,
    val department: String,
    val status: String,
    val priority: String,
    val message: String
)
```

### Alert Model

```kotlin
data class Alert(
    val id: String,
    val type: String,
    val severity: String,
    val title: String,
    val description: String,
    val suburb: String,
    val ward: String,
    val department: String,
    val createdAt: String,
    val expiresAt: String?
)
```

---

## Features Implementation Guide

### Photo Capture

```kotlin
private val takePicture = 
    registerForActivityResult(ActivityResultContracts.TakePicture()) { success ->
        if (success) {
            // Photo saved to photoUri
            displayPhoto(photoUri)
        }
    }

private fun capturePhoto() {
    photoUri = createImageUri()
    takePicture.launch(photoUri)
}

private fun createImageUri(): Uri {
    val image = File(filesDir, "incident_${System.currentTimeMillis()}.jpg")
    return FileProvider.getUriForFile(
        this,
        "${packageName}.fileprovider",
        image
    )
}
```

### Voice Recording

```kotlin
class VoiceRecorder(private val context: Context) {
    private var mediaRecorder: MediaRecorder? = null
    private var outputFile: File? = null
    
    fun startRecording() {
        outputFile = File(context.cacheDir, "voice_${System.currentTimeMillis()}.m4a")
        
        mediaRecorder = MediaRecorder().apply {
            setAudioSource(MediaRecorder.AudioSource.MIC)
            setOutputFormat(MediaRecorder.OutputFormat.MPEG_4)
            setAudioEncoder(MediaRecorder.AudioEncoder.AAC)
            setOutputFile(outputFile?.absolutePath)
            prepare()
            start()
        }
    }
    
    fun stopRecording(): File? {
        mediaRecorder?.apply {
            stop()
            release()
        }
        mediaRecorder = null
        return outputFile
    }
}
```

### Location Services

```kotlin
private val fusedLocationClient: FusedLocationProviderClient by lazy {
    LocationServices.getFusedLocationProviderClient(this)
}

private fun getCurrentLocation() {
    if (ActivityCompat.checkSelfPermission(
            this, Manifest.permission.ACCESS_FINE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED
    ) {
        fusedLocationClient.lastLocation.addOnSuccessListener { location ->
            location?.let {
                val latitude = it.latitude
                val longitude = it.longitude
                // Use coordinates
            }
        }
    }
}
```

---

## Offline Support (Future Enhancement)

### Local Database with Room

```kotlin
@Entity(tableName = "drafts")
data class ReportDraft(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val description: String,
    val category: String?,
    val suburb: String?,
    val ward: String?,
    val timestamp: Long = System.currentTimeMillis()
)

@Dao
interface DraftDao {
    @Query("SELECT * FROM drafts ORDER BY timestamp DESC")
    fun getAllDrafts(): Flow<List<ReportDraft>>
    
    @Insert
    suspend fun insertDraft(draft: ReportDraft)
    
    @Delete
    suspend fun deleteDraft(draft: ReportDraft)
}
```

---

## Testing

### Unit Tests

```kotlin
class ReportViewModelTest {
    @Test
    fun `submit report with valid data returns success`() = runTest {
        val viewModel = ReportViewModel(fakeRepository)
        val report = ReportRequest(
            description = "Test incident",
            suburb = "Chatsworth"
        )
        
        viewModel.submitReport(report)
        
        val result = viewModel.submitResult.value
        assertTrue(result is Result.Success)
    }
}
```

### UI Tests

```kotlin
@Test
fun testReportSubmission() {
    onView(withId(R.id.descriptionEditText))
        .perform(typeText("Test incident"))
    
    onView(withId(R.id.suburbEditText))
        .perform(typeText("Chatsworth"))
    
    onView(withId(R.id.submitButton))
        .perform(click())
    
    onView(withId(R.id.confirmationText))
        .check(matches(isDisplayed()))
}
```

---

## Build Variants

### Debug Build

```gradle
buildTypes {
    debug {
        applicationIdSuffix ".debug"
        debuggable true
        buildConfigField "String", "API_BASE_URL", "\"http://10.0.2.2:5000/\""
    }
}
```

### Release Build

```gradle
buildTypes {
    release {
        minifyEnabled true
        proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'), 'proguard-rules.pro'
        buildConfigField "String", "API_BASE_URL", "\"https://api.civicops.example.com/\""
    }
}
```

---

## Deployment

### Generate Signed APK

1. Build → Generate Signed Bundle/APK
2. Select APK
3. Create or select keystore
4. Select release build variant
5. Build APK

### Google Play Store

1. Create Google Play Console account
2. Create new app
3. Upload APK/AAB
4. Complete store listing
5. Set pricing and distribution
6. Submit for review

---

## Troubleshooting

### Common Issues

**Issue:** Cannot connect to API
- **Solution:** Use `10.0.2.2` for emulator, not `localhost`

**Issue:** SSL certificate errors
- **Solution:** Add network security config for development

**Issue:** Permissions denied
- **Solution:** Check runtime permission handling

**Issue:** Build fails
- **Solution:** Sync Gradle files, invalidate caches

---

## Future Enhancements

### Planned Features
- [ ] Push notifications for status updates
- [ ] Offline draft saving
- [ ] Photo compression before upload
- [ ] Multiple photo support
- [ ] Map view of incidents
- [ ] Dark mode support
- [ ] Multi-language support
- [ ] Biometric authentication
- [ ] Widget for quick reporting

---

## Resources

### Documentation
- [Android Developer Guide](https://developer.android.com/guide)
- [Kotlin Documentation](https://kotlinlang.org/docs/home.html)
- [Material Design](https://material.io/design)
- [Retrofit Documentation](https://square.github.io/retrofit/)

### Sample Code
- Full implementation examples in `/mobile/CivicOpsAndroid/app/src/main/java`
- Layout examples in `/mobile/CivicOpsAndroid/app/src/main/res/layout`

---

## Support

For issues or questions:
- Check the main README.md
- Review API documentation
- Check backend logs
- Test API endpoints with Postman

---

**Document Version:** 1.0  
**Last Updated:** May 15, 2026  
**Android Target:** API 34 (Android 14)
