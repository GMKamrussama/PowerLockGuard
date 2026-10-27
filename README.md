# PowerLockGuard v1.1.0 release candidate

**Local restoration branch:** unpublished core-functionality work based on `c827aa8` adds Windows system-awake leases, suspend privilege handling, independent configurable charger debounce and bounded registered-task monitoring. Local Debug/Release validation: **66 passed / 0 failed each**, both solution builds and Build.bat passed. No new hosted CI, release, physical transition or production Codex session validates this branch. See [implementation and capability matrix](docs/CORE_FUNCTIONALITY.md), [current/historical results](docs/TEST_RESULTS.md) and [owner acceptance plan](docs/WINDOWS_ACCEPTANCE.md). The merged/hosted records below describe the earlier public snapshot.

**Release status:** v1.1 source was merged into `master` through [PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1) at [70cd1b8ac8ad2441c6ac75b87124aac500ce3f36](https://github.com/GMKamrussama/PowerLockGuard/commit/70cd1b8ac8ad2441c6ac75b87124aac500ce3f36). [Post-merge Windows CI passed](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147). A v1.1 binary release has not been published; the [published release](https://github.com/GMKamrussama/PowerLockGuard/releases/tag/v1.0.0) is still v1.0.0. Automated validation does not verify physical power behavior, physical sensors or Codex task completion.

Offline Windows CPU inactivity watchdog and confirmed charger-disconnect power actions. C# / WinForms, .NET Framework 4.0. By [GMK Solution](https://gmksolution.com) · [Source](https://github.com/GMKamrussama/PowerLockGuard) · MIT.

**CPU inactivity is a heuristic, not proof that an AI agent or build finished.** There is no verified Codex/Claude/Cursor completion integration. Network waits, approval prompts and low-CPU work can look inactive.

## Safety improvements

- High CPU remains activity telemetry. Automatic termination is disabled, including legacy INI configurations. Confirmed manual termination verifies PID + start time and may still lose unsaved work.
- CPU-heuristic arming requires valid observed activity after enabling. Registered-task mode instead requires authoritative finite-command completion plus grace and healthy process observations. No registered task, warming/partial samples and unavailable status cannot start an automatic countdown in that mode.
- X/Alt+F4, Cancel, Esc and Space cancel the countdown. Returning activity/settings changes invalidate it; execution requires a fresh sample.
- The finalization follow-up requires keyboard/mouse inactivity for the configured grace before automatic watchdog action. Recent/unavailable input readings block automatic action. Explicit Execute Now is manual and still revalidates CPU/sample eligibility. Charger protection retains its independent unplug behavior.
- Charger Online/Offline/Unknown states and configurable offline debounce (default 500 ms) prevent Unknown/bounce from triggering. One confirmed unplug gives one request until reconnect. Watchdog-only saves, countdown cleanup and warning previews do not disarm it.
- With Watchdog enabled, system-only keep-awake requests protect confirmed running/waiting tasks and eligible heuristic workloads. Requests are released on completion/disable/exit and before intended Sleep/Hibernate/Shutdown. Native failure is logged; hardware effectiveness is unverified.
- Shutdown does not force-close applications. Unsupported actions are rejected; failures and submitted requests are logged separately.
- Monotonic timing, instance-specific ignore, validated/atomic settings, bounded/exportable logs and deterministic resource disposal.

## Recording-only demonstration

Build first, then run:

```powershell
.\PowerLockGuard.exe --dry-run
```

The title says **SIMULATION**. Inputs are synthetic: a simulated Codex process is busy for eight seconds, then CPU-inactive; charger defaults to Unknown. All manual, tray and automatic actions use a recording executor. Startup shortcuts and production configuration are unchanged.

In the dry-run UI, enabling the watchdog restarts the synthetic workload. Choose a one-minute buffer for a shorter demonstration. Use Settings & Logs → Test Warning Dialog to preview cancellation without waiting for the buffer. The current Windows session's real user-input sensor is not read in simulation; the input policy is tested with scripted readings.

For deterministic headless evidence:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
$demo = Start-Process .\PowerLockGuard.exe -ArgumentList '--simulate all' -WindowStyle Hidden -Wait -PassThru
$demo.ExitCode
Get-Content .\artifacts\simulation.log
```

Scenarios: `all`, `watchdog`, `charger`; an optional third argument specifies the output path. Unknown scenarios exit 2. The headless watchdog demonstration compresses the inactivity policy into virtual time; countdown UI is tested separately. `--capture-screenshots` also initializes simulation; its example screenshots are synthetic, not evidence of real task completion.

The `POWERLOCKGUARD_NO_NATIVE_ACTIONS=1` interlock forbids production action adapters. Keep it set during verification; remove it only when intentionally starting a live application process.

## Live operation

Without simulation flags the app uses real process observations and Windows power APIs. The shipped config enables Charger Guard, disables Work Watchdog and disables Windows startup. Review charger protection before leaving the app running.

For legacy **CPU heuristic**, choose process mode/action/cutoff/buffer and enable it while the selected workload is active. A fresh CPU baseline and observed activity are required before arming. Continuous healthy CPU inactivity and sufficient keyboard/mouse inactivity lead to a warning countdown; uninstrumented API waits can still look idle. For **Registered tasks**, use an explicit owned finite command or Codex JSON session as described in [CORE_FUNCTIONALITY](docs/CORE_FUNCTIONALITY.md). A running task remains protected even at low CPU; successful completion requires the full configured grace and user inactivity. No registered task means no confirmed completion. Unknown status blocks automatic action; its keep-awake policy is selectable. New work/input cancels automatic eligibility. Explicit Execute Now remains manual and still checks workload/sample eligibility. After one action attempt, including failure, the watchdog pauses; explicitly re-enable and inspect the log.

Matching is case-insensitive **exact process name**, with optional `.exe` for custom targets. Broad substring matching was removed for safety: `python` does not include `pythonw`, and `code` does not include every name containing `code`. Ignore applies only to the selected instance, not every same-name process or a reused PID.

CPU is normalized across logical processors: one busy core on 32 logical processors is approximately 3.125%, below the default 3.5% cutoff. A CPU-only workload that never crosses the cutoff cannot arm the legacy heuristic. Registered owned tasks do not need to cross that cutoff to establish activity. Unmatched unregistered work may be missed; low CPU never proves completion.

**Charger actions bypass the watchdog countdown**, including a warning preview, after confirmed unplug. Disable Charger Guard when this behavior is unsuitable. Polling uses a 500 ms WinForms timer plus configurable debounce (100–10000 ms; default 500 ms), with a power-status message as an additional observation opportunity; UI load may delay it. Unknown breaks confirmation. Watchdog-only configuration errors do not disable a valid charger configuration. Resume/reconnect require fresh observations.

Sleep state depends on Windows/hardware. S3, zero battery drain, preserved network requests and token loss prevention are not guaranteed. API success is not independent physical-state verification. Hibernate/privilege errors appear in the log. Sleep PC Now and End Selected Process require confirmation.

Settings & Logs can export the latest 1,000 local events. Logs can contain process names/IDs; review before sharing. There is no automatic upload or telemetry. Build-time reference downloads are separate from offline runtime behavior.

`config.ini` preserves v1 keys, including `IsEnabled`/`AutoDev` migration. Malformed safety values disable automatic guards and log warnings; automatic-kill is always false. Save uses atomic replacement and reports failures. The application directory must be writable for persistence; failed saves leave only in-memory changes.

## Build and verification

```bat
Build.bat --no-pause
```

Or use `PowerLockGuard.sln` with Visual Studio and .NET 4.0 targeting references. No framework migration or new runtime package dependency is introduced.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1
```

Add `-CheckMSBuild` with MSBuild on PATH to check solution builds too. The script downloads a pinned SHA256-verified .NET 4.0 reference package, builds Debug/Release, runs assertions and simulation-only WinForms tests, exercises headless CLI and verifies Build.bat. It does not run the original binary or real kill/power actions.

GitHub Actions runs this on `windows-2022`, including solution builds. The `safety-evidence` artifact contains reports, scenario logs, a UI render, candidate executables/config/icon and SHA256 hashes. Failures return nonzero. The stale source-controlled v1.0 executable is removed from this branch; build locally or inspect CI candidates. Artifacts are not a signed/published release.

## Tested behavior and evidence

The merged snapshot `70cd1b8ac8ad2441c6ac75b87124aac500ce3f36` passed [post-merge Windows CI](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147): **40 passed / 0 failed in each Debug and Release configuration**, both solution builds and Build.bat passed, with the native-action interlock enabled. Historical snapshot `d829004` passed 37 tests per configuration; `04a0fe4` passed 40 in its [PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) and [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420). These counts are not a coverage percentage or hardware certification. See [Windows workflow runs](https://github.com/GMKamrussama/PowerLockGuard/actions/workflows/windows-safety.yml) for subsequent heads.

Read the [Codex case study](docs/CODEX_CASE_STUDY.md), [test matrix and exact evidence](docs/TEST_RESULTS.md), [Bengali tutorial](docs/TUTORIAL_BN.md), [regression suite](tests/SafetyTests.cs) and [release notes](RELEASE_NOTES_v1.1.0.md). Hardware power transitions, physical input-sensor behavior and live agent completion remain outside simulation evidence.

## Screenshots

New simulation renders are generated in the ignored test artifacts and are excluded from this source-only update. [Earlier watchdog](docs/screenshots/01_work_watchdog.png) and [charger](docs/screenshots/02_charger_guard.png) screenshots remain as legacy v1.0 references; their labels do not describe v1.1 behavior.

## বাংলা

PowerLockGuard CPU inactivity দেখে; Codex বা অন্য agent-এর কাজ শেষ হওয়া নিশ্চিত করে না। উচ্চ CPU বৈধ কাজ হতে পারে, তাই auto-kill বন্ধ। প্রথমে `--dry-run` ব্যবহার করুন—synthetic input ও recording executor থাকায় আসল sleep/shutdown/kill হবে না।

স্থানীয় restoration branch-এ system keep-awake, suspend privilege handling এবং registered finite task/Codex JSON event parser যোগ হয়েছে। Debug/Release প্রতিটিতে ৬৬টি test পাস, ০ ব্যর্থ; বাস্তব Codex session বা physical Sleep যাচাই হয়নি। CPU heuristic-এর সীমাবদ্ধতা রয়ে গেছে। Registered tasks mode-এ চলমান task কম CPU-তেও protected থাকে; কাজ শেষের পরে grace ও user inactivity লাগে। Warning preview-এর সময়ও enabled Charger Guard সক্রিয় থাকে। [নতুন কাজের বিবরণ](docs/CORE_FUNCTIONALITY.md) এবং [অনুমতি সাপেক্ষ hardware plan](docs/WINDOWS_ACCEPTANCE.md) দেখুন। এই স্থানীয় পরিবর্তন push/release হয়নি।

বাস্তবে চালানোর আগে Charger Guard দেখুন: নিশ্চিত disconnect হলে এটি watchdog countdown ছাড়াই action request করে। Watchdog enable-এর পর valid activity দেখে arm হয়; Unknown/failed sample idle সময় বাড়ায় না। Countdown বন্ধ করা, Cancel/Esc/Space, নতুন activity ও settings পরিবর্তনে pending action বাতিল হয়।

Merged v1.1-এ automatic watchdog action-এর আগে keyboard/mouse inactivity-ও যাচাই হয়। নতুন input বা input telemetry না পাওয়া গেলে automatic action আটকায়। Explicit Execute Now manual সিদ্ধান্ত; সেখানেও fresh CPU/sample যাচাই থাকে। Charger Guard-এর স্বাধীন unplug action এই নিয়মে পরিবর্তিত হয়নি।

v1.1 source `70cd1b8` commit-এ `master`-এ merge হয়েছে; v1.1 binary release প্রকাশিত হয়নি, প্রকাশিত release এখনো v1.0.0। [Post-merge Windows CI](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147)-তে Debug/Release প্রতিটিতে ৪০টি পাস, ০ ব্যর্থ। Historical `d829004`-এ ৩৭টি এবং `04a0fe4`-এ ৪০টি করে পাস করেছিল। বাস্তব power transition, physical sensor এবং Codex task completion যাচাই করা হয়নি। Windows sleep state, session বা token preservation নিশ্চিত করা হচ্ছে না। [বাংলা tutorial](docs/TUTORIAL_BN.md) ও [test evidence](docs/TEST_RESULTS.md) পড়ুন।

প্রথম সংস্করণ Antigravity-এর সহায়তায় তৈরি হয়েছিল বলে লেখক জানিয়েছেন। Merged v1.1 safety refinement, regression tests ও simulation workflow Codex-এর সহায়তায় তৈরি হয়েছে। ফল যাচাই করুন public PR ও CI evidence দেখে।
