# PowerLockGuard

Offline Windows power management for developer workloads, built with C# / WinForms and .NET Framework 4.0.

- **Charger Disconnect Protection:** independently detects confirmed AC-to-battery changes and requests the selected Sleep, Hibernate, Shutdown or Lock action after configurable debounce.
- **AI Work Watchdog:** monitors selected developer processes or explicitly registered finite tasks, holds a Windows SystemRequired request while work needs protection, and applies inactivity/grace checks plus a cancellable countdown before its configured action.

By [GMK Solution](https://gmksolution.com) · [Source](https://github.com/GMKamrussama/PowerLockGuard) · MIT.

## Install or build

The published binary release is [v1.0.0](https://github.com/GMKamrussama/PowerLockGuard/releases/tag/v1.0.0). The v1.1 core candidate is reviewed in [PR #3](https://github.com/GMKamrussama/PowerLockGuard/pull/3); no v1.1 release or Microsoft Store update is published by that PR. Historical v1.0 release-note claims do not describe the current safety policy.

For the candidate, build this source in a writable directory using the installed Framework compiler:

```bat
Build.bat --no-pause
```

Alternatively use `PowerLockGuard.sln` with MSBuild/Visual Studio and .NET 4.0 targeting references. The full Framework is used, including System.Web.Extensions. Builds write a root executable, so use a separate source checkout to preserve an approved binary. No compiled candidate is committed to Git.

## Try a recording-only demonstration

```powershell
.\PowerLockGuard.exe --dry-run
```

The title shows SIMULATION. Process/power inputs are synthetic, actions are recorded, and startup/configuration are not changed. Enable Work Watchdog to restart the synthetic workload; choose a one-minute buffer or preview its warning dialog. `--simulate all|watchdog|charger [output-path]` provides a headless virtual-time trace; `--capture-screenshots` renders the simulation UI.

## Use Charger Guard

An unflagged invocation starts live operation. The supplied configuration enables Charger Guard, disables Work Watchdog and disables Windows startup; choose the charger action before leaving it running.

Debounce is configurable from 100 to 10000 ms (default 500 ms). A 500 ms UI poll and power-status messages supply readings; UI scheduling adds latency. Fresh Online arms protection, sustained Offline produces one request, and reconnect re-arms. Unknown or invalid telemetry cancels confirmation and requires fresh Online.

**Charger actions bypass the watchdog countdown**, including warning previews. Watchdog-only settings do not disarm protection. Disable Charger Guard if that behavior is unsuitable.

## Use Work Watchdog

Choose one of two evidence modes:

| Mode | How it works | Suitable scope |
|---|---|---|
| CPU heuristic | Fresh valid activity arms monitoring; continuous healthy CPU inactivity and user inactivity can start the warning countdown | Existing matched developer processes; low CPU is not proof of completion |
| Registered tasks | Retained task/process lifecycle establishes running work; successful finite completion, drained output, healthy observations and configured grace/user inactivity gate the countdown | One explicit owned native command or opt-in Codex JSON session |

Configure target/action/cutoff/grace, then enable monitoring. Low-CPU running registered work remains protected. Unknown task status blocks automatic action; the unknown-work keep-awake policy is configurable. No registered task means no confirmed completion in registered mode.

`--watch-command <absolute-native-exe-path> [arguments...]` launches one finite command. `--watch-codex <absolute-codex-exe-path> [exec-arguments...]` prepends `exec --json`. Both are live opt-in paths and enable registered-task monitoring. Native .exe paths are required; shell wrappers are unsupported. Child arguments are forwarded and never interpreted as application simulation options. See [provider contracts and limitations](docs/CORE_FUNCTIONALITY.md).

New work, recent/unavailable user input, settings changes or uncertain samples cancel automatic eligibility. X/Alt+F4, Cancel, Esc and Space cancel the countdown. Explicit Execute Now is manual and still revalidates workload/sample eligibility. After an action attempt the watchdog pauses for review/re-enabling. SystemRequired protection is released after completion, disable/exit and before intended power transitions.

Matching is case-insensitive exact process name, optionally ending in .exe; ignored processes are identified by PID plus start time. CPU is normalized across logical processors. Unregistered IDE windows, network waits and detached jobs do not supply authoritative completion. Automatic CPU-based termination is disabled; manual termination remains confirmed and identity-checked.

## Settings and supported behavior

`config.ini` preserves legacy keys, including IsEnabled/AutoDev migration. Invalid safety settings disable the affected guard and log warnings; legacy auto-kill remains false. Atomic saves report failures and require a writable application directory. Settings & Logs exports the latest 1000 events; review process identifiers before sharing. Runtime has no automatic upload or telemetry.

Windows determines supported sleep states and policy overrides. Native errors are logged; S3, network/session preservation and zero battery drain are not guarantees. Sleep PC Now and End Selected Process require confirmation. The original approved build's charger/input/Sleep-Resume result is **OWNER-TESTED: SUCCESS**; candidate-specific measurements and remaining acceptance are in [TEST_RESULTS](docs/TEST_RESULTS.md).

## Verification and technical documentation

Application source `8b7fdd4` passed **103 local tests in Debug and 103 in Release**, plus both solution builds, Build.bat and safe CLI. Current hosted results and fresh review belong to the exact head shown in [PR #3 checks](https://github.com/GMKamrussama/PowerLockGuard/pull/3/checks). [TEST_RESULTS](docs/TEST_RESULTS.md) is the canonical record for hashes, historical hosted runs, Stage 1 measurements and validation limits.

To verify safely in a disposable checkout with MSBuild on PATH:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1 -CheckMSBuild
```

The interlock blocks production action adapters; automated tests use recording/fake actions. GitHub Actions runs this on Windows and uploads test/build evidence, not a published release.

- [Architecture and supported providers](docs/CORE_FUNCTIONALITY.md)
- [Validation record](docs/TEST_RESULTS.md) and [hardware acceptance checklist](docs/WINDOWS_ACCEPTANCE.md)
- [Codex case study](docs/CODEX_CASE_STUDY.md) and [বাংলা tutorial](docs/TUTORIAL_BN.md)
- [v1.1 candidate notes](RELEASE_NOTES_v1.1.0.md) and [historical v1.0 notes](RELEASE_NOTES_v1.0.0.md)

Earlier [watchdog](docs/screenshots/01_work_watchdog.png) and [charger](docs/screenshots/02_charger_guard.png) screenshots show v1.0; current simulation renders are generated by verification.

## বাংলা

PowerLockGuard-এর দুটি মূল কাজ: charger খুললে নির্বাচিত protection action, এবং developer কাজ চলাকালে system keep-awake ও inactivity/grace শেষে cancellable power action। CPU কম হলেই AI কাজ শেষ—এমন দাবি নেই; auto-kill বন্ধ। নিরাপদ প্রদর্শনে `--dry-run` ব্যবহার করুন। Enabled Charger Guard live warning preview-এর সময়ও স্বাধীনভাবে কাজ করে।

আগের approved build মালিক নিজে পরীক্ষা করেছেন: **OWNER-TESTED: SUCCESS**। বর্তমান application source `8b7fdd4`-এ review fixes-সহ স্থানীয় Debug/Release প্রতিটিতে **১০৩টি পাস, ০ ব্যর্থ**। Hosted ও physical evidence নির্দিষ্ট commit/hash অনুসারে [validation record](docs/TEST_RESULTS.md)-এ আছে। মূল সংস্করণ Antigravity-এর সহায়তায় তৈরি, পরের audit/refinement/tests-এ Codex ব্যবহৃত হয়েছে।
