# Microsoft Store Publishing Guide for PowerLockGuard

PowerLockGuard can be published to the Microsoft Store via Microsoft Partner Center using two methods:

---

## Method 1: Win32 App (Easiest & Recommended)
Microsoft Store now officially supports unpackaged Win32 `.exe` applications directly.

1. **Sign in to Microsoft Partner Center**:
   - Go to [partner.microsoft.com](https://partner.microsoft.com/dashboard).
   - Register as an individual or company developer account ($19 USD one-time fee).
2. **Create a New App Submission**:
   - Click **Apps & games** -> **New product**.
   - Choose **Windows App (EXE or MSI)**.
   - Reserve your app name: `PowerLockGuard`.
3. **App Details & Assets**:
   - Upload description, category (Developer Tools / Utilities).
   - Upload app icons and screenshots (minimum 1366x768).
   - Provide the download URL to your signed installer or hosted `.exe` on GitHub Releases.
4. **Publish**:
   - Submit for certification. Review typically takes 24-48 hours.

---

## Method 2: MSIX Package (Windows Store Native)
If you prefer distributing as a sandboxed MSIX package:

1. Install **MSIX Packaging Tool** from the Microsoft Store.
2. Follow the wizard:
   - Select `PowerLockGuard.exe`.
   - Use the Package Identity specified in `Package.appxmanifest`.
3. Test locally:
   ```powershell
   Add-AppxPackage -Path .\PowerLockGuard.msix
   ```
4. Upload the generated `.msix` file directly to Microsoft Partner Center.
