package com.civicops.citizen;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.graphics.Color;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.webkit.GeolocationPermissions;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import java.util.HashSet;
import java.util.Set;

public class MainActivity extends Activity {
    private static final int PERMISSIONS_REQUEST = 42;
    private static final Set<String> ALLOWED_WEBVIEW_RESOURCES = new HashSet<String>() {{
        add(PermissionRequest.RESOURCE_AUDIO_CAPTURE);
        add(PermissionRequest.RESOURCE_VIDEO_CAPTURE);
    }};
    private WebView webView;
    private ProgressBar progressBar;
    private String allowedHost;

    @SuppressLint("SetJavaScriptEnabled")
    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setStatusBarColor(Color.rgb(2, 7, 13));
        getWindow().setNavigationBarColor(Color.rgb(2, 7, 13));
        allowedHost = Uri.parse(resolveStartUrl()).getHost();

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.rgb(2, 7, 13));

        progressBar = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progressBar.setIndeterminate(false);
        progressBar.setMax(100);
        progressBar.getProgressDrawable().setTint(Color.rgb(32, 231, 255));
        root.addView(progressBar, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 6));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.VERTICAL);
        header.setPadding(28, 20, 28, 18);
        header.setBackgroundColor(Color.rgb(4, 14, 27));
        TextView label = new TextView(this);
        label.setText("CivicOps Citizen");
        label.setTextColor(Color.rgb(32, 231, 255));
        label.setTextSize(13);
        label.setTypeface(Typeface.DEFAULT_BOLD);
        TextView subtitle = new TextView(this);
        subtitle.setText("Secure civic reporting shell");
        subtitle.setTextColor(Color.rgb(216, 231, 240));
        subtitle.setTextSize(15);
        header.addView(label);
        header.addView(subtitle);
        root.addView(header);

        webView = new WebView(this);
        root.addView(webView, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(root);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setGeolocationEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setSupportMultipleWindows(false);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setUserAgentString(settings.getUserAgentString() + " CivicOpsCitizenAndroid/1.0");
        webView.setWebContentsDebuggingEnabled(false);

        webView.setWebViewClient(new WebViewClient() {
            @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                Uri uri = request.getUrl();
                if (uri == null) return true;
                String scheme = uri.getScheme();
                String host = uri.getHost();
                boolean sameHost = allowedHost != null && allowedHost.equalsIgnoreCase(host);
                boolean webScheme = "https".equalsIgnoreCase(scheme) || "http".equalsIgnoreCase(scheme);
                return !(webScheme && sameHost);
            }
            @Override public void onPageFinished(WebView view, String url) { progressBar.setVisibility(View.GONE); }
        });
        webView.setWebChromeClient(new WebChromeClient() {
            @Override public void onProgressChanged(WebView view, int newProgress) {
                progressBar.setVisibility(newProgress >= 100 ? View.GONE : View.VISIBLE);
                progressBar.setProgress(newProgress);
            }
            @Override public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                Uri uri = Uri.parse(origin);
                callback.invoke(origin, allowedHost != null && allowedHost.equalsIgnoreCase(uri.getHost()), false);
            }
            @Override public void onPermissionRequest(PermissionRequest request) {
                Set<String> approved = new HashSet<>();
                for (String resource : request.getResources()) {
                    if (ALLOWED_WEBVIEW_RESOURCES.contains(resource)) approved.add(resource);
                }
                if (approved.isEmpty()) {
                    request.deny();
                } else {
                    request.grant(approved.toArray(new String[0]));
                }
            }
        });

        requestPermissions(new String[]{Manifest.permission.CAMERA, Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.RECORD_AUDIO}, PERMISSIONS_REQUEST);
        if (isOnline()) webView.loadUrl(resolveStartUrl()); else showOfflineState();
    }

    private String resolveStartUrl() {
        String baseUrl = BuildConfig.CIVICOPS_BASE_URL;
        if (baseUrl.endsWith("/")) baseUrl = baseUrl.substring(0, baseUrl.length() - 1);
        return baseUrl.endsWith("/app") ? baseUrl : baseUrl + "/app";
    }

    private boolean isOnline() {
        ConnectivityManager cm = (ConnectivityManager) getSystemService(CONNECTIVITY_SERVICE);
        NetworkInfo active = cm == null ? null : cm.getActiveNetworkInfo();
        return active != null && active.isConnected();
    }

    private void showOfflineState() {
        LinearLayout offline = new LinearLayout(this);
        offline.setOrientation(LinearLayout.VERTICAL);
        offline.setGravity(Gravity.CENTER);
        offline.setPadding(48, 48, 48, 48);
        offline.setBackgroundColor(Color.rgb(2, 7, 13));
        GradientDrawable badge = new GradientDrawable();
        badge.setColor(Color.rgb(4, 14, 27));
        badge.setStroke(2, Color.rgb(32, 231, 255));
        badge.setCornerRadius(18);
        TextView title = new TextView(this);
        title.setText("CivicOps Citizen App");
        title.setTextColor(Color.WHITE);
        title.setTextSize(28);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setGravity(Gravity.CENTER);
        title.setPadding(22, 18, 22, 18);
        title.setBackground(badge);
        TextView body = new TextView(this);
        body.setText("No network connection. Reconnect to submit reports, track references, view area alerts or open Copilot. No credentials are stored in this APK.");
        body.setTextColor(Color.rgb(216, 231, 240));
        body.setTextSize(16);
        body.setGravity(Gravity.CENTER);
        body.setPadding(0, 28, 0, 0);
        offline.addView(title);
        offline.addView(body);
        setContentView(offline);
    }

    @Override public void onBackPressed() {
        if (webView != null && webView.canGoBack()) webView.goBack(); else super.onBackPressed();
    }
}
