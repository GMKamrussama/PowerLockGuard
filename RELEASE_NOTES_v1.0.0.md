# 🛡️ PowerLockGuard v1.0.0 — Release Notes

> **Smart Power Guard for Developers, AI Agents & Laptop Users**  
> *Developed with ❤️ by [GMK Solution](https://gmksolution.com)*

---

## 🚀 What's New in v1.0.0

We are thrilled to release **PowerLockGuard v1.0.0**, the ultimate lightweight power management tool engineered specifically for developers running autonomous AI coding agents, long builds, and laptop users who value battery health and physical security.

### 🌟 1. Autonomous AI Agent & Dev Work Watchdog
- **Pre-Configured Intelligent Presets:**
  - `⚡ AI Agents + Active Builds (Recommended)`: Automatically tracks Claude Code, Aider, Codex, Copilot, Cline, Antigravity, Node.js, Python, Git, Cargo/Rust, etc.
  - `🤖 AI Coding Agents Only`: Dedicated purely to autonomous AI agent processes.
  - `💻 All Dev Tools`: Includes heavy IDE runtimes (Cursor, VS Code, Windsurf).
  - `🎯 Specific Process Name`: Manually target any custom process, script, or executable.
- **Configurable Idle Cutoff Thresholds:** Select from `1.5%`, `2.5%`, `3.5% (Default)`, `5.0%`, `8.0%`, and `10.0%` CPU to match lightweight or heavy developer setups.
- **Configurable Inactivity Buffers:** Choose `1m`, `2m`, `3m (Recommended)`, `5m`, or `10m` to accommodate LLM API thinking latency without prematurely sleeping.

---

### 🔴 2. Stuck / Runaway Process Detection & Auto-Kill
- **Vivid Alert Red Highlighting (`🔴 STUCK / High`):** Any deadlocked or pinned background process consuming excessive CPU is flagged with a soft red alert background (`#FEE2E2`), dark bold red text, and pushed to the very top row of the telemetry grid.
- **⚡ Auto-Kill on Sleep Engine:** A new toggleable option automatically terminates hung or runaway tasks right before putting the PC to sleep. Never wake up to an overheated machine or dead battery again!

---

### 📊 3. Live Per-Process Telemetry Grid
- Real-time double-buffered, flicker-free list view displaying:
  - **Process Name & PID**
  - **Normalized CPU %**
  - **RAM Memory (MB)**
  - **Activity Badges** (`🔴 STUCK / High`, `🟢 Active`, `⚪ Idle`)
  - **Tracking State** (`✓ Tracking`, `🚫 Ignored`)
- **100% Responsive Columns:** Perfectly scales with your window size; zero bottom horizontal scrollbar.
- **Context Controls:** Instantly ignore non-critical processes or terminate pegged tasks with 1 click.

---

### 🎨 4. Modern Gradient Activity & Countdown Meter
- Replaces generic progress bars with an anti-aliased 22px custom gradient meter:
  - **Active Work:** Vibrant Emerald-to-Teal gradient indicating active CPU workload.
  - **Stuck Tasks:** Warning Amber-to-Red gradient indicating heavy load.
  - **Idle Countdown:** Sleek Royal Blue-to-Indigo gradient tracking countdown buffer progression.

---

### 🛡️ 5. Instant Charger Guard (Electricity Outage & AI Task RAM Preservation)
- **Sudden Power Outage Protection:** When electricity suddenly fails and your home/office WiFi router drops, Charger Guard instantly puts your PC into **Deep Sleep (S3 Standby)** in milliseconds.
- **Preserves In-Flight AI Tasks:** Freezes your autonomous AI coding agents (Claude Code, Cursor, Aider) safely in RAM *before* network timeout exceptions or broken responses corrupt your code and burn expensive LLM tokens. When power and router return, tap the power button to resume seamlessly!
- **Anti-Theft & Battery Health:** Also triggers instant lock/sleep if a charging cord is unplugged at a public venue, saving 100% of your laptop's battery.

---

### 🪶 6. Ultra-Lightweight & 100% Offline
- Single standalone executable (~90 KB).
- Zero dependencies, zero installers, zero background bloat (<15 MB RAM, 0.0% CPU when idle).
- Zero network telemetry: 100% private and offline.

---

## 📥 Download & Verification

| Asset | Platform | File Size | SHA-256 Checksum |
|:---|:---:|:---|:---|
| **`PowerLockGuard.exe`** | Windows 10 / 11 (x86/x64) | ~90 KB (92,160 bytes) | `9794F83EC1352D20D7065FA5E2BD14463E4F9059D6F0D231DE270722FC5C528B` |

### Verify with PowerShell:
```powershell
Get-FileHash .\PowerLockGuard.exe -Algorithm SHA256
```

---

## 🚀 Quick Start
1. Download `PowerLockGuard.exe`.
2. Double-click to run (no setup required).
3. Switch to **Work Watchdog**, choose **Sleep (Standby S3)**, activate the watchdog, and let your AI agents work while you sleep!

---

**Developed with ❤️ by [GMK Solution](https://gmksolution.com)**  
*Official Site:* [gmksolution.com](https://gmksolution.com) • *Source:* [github.com/GMKamrussama/PowerLockGuard](https://github.com/GMKamrussama/PowerLockGuard)
