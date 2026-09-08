# 🛡️ PowerLockGuard v1.0.0

> **Smart Power Guard for Developers, AI Agents & Laptop Users**  
> *Auto-Sleep or Shut Down when AI Agents & IDE tasks finish — and Instant Deep Sleep on Charger Unplug.*  
> **Developed by [GMK Solution](https://gmksolution.com)**

<p align="center">
  <a href="#-english"><b>English 🇬🇧</b></a> •
  <a href="#-বাংলা-ভার্সন-bengali"><b>বাংলা 🇧🇩</b></a>
</p>

<p align="center">
  <a href="https://github.com/GMKamrussama/PowerLockGuard/releases/latest"><img src="https://img.shields.io/badge/Download-PowerLockGuard.exe%20(~62%20KB)-10B981?style=for-the-badge&logo=windows&logoColor=white" alt="Download EXE" /></a>
  <a href="https://apps.microsoft.com/detail/PowerLockGuard"><img src="https://img.shields.io/badge/Microsoft_Store-Get_App-0078D4?style=for-the-badge&logo=microsoft&logoColor=white" alt="Microsoft Store" /></a>
  <a href="https://gmksolution.com"><img src="https://img.shields.io/badge/Developer-GMK_Solution-0F172A?style=for-the-badge&logo=googlechrome&logoColor=10B981" alt="GMK Solution" /></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Version-1.0.0-emerald.svg" alt="Version 1.0.0" />
  <img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blueviolet.svg" alt="Windows" />
  <img src="https://img.shields.io/badge/Runtime-.NET%20Framework%204.0+-purple.svg" alt=".NET" />
  <img src="https://img.shields.io/badge/Zero_Telemetry-100%25%20Offline-success.svg" alt="Offline" />
</p>

---

## 📥 Download & Installation

### Option 1: Direct Executable Download (GitHub Release)
No installer required! Download the single lightweight standalone `.exe` (~62 KB) and run it:
- 🚀 **[Download Latest PowerLockGuard.exe (v1.0.0)](https://github.com/GMKamrussama/PowerLockGuard/releases/latest/download/PowerLockGuard.exe)**
- Or download via **GitHub CLI**:
  ```bash
  gh release download --repo GMKamrussama/PowerLockGuard --pattern "PowerLockGuard.exe"
  ```

### Option 2: Microsoft Store / Windows Package Manager (winget)
You can install and automatically update PowerLockGuard via Microsoft Store:
- 🛒 **[Get it from Microsoft Store](https://apps.microsoft.com/detail/PowerLockGuard)**
- Or install via **winget**:
  ```powershell
  winget install PowerLockGuard
  ```

---

<a name="-english"></a>
# 🇬🇧 English Documentation

## 🏢 About the Developer
PowerLockGuard is crafted by **[GMK Solution](https://gmksolution.com)** — empowering developers with high-performance, privacy-first, lightweight productivity tools.  
- **Official Website:** [https://gmksolution.com](https://gmksolution.com)
- **Support & Inquiries:** [gmksolution.com](https://gmksolution.com)

---

## 🌟 The Problems We Solve

### 🌙 Scenario 1: Leaving AI Agents & Builds Running Overnight
You are working late at night (e.g. 12:00 AM). Several AI coding agents (**Claude Code, Cursor, Aider, Codex, Copilot**) or long-running builds, test suites, or training scripts are active on your PC. You need to sleep, but you don't want your laptop running hot at full power all night long after the tasks finish.

👉 **The Solution:**  
1. Switch to the **Work Watchdog** tab.
2. Select your action: **Sleep (Standby S3 - Recommended)** or **Shut Down**.
3. Click **ACTIVATE WORK WATCHDOG** and go to bed!
4. PowerLockGuard monitors the tasks and child processes. When work completes and stays idle for the configured grace period, your PC automatically sleeps or powers down.

### ⚡ Scenario 2: Instant Sleep on Charger Disconnection
You want your laptop to lock down or sleep instantly the moment someone disconnects the charger cord (e.g., leaving a workspace, battery preservation, or anti-tamper).

👉 **The Solution:**  
1. Turn on **Charger Guard**.
2. Whenever the power cord is unplugged, your laptop enters **Deep Sleep (S3 Standby)** within milliseconds. Zero battery drain, and all open IDE tabs remain intact in RAM upon manual wake-up.

---

## 📸 Screenshots

| 🤖 AI Agent & Work Watchdog | 🛡️ Instant Charger Unplug Guard |
|:---:|:---:|
| ![Work Watchdog](docs/screenshots/01_work_watchdog.png) | ![Charger Guard](docs/screenshots/02_charger_guard.png) |
| *Auto-sleeps when AI agents & IDE tasks finish* | *Instant deep sleep on power cord disconnection* |

| ⏰ Safety Countdown Warning Dialog | ⚙️ Settings & Activity History |
|:---:|:---:|
| ![Countdown Dialog](docs/screenshots/04_safety_countdown.png) | ![Settings & History](docs/screenshots/03_settings_history.png) |
| *30s audio chime & warning. Press Esc to cancel* | *Auto-start with Windows & full activity logs* |

---

## ✨ Key Features

- **🤖 Smart AI Agent & IDE Watchdog:**
  - **Auto-Detection Preset:** Automatically tracks popular AI coding tools and runtimes:
    - *Agents & CLIs:* `Claude Code`, `Aider`, `Codex`, `Copilot CLI`, `Gemini`, `Antigravity`, `Cline`
    - *IDEs & Editors:* `Cursor`, `VS Code`, `Windsurf`, `Visual Studio`, `JetBrains` (PyCharm, WebStorm, IntelliJ)
    - *Compilers & Tools:* `Node.js`, `Python`, `Git`, `Cargo` / `Rust`, `dotnet`, `Java`, `npm`, `yarn`
  - **Specific Process Mode:** Target a specific executable or script (e.g. `claude`, `python train.py`, `npm test`).
- **⏱️ Smart Inactivity Buffer (Grace Period):**
  - Configurable: **1, 2, 3 (Recommended), 5, or 10 minutes** of sustained idle time.
  - Prevents premature sleep while an agent is waiting for an LLM API reply (which often takes 15–45 seconds).
- **🔔 Safety Countdown Warning Dialog:**
  - When tasks finish, a prominent 30/60-second warning dialog appears with an audio chime.
  - If you are still at your desk, tap **Cancel** or press `Esc` to keep your PC awake.
- **⚡ Multiple Power Actions:**
  - **Sleep (Standby S3)** *(Recommended)*: Preserves RAM, instantaneous wake-up, keeps code and files intact.
  - **Shut Down**: Complete power-off.
  - **Hibernate**: Saves memory to SSD/HDD and turns off.
  - **Lock Screen**: Locks Windows desktop.
- **🪶 Ultra-Lightweight & Offline:**
  - Standalone executable (~62 KB).
  - 100% offline, zero network telemetry, <0.1% background CPU.
  - Native Windows system tray integration.

---

## 🛠️ Build & Development

### 1-Click Instant Build
Double-click:
```cmd
Build.bat
```
Uses the built-in Windows C# compiler (`csc.exe`) to compile `PowerLockGuard.exe` with embedded multi-resolution icon in under 2 seconds.


### Visual Studio / MSBuild
```cmd
msbuild PowerLockGuard.csproj /p:Configuration=Release
```

---

## 🏷️ GitHub Repository SEO & Discoverability Setup

When configuring your GitHub repository settings (`https://github.com/<org>/PowerLockGuard`), use these optimized values for maximum search visibility across GitHub, Google, and Bing:

| Setting | Recommended Value |
|:---|:---|
| **Repository Description** | `⚡ Smart auto-sleep & power management for developers, laptops & AI agents (Claude Code, Cursor, Aider, VS Code). Automatically sleeps or shuts down when coding tasks finish, plus instant deep sleep on charger unplug. Lightweight (~62 KB) & offline. Built by GMK Solution.` |
| **Website** | `https://gmksolution.com` |
| **Topics / Tags** *(Copy-paste into Topics)* | `ai-agents`, `claude-code`, `cursor-ai`, `developer-tools`, `auto-sleep`, `auto-shutdown`, `power-management`, `battery-saver`, `sleep-timer`, `windows-utility`, `gmk-solution`, `csharp`, `winforms`, `deep-sleep`, `laptop`, `standby`, `dev-tools`, `offline-first`, `productivity`, `workstation` |

---

## 🛒 Microsoft Store Packaging & SEO Metadata

Everything required to publish on the **Microsoft Partner Center** is prepared and optimized:
- 📋 **Full Store SEO Listing Data:** [`StorePackaging/MICROSOFT_STORE_METADATA.md`](StorePackaging/MICROSOFT_STORE_METADATA.md) *(Title, Short Description, Full Description, 7 Search Keywords, Feature Bullets, Release Notes)*
- 📖 **Step-by-Step Submission Guide:** [`StorePackaging/MICROSOFT_STORE_GUIDE.md`](StorePackaging/MICROSOFT_STORE_GUIDE.md)
- 🖼️ **1080p Promotional Store Showcase Images:** [`StorePackaging/assets/`](StorePackaging/assets/)
- 📦 **Appx / MSIX Package Manifest:** [`StorePackaging/Package.appxmanifest`](StorePackaging/Package.appxmanifest)

---

<a name="-বাংলা-ভার্সন-bengali"></a>
# 🇧🇩 বাংলা ভার্সন (Bengali Documentation)

## 🏢 ডেভেলপার পরিচিতি
**PowerLockGuard v1.0.0** তৈরি করেছে **[GMK Solution](https://gmksolution.com)**। এটি ডেভেলপারদের জন্য একটি সম্পূর্ণ অফলাইন, দ্রুতগতির এবং ওপেন-সোর্স পাওয়ার ম্যানেজমেন্ট সফটওয়্যার।  
- **অফিশিয়াল ওয়েবসাইট:** [https://gmksolution.com](https://gmksolution.com)

---

## 🌟 যে দুটি সমস্যার স্থায়ী সমাধান:

### 🌙 ১. রাতে এআই এজেন্ট বা কোড রান করে নিশ্চিন্তে ঘুমান:
রাত ১২টায় আপনি ঘুমাতে যেতে চান, কিন্তু ব্যাকগ্রাউন্ডে কোডিং এআই এজেন্ট (**Claude Code, Cursor, Aider, Codex, Copilot**) অথবা কোনো স্ক্রিপ্ট বা বিল্ড রান হচ্ছে। আপনি ঘুমাতে পারছেন না কারণ কাজ শেষ হলে পিসি সারা রাত অন থাকবে।

👉 **পাওয়ারলক গার্ডের সমাধান:**
1. **Work Watchdog** ট্যাবে যান।
2. আপনার পছন্দের অ্যাকশন নির্বাচন করুন: 💤 **Sleep (Standby S3 - Recommended)** অথবা 🛑 **Shut Down PC**।
3. **ACTIVATE WORK WATCHDOG** বাটনে ক্লিক করে ঘুমাতে যান!
4. এআই এজেন্ট ও কোডিং টাস্ক শেষ হয়ে টানা ৩ মিনিট (বা আপনার সেট করা বাফার সময়) নিষ্ক্রিয় থাকলে সফটওয়্যারটি স্বয়ংক্রিয়ভাবে পিসি স্লিপ বা শাটডাউন করে দেবে।

### ⚡ ২. চার্জার খুললেই ইনস্ট্যান্ট স্লিপ (Charger Guard):
ল্যাপটপ থেকে চার্জার খোলার সাথে সাথে চোখের পলকে পিসি **Deep Sleep (S3 Standby)** মোডে চলে যাবে। ডিসপ্লে ও ফ্যানের পাওয়ার সম্পূর্ণ বন্ধ হয়ে যাবে, কোনো ব্যাটারি খরচ হবে না এবং পাওয়ার বাটন চাপলেই সব কোড ও ফাইল আগের মতোই খোলা পাবেন। স্বাভাবিক কাজের সময় ১-ক্লিকেই প্রোটেকশন **PAUSED** করে রাখা যায়।

---

## 📥 ডাউনলোড ও ব্যবহার করার নিয়ম

### গিটহ্যাব থেকে সরাসরি ডাউনলোড (.exe):
কোনো ইন্সটলারের প্রয়োজন নেই! সরাসরি সিঙ্গেল এক্সিকিউটেবল ডাউনলোড করে ডাবল-ক্লিক করলেই চালু হবে:
- 🚀 **[সরাসরি ডাউনলোড করুন: PowerLockGuard.exe (v1.0.0)](https://github.com/GMKamrussama/PowerLockGuard/releases/latest/download/PowerLockGuard.exe)**

### মাইক্রোসফট স্টোর বা টার্মিনাল থেকে ইনস্টল:
- 🛒 **[মাইক্রোসফট স্টোর থেকে ইনস্টল করুন](https://apps.microsoft.com/detail/PowerLockGuard)**
- অথবা **PowerShell / Terminal**-এ নিচের কমান্ডটি দিন:
  ```powershell
  winget install PowerLockGuard
  ```

---

## 📸 সফটওয়্যারের স্ক্রিনশটসমূহ:

| 🤖 এআই এজেন্ট ও আইডিই ওয়াচডগ | 🛡️ ইনস্ট্যান্ট চার্জার আনপ্লাগ গার্ড |
|:---:|:---:|
| ![Work Watchdog](docs/screenshots/01_work_watchdog.png) | ![Charger Guard](docs/screenshots/02_charger_guard.png) |
| *কোডিং এজেন্ট ও বিল্ড শেষ হলে অটো স্লিপ* | *চার্জার খুললেই তাৎক্ষণিক ডিপ স্লিপ* |

| ⏰ সেফটি কাউন্টডাউন ও বিপ অ্যালার্ট | ⚙️ সেটিংস ও অ্যাক্টিভিটি হিস্ট্রি |
|:---:|:---:|
| ![Countdown Dialog](docs/screenshots/04_safety_countdown.png) | ![Settings & History](docs/screenshots/03_settings_history.png) |
| *স্লিপের আগে ৩০ সেকেন্ড সতর্কবার্তা ও বিপ* | *উইন্ডোজের সাথে অটো-স্টার্ট ও ইভেন্ট লগ* |

---

## 🛒 মাইক্রোসফট স্টোরে পাবলিশ করার গাইড

মাইক্রোসফট পার্টনার সেন্টারে অ্যাপটি সহজে পাবলিশ করার জন্য বিস্তারিত গাইড ও মেনিফেস্ট প্রস্তুত আছে:
- 📋 **সম্পূর্ণ এসইও মেটাডাটা ও কি-ওয়ার্ড তালিকা:** [`StorePackaging/MICROSOFT_STORE_METADATA.md`](StorePackaging/MICROSOFT_STORE_METADATA.md)
- 📄 নির্দেশিকা ও পাবলিশিং স্টেপস: [`StorePackaging/MICROSOFT_STORE_GUIDE.md`](StorePackaging/MICROSOFT_STORE_GUIDE.md)
- 🖼️ মাইক্রোসফট স্টোর ১৯২০x১০৮০ শোকেস প্রমোশনাল ইমেজ: [`StorePackaging/assets/`](StorePackaging/assets/)
- 📦 MSIX Manifest: [`StorePackaging/Package.appxmanifest`](StorePackaging/Package.appxmanifest)

---

## 📄 লাইসেন্স (License)

সম্পূর্ণ **MIT License**-এর অধীনে ওপেন-সোর্স করা হয়েছে। বিশ্বের যেকোনো ডেভেলপার এটি বিনামূল্যে ব্যবহার, মডিফাই ও রি-ডিস্ট্রিবিউট করতে পারবেন। বিস্তারিত জানতে [`LICENSE`](LICENSE) ফাইলটি দেখুন।  
**Developer:** [GMK Solution](https://gmksolution.com)
