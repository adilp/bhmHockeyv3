# BHM Hockey - App Deployment Guide

Complete guide for building and publishing the BHM Hockey app to the App Store and Google Play Store without paying for Expo's EAS Build service.

## Prerequisites

- **Mac with Xcode** (required for iOS builds)
- **Apple Developer Account** ($99/year) - [developer.apple.com](https://developer.apple.com)
- **Google Play Developer Account** ($25 one-time) - [play.google.com/console](https://play.google.com/console)
- **Firebase Project** (free) - for Android push notifications
- Node.js and Yarn installed

---

## Table of Contents

1. [Pre-Deployment Checklist](#1-pre-deployment-checklist)
2. [Push Notification Setup](#2-push-notification-setup)
3. [Building for iOS](#3-building-for-ios)
4. [Building for Android](#4-building-for-android)
5. [Submitting to App Store](#5-submitting-to-app-store)
6. [Submitting to Google Play](#6-submitting-to-google-play)
7. [Post-Deployment](#7-post-deployment)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Pre-Deployment Checklist

### Verify Production API

Make sure your Digital Ocean API is deployed and accessible:

```bash
curl https://bhmhockey-mb3md.ondigitalocean.app/health
```

Expected response: `Healthy`

### Verify App Configuration

Check `apps/mobile/app.json`:

```json
{
  "expo": {
    "name": "BHM Hockey",
    "slug": "bhm-hockey",
    "version": "1.0.0",  // Update for each release
    "ios": {
      "bundleIdentifier": "com.bhmhockey.app",
      "buildNumber": "1"  // Increment for each build
    },
    "android": {
      "package": "com.bhmhockey.app",
      "versionCode": 1  // Increment for each build
    }
  }
}
```

### Verify Production API URL

The app automatically uses the production URL in release builds. Verify in `apps/mobile/config/api.ts`:

```typescript
// Line 62 - This is used in production builds
return configuredUrl || 'https://bhmhockey-mb3md.ondigitalocean.app/api';
```

### Update App Assets

Ensure these files are production-ready in `apps/mobile/assets/`:

| File | Size | Purpose |
|------|------|---------|
| `icon.png` | 1024x1024 | App icon |
| `splash.png` | 1284x2778 | Splash screen |
| `adaptive-icon.png` | 1024x1024 | Android adaptive icon |
| `notification-icon.png` | 96x96 | Android notification icon (white on transparent) |

---

## 2. Push Notification Setup

Push notifications require platform-specific credentials. **Complete this before building.**

### 2.1 iOS: Apple Push Notification Service (APNs)

#### Step 1: Create APNs Key

1. Go to [Apple Developer Portal - Keys](https://developer.apple.com/account/resources/authkeys/list)
2. Click **"+"** to create a new key
3. Enter a name: `BHM Hockey Push Key`
4. Check **"Apple Push Notifications service (APNs)"**
5. Click **Continue** → **Register**
6. **Download the `.p8` file** (you can only download once!)
7. Note the **Key ID** (shown on the page)
8. Note your **Team ID** (from [Membership page](https://developer.apple.com/account/#/membership))

Save these securely:
- `AuthKey_XXXXXXXXXX.p8` file
- Key ID: `XXXXXXXXXX`
- Team ID: `XXXXXXXXXX`

#### Step 2: Upload to Expo

```bash
# Login to Expo/EAS
npx eas login

# Configure credentials
npx eas credentials

# Select:
# → iOS
# → com.bhmhockey.app
# → Push Notifications: Set up
# → Upload your APNs key (.p8 file)
# → Enter Key ID and Team ID
```

### 2.2 Android: Firebase Cloud Messaging (FCM)

#### Step 1: Create Firebase Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Click **"Add project"**
3. Name: `BHM Hockey`
4. Disable Google Analytics (optional, not needed)
5. Click **Create project**

#### Step 2: Add Android App to Firebase

1. In Firebase Console, click **"Add app"** → Android icon
2. Enter package name: `com.bhmhockey.app`
3. App nickname: `BHM Hockey`
4. Skip the SHA-1 for now (can add later)
5. Click **Register app**
6. **Download `google-services.json`**
7. Click through remaining steps (we'll configure manually)

#### Step 3: Add google-services.json to Project

```bash
# Copy to mobile app directory
cp ~/Downloads/google-services.json apps/mobile/google-services.json
```

#### Step 4: Update app.json

Add the `googleServicesFile` to your `apps/mobile/app.json`:

```json
{
  "expo": {
    "android": {
      "adaptiveIcon": {
        "foregroundImage": "./assets/adaptive-icon.png",
        "backgroundColor": "#0D1117"
      },
      "package": "com.bhmhockey.app",
      "googleServicesFile": "./google-services.json"
    }
  }
}
```

#### Step 5: Get FCM Server Key (for Expo)

1. In Firebase Console → **Project Settings** (gear icon)
2. Go to **Cloud Messaging** tab
3. If you see "Cloud Messaging API (Legacy)" is disabled, click the three dots → **Manage API in Google Cloud Console** → **Enable**
4. Copy the **Server key**

#### Step 6: Upload FCM Key to Expo

```bash
npx eas credentials

# Select:
# → Android
# → com.bhmhockey.app
# → Push Notifications: Manage your FCM Api Key
# → Upload FCM Server Key
```

### 2.3 Verify Credentials

```bash
# Check all credentials are configured
npx eas credentials

# You should see:
# iOS: APNs Key configured ✓
# Android: FCM configured ✓
```

---

## 3. Building for iOS

### 3.1 Generate Native Project

```bash
cd apps/mobile

# Clean and regenerate native iOS project
npx expo prebuild --platform ios --clean
```

### 3.2 Install CocoaPods

```bash
cd ios
pod install
cd ..
```

### 3.3 Open in Xcode

```bash
open ios/BHMHockey.xcworkspace
```

**Important:** Open `.xcworkspace`, NOT `.xcodeproj`

### 3.4 Configure Signing

1. In Xcode, select the **BHMHockey** project in the navigator
2. Select the **BHMHockey** target
3. Go to **Signing & Capabilities** tab
4. Check **"Automatically manage signing"**
5. Select your **Team** (your Apple Developer account)
6. Xcode will create/download provisioning profiles

### 3.5 Configure Push Notifications Capability

1. Still in **Signing & Capabilities**
2. Click **"+ Capability"**
3. Add **"Push Notifications"**
4. Add **"Background Modes"** → check **"Remote notifications"**

### 3.6 Set Build Configuration

1. In Xcode menu: **Product** → **Scheme** → **Edit Scheme**
2. Select **Run** on the left
3. Set **Build Configuration** to **Release**

### 3.7 Archive for Distribution

1. Select **"Any iOS Device (arm64)"** as the build target (not a simulator)
2. Menu: **Product** → **Archive**
3. Wait for build to complete (5-10 minutes)
4. **Organizer** window opens automatically with your archive

### 3.8 Validate and Upload

1. In Organizer, select your archive
2. Click **"Distribute App"**
3. Select **"App Store Connect"** → **Next**
4. Select **"Upload"** → **Next**
5. Keep default options → **Next**
6. Select your distribution certificate → **Next**
7. Click **"Upload"**

Your build is now in App Store Connect!

---

## 4. Building for Android

### 4.1 Generate Native Project

```bash
cd apps/mobile

# Clean and regenerate native Android project
npx expo prebuild --platform android --clean
```

### 4.2 Create Signing Key

You need a keystore to sign your release builds. **Keep this file safe - you need the same key for all future updates!**

```bash
cd android/app

# Generate keystore (remember the passwords you set!)
keytool -genkeypair -v -storetype PKCS12 -keystore bhmhockey-release.keystore -alias bhmhockey -keyalg RSA -keysize 2048 -validity 10000

# You'll be prompted for:
# - Keystore password (create one, save it!)
# - Key password (can be same as keystore password)
# - Your name, organization, etc.
```

### 4.3 Configure Gradle for Signing

Create/edit `android/gradle.properties`:

```properties
MYAPP_RELEASE_STORE_FILE=bhmhockey-release.keystore
MYAPP_RELEASE_KEY_ALIAS=bhmhockey
MYAPP_RELEASE_STORE_PASSWORD=your_keystore_password
MYAPP_RELEASE_KEY_PASSWORD=your_key_password
```

**Security Note:** Don't commit passwords to git. For CI/CD, use environment variables.

Edit `android/app/build.gradle`, add signing config in the `android` block:

```gradle
android {
    ...
    signingConfigs {
        release {
            if (project.hasProperty('MYAPP_RELEASE_STORE_FILE')) {
                storeFile file(MYAPP_RELEASE_STORE_FILE)
                storePassword MYAPP_RELEASE_STORE_PASSWORD
                keyAlias MYAPP_RELEASE_KEY_ALIAS
                keyPassword MYAPP_RELEASE_KEY_PASSWORD
            }
        }
    }
    buildTypes {
        release {
            signingConfig signingConfigs.release
            minifyEnabled true
            proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'), 'proguard-rules.pro'
        }
    }
}
```

### 4.4 Build Release AAB

```bash
cd apps/mobile/android

# Build Android App Bundle (required for Play Store)
./gradlew bundleRelease
```

The AAB file will be at:
```
android/app/build/outputs/bundle/release/app-release.aab
```

### 4.5 (Optional) Build APK for Testing

```bash
# Build APK for direct installation/testing
./gradlew assembleRelease
```

APK location: `android/app/build/outputs/apk/release/app-release.apk`

---

## 5. Submitting to App Store

### 5.1 App Store Connect Setup

1. Go to [App Store Connect](https://appstoreconnect.apple.com/)
2. Click **"My Apps"** → **"+"** → **"New App"**
3. Fill in:
   - Platform: iOS
   - Name: BHM Hockey
   - Primary Language: English (U.S.)
   - Bundle ID: com.bhmhockey.app
   - SKU: bhmhockey (any unique identifier)
4. Click **Create**

### 5.2 App Information

Fill out all required fields:

**App Information:**
- Privacy Policy URL (required)
- Category: Sports

**Pricing and Availability:**
- Price: Free
- Availability: All territories (or select specific)

**App Privacy:**
- Complete the privacy questionnaire about data collection

### 5.3 Prepare Screenshots

Required screenshots (use Simulator to capture):

| Device | Size | Required |
|--------|------|----------|
| iPhone 6.7" | 1290 x 2796 | Yes (iPhone 15 Pro Max) |
| iPhone 6.5" | 1284 x 2778 | Yes (iPhone 11 Pro Max) |
| iPhone 5.5" | 1242 x 2208 | Optional (iPhone 8 Plus) |
| iPad 12.9" | 2048 x 2732 | If supporting iPad |

### 5.4 Submit for Review

1. In App Store Connect, go to your app
2. Select the build you uploaded
3. Fill in "What's New" (version notes)
4. Add screenshots
5. Click **"Submit for Review"**

Review typically takes 24-48 hours.

---

## 6. Submitting to Google Play

### 6.1 Google Play Console Setup

1. Go to [Google Play Console](https://play.google.com/console)
2. Click **"Create app"**
3. Fill in:
   - App name: BHM Hockey
   - Default language: English (US)
   - App or game: App
   - Free or paid: Free
4. Accept declarations
5. Click **Create app**

### 6.2 Store Listing

Fill out all required fields:

**Main store listing:**
- Short description (80 chars max)
- Full description (4000 chars max)
- App icon (512x512)
- Feature graphic (1024x500)
- Screenshots (min 2, max 8 per device type)

**App categorization:**
- Category: Sports
- Tags: Hockey, Sports, Team Management

### 6.3 App Content

Complete all content declarations:

- Privacy policy URL
- Ads declaration (no ads)
- Content rating questionnaire
- Target audience
- Data safety form

### 6.4 Upload AAB

1. Go to **Release** → **Production**
2. Click **"Create new release"**
3. Upload your `app-release.aab` file
4. Add release notes
5. Click **"Review release"**
6. Click **"Start rollout to Production"**

Review typically takes a few hours to a few days.

---

## 7. Post-Deployment

### 7.1 Monitor Crashes

**iOS:**
- App Store Connect → App Analytics
- Xcode → Organizer → Crashes

**Android:**
- Google Play Console → Quality → Android Vitals
- Firebase Crashlytics (if configured)

### 7.2 Updating the App

For each new release:

1. **Update version numbers** in `apps/mobile/app.json`:
   ```json
   {
     "expo": {
       "version": "1.1.0",
       "ios": {
         "buildNumber": "2"
       },
       "android": {
         "versionCode": 2
       }
     }
   }
   ```

2. **Regenerate native projects:**
   ```bash
   npx expo prebuild --clean
   ```

3. **Build and submit** following sections 3-6 above

### 7.3 Keystore Backup

**CRITICAL:** Back up your Android keystore and passwords securely. If you lose them, you cannot update your app - you'll have to publish a new app with a different package name.

Recommended backup locations:
- Password manager (1Password, Bitwarden)
- Encrypted cloud storage
- Physical secure location

---

## 8. Troubleshooting

### iOS Build Errors

**"No signing certificate"**
- Xcode → Preferences → Accounts → Download Manual Profiles
- Or delete derived data: `rm -rf ~/Library/Developer/Xcode/DerivedData`

**"Provisioning profile doesn't include Push Notifications"**
- Regenerate profiles in Apple Developer Portal
- Or let Xcode manage automatically (recommended)

**CocoaPods issues**
```bash
cd ios
pod deintegrate
pod cache clean --all
pod install
```

### Android Build Errors

**"Could not find google-services.json"**
- Ensure file is at `apps/mobile/google-services.json`
- Check `app.json` has correct path in `googleServicesFile`

**"Keystore was tampered with"**
```bash
# Delete and regenerate
rm android/app/bhmhockey-release.keystore
keytool -genkeypair -v -storetype PKCS12 -keystore android/app/bhmhockey-release.keystore -alias bhmhockey -keyalg RSA -keysize 2048 -validity 10000
```

**Gradle build fails**
```bash
cd android
./gradlew clean
./gradlew bundleRelease
```

### Push Notifications Not Working

1. **Verify credentials are uploaded:**
   ```bash
   npx eas credentials
   ```

2. **Check API is sending notifications:**
   - Check API logs in Digital Ocean
   - Verify user has push token saved

3. **iOS specific:**
   - Ensure Push Notifications capability is added in Xcode
   - Ensure Background Modes → Remote notifications is enabled

4. **Android specific:**
   - Verify `google-services.json` has correct package name
   - Check Firebase Console for delivery reports

### App Rejected

**Common rejection reasons:**

| Reason | Solution |
|--------|----------|
| Crashes on launch | Test thoroughly on real devices |
| Missing privacy policy | Add privacy policy URL |
| Incomplete metadata | Fill all required fields |
| Broken links | Verify all URLs work |
| Login required without demo | Provide demo account credentials |

---

## Quick Reference Commands

```bash
# Full clean rebuild
cd apps/mobile
rm -rf ios android node_modules
yarn install
npx expo prebuild --clean

# iOS build
cd ios && pod install && cd ..
open ios/BHMHockey.xcworkspace
# Then Archive in Xcode

# Android build
cd android
./gradlew clean
./gradlew bundleRelease

# Check credentials
npx eas credentials

# Test production API
curl https://bhmhockey-mb3md.ondigitalocean.app/health
```

---

## Files to Never Commit

Add to `.gitignore`:

```
# Secrets
google-services.json
*.keystore
gradle.properties  # if it contains passwords

# Build artifacts
android/app/build/
ios/build/
*.aab
*.apk
*.ipa
```

---

**Last Updated:** December 2024
