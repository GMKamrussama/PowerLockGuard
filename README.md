# 🛡️ PowerLockGuard v2.0

> **Smart Power Guard for Developers, AI Agents & Laptop Users**  
> *Auto-Sleep or Shut Down when AI Agents & IDE tasks finish — and Instant Deep Sleep on Charger Unplug.*

[![License: MIT](https://img.shields.io/badge/License-MIT-emerald.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com)
[![Binary Size](https://img.shields.io/badge/Size-~60%20KB-success.svg)]()
[![Microsoft Store Ready](https://img.shields.io/badge/Microsoft%20Store-Ready-blueviolet.svg)](StorePackaging/MICROSOFT_STORE_GUIDE.md)

---

## 🌟 The Problem & The Solution

### 🌙 Scenario 1: Leaving AI Agents & Builds Running at Midnight
You are working late at night (e.g. 12:00 AM). Several AI agents (**Claude Code, Cursor, Aider, Codex**) or long-running builds, test suites, or training scripts are still running on your laptop. You need to go to sleep, but you don't want your laptop to stay awake at full power all night long after the work finishes.

👉 **PowerLockGuard Solution:**  
1. Select **"Sleep"** (or **"Shut Down"**).
2. Click **"ACTIVATE WORK WATCHDOG"**.
3. Go to bed!
4. PowerLockGuard monitors the active processes, ensures all tasks and child processes have completed (with a smart inactivity grace period so waiting for API responses doesn't trigger sleep prematurely), shows a safety countdown, and automatically puts your laptop into **Deep Sleep (S3 Standby)** or **Powers it Down**.

---

### ⚡ Scenario 2: Instant Sleep on Charger Disconnection
You want your laptop to lock down or sleep instantly the second its charger is pulled out (e.g., leaving a workspace, anti-theft, or battery preservation).

👉 **PowerLockGuard Solution:**  
1. Turn on **Charger Guard**.
2. Whenever the power cord is unplugged, your laptop enters **Instant Deep Sleep** within milliseconds.
3. Plug it back in and tap the power button — all your open IDE tabs, terminal sessions, and work are exactly where you left them with **zero battery drain**.

---

## ✨ Key Features

- **🤖 Smart AI Agent & IDE Watchdog:**
  - **Auto-Detection Preset:** Automatically monitors popular AI coding tools and runtimes:
    - *Agents & CLIs:* `Claude Code`, `Aider`, `Codex`, `Copilot CLI`, `Gemini`, `Antigravity`, `Cline`
    - *IDEs & Editors:* `Cursor`, `VS Code`, `Windsurf`, `Visual Studio`, `JetBrains` (PyCharm, WebStorm, IntelliJ)
    - *Compilers & Tools:* `Node.js`, `Python`, `Git`, `Cargo` / `Rust`, `dotnet`, `Java`, `npm`, `yarn`
  - **Specific Process Mode:** Wait for a specific command or script (e.g. `claude`, `python train.py`, `npm test`) to terminate.
- **⏱️ Smart Inactivity Buffer (Grace Period):**
  - Choose between **1, 2, 3 (Recommended), 5, or 10 minutes** of sustained idle time.
  - Prevents premature sleep while an agent is waiting for an LLM API reply (which often takes 15–45 seconds).
- **🔔 Safety Countdown Warning Dialog:**
  - When tasks finish, a prominent 30/60-second warning dialog appears with an audio chime.
  - If you're still at your PC, tap **Cancel** or press `Esc` to keep your PC awake.
- **⚡ Multiple Power Actions:**
  - **Sleep (Standby S3)** *(Recommended)*: Keeps RAM alive, instantaneous wake-up, preserves open files.
  - **Shut Down**: Clean power-off.
  - **Hibernate**: Saves session to SSD/HDD and shuts down completely.
  - **Lock Screen**: Locks Windows desktop.
- **🪶 Ultra-Lightweight & Offline:**
  - Single standalone executable (~60 KB).
  - 100% offline, zero network telemetry, <0.1% background CPU usage.
  - Native Windows system tray integration.

---

## 🇧🇩 বাংলা নির্দেশিকা (Bengali Guide)

### এটি ঠিক যেভাবে কাজ করে:

1. **রাতে এআই এজেন্ট বা কোড রান করে নিশ্চিন্তে ঘুমান:**
   - রাত ১২টায় বা যেকোনো সময় এআই এজেন্ট (Claude Code, Cursor, Aider ইত্যাদি) বা বিল্ড স্ক্রিপ্ট চালু রেখে ঘুমাতে যেতে চান?
   - **Work Watchdog** ট্যাবে গিয়ে **Sleep** অথবা **Shut Down** সিলেক্ট করে বড় বাটনে ক্লিক করুন।
   - সব কাজ ও প্রসেস শেষ হয়ে টানা ৩ মিনিট (বা আপনার পছন্দের সময়) আইডল থাকলেই সফটওয়্যারটি পিসিকে স্বয়ংক্রিয়ভাবে স্লিপ বা শাটডাউন করে দেবে।
   - স্লিপ হওয়ার আগে স্ক্রিনে ৩০/৬০ সেকেন্ডের একটি কাউন্টডাউন ও বিপিং অ্যালার্ট আসবে। আপনি জেগে থাকলে এক ক্লিকেই "Cancel" করতে পারবেন।

2. **চার্জার খুললেই ইনস্ট্যান্ট স্লিপ (Charger Guard):**
   - ল্যাপটপ থেকে চার্জার খোলার সাথে সাথে চোখের পলকে পিসি **Deep Sleep (S3 Standby)** মোডে চলে যাবে।
   - ব্যাটারি এক ফোঁটাও নষ্ট হবে না এবং ডিসপ্লে ও ফ্যান সাথে সাথে বন্ধ হয়ে যাবে।
   - পাওয়ার বাটন চাপলে আবার সব কোড আগের মতো খোলা পাবেন।

3. **কাজ করার সময় ১-ক্লিকে বন্ধ রাখার সুবিধা:**
   - স্বাভাবিক কাজের সময় যখন সুরক্ষা দরকার নেই, তখন বাটনে ক্লিক করে **PAUSED** করে দিন।

---

## 🛠️ Build & Development

### Prerequisites
- Any Windows 10 / 11 system with .NET Framework 4.0 or newer (pre-installed on virtually all modern Windows PCs).

### 1-Click Instant Build
Simply double-click:
```cmd
Build.bat
```
This uses the built-in Windows C# compiler (`csc.exe`) to compile `PowerLockGuard.exe` with embedded icon in under 2 seconds.

### Visual Studio / MSBuild
You can also open the project in Visual Studio 2019 / 2022:
```cmd
PowerLockGuard.sln
```
Or build via command line:
```cmd
msbuild PowerLockGuard.csproj /p:Configuration=Release
```

---

## 🛒 Microsoft Store & Public Distribution

PowerLockGuard is architected to be published on the **Microsoft Store**:
- Follow the step-by-step guide in [`StorePackaging/MICROSOFT_STORE_GUIDE.md`](StorePackaging/MICROSOFT_STORE_GUIDE.md).
- Manifest template: [`StorePackaging/Package.appxmanifest`](StorePackaging/Package.appxmanifest).
- Can be published directly as a Win32 desktop application or packaged as an MSIX bundle.

---

## 📄 License

Distributed under the **MIT License**. See [`LICENSE`](LICENSE) for details. Open source and free for developers worldwide!
