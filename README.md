# PowerLockGuard v1.1.0 release candidate

**Release status:** v1.1 is under review in [PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1). The [published release](https://github.com/GMKamrussama/PowerLockGuard/releases/tag/v1.0.0) is still v1.0.0. This branch is not a signed or published v1.1 release.

Offline Windows CPU inactivity watchdog and confirmed charger-disconnect power actions. C# / WinForms, .NET Framework 4.0. By [GMK Solution](https://gmksolution.com) · [Source](https://github.com/GMKamrussama/PowerLockGuard) · MIT.

**CPU inactivity is a heuristic, not proof that an AI agent or build finished.** There is no verified Codex/Claude/Cursor completion integration. Network waits, approval prompts and low-CPU work can look inactive.

## Safety improvements

- High CPU remains activity telemetry. Automatic termination is disabled, including legacy INI configurations. Confirmed manual termination verifies PID + start time and may still lose unsaved work.
- Watchdog arming requires valid observed activity after enabling. No targets at startup, warming samples, partial observations and read failures cannot start a countdown.
- X/Alt+F4, Cancel, Esc and Space cancel the countdown. Returning activity/settings changes invalidate it; execution requires a fresh sample.
- The finalization follow-up requires keyboard/mouse inactivity for the configured grace before automatic watchdog action. Recent/unavailable input readings block automatic action. Explicit Execute Now is manual and still revalidates CPU/sample eligibility. Charger protection retains its independent unplug behavior.
- Charger Online/Offline/Unknown states and 500 ms offline debounce prevent Unknown/bounce from triggering. One confirmed unplug gives one request until reconnect.
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

For Work Watchdog, choose mode/action/cutoff/buffer and enable it while the selected workload is active. A fresh CPU baseline and observed activity are required before arming. Continuous healthy CPU inactivity and sufficient keyboard/mouse inactivity lead to a warning countdown. New input cancels automatic eligibility. Explicit Execute Now is a manual action: its own mouse click does not prevent it, but the final process sample must still be eligible. After one action attempt, including failure, the watchdog pauses; explicitly re-enable for the next workload and inspect the log.

Matching is case-insensitive **exact process name**, with optional `.exe` for custom targets. Broad substring matching was removed for safety: `python` does not include `pythonw`, and `code` does not include every name containing `code`. Ignore applies only to the selected instance, not every same-name process or a reused PID.

CPU is normalized across logical processors: one busy core on 32 logical processors is approximately 3.125%, below the default 3.5% cutoff. A workload that never crosses the cutoff cannot arm the watchdog. Unmatched work does not keep this watchdog active; low CPU never proves completion.

**Charger actions bypass the watchdog countdown** after confirmed unplug, preserving the intended feature. Disable Charger Guard when this behavior is unsuitable. Polling uses a 500 ms WinForms timer plus a 500 ms offline confirmation window; UI load may delay it. Unknown breaks confirmation. Resume/reconnect require fresh observations.

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

The latest verified hosted snapshot before this documentation update is `04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa`: **40 passed / 0 failed in each Debug and Release configuration**, including the user-input follow-up. Both solution builds and Build.bat passed in the [PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) and [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420). Historical snapshot `d829004` passed 37 tests per configuration; it is not the current HEAD. These counts are not a coverage percentage or hardware certification. See [PR checks](https://github.com/GMKamrussama/PowerLockGuard/pull/1/checks) for subsequent heads.

Read the [Codex case study](docs/CODEX_CASE_STUDY.md), [test matrix and exact evidence](docs/TEST_RESULTS.md), [Bengali tutorial](docs/TUTORIAL_BN.md), [regression suite](tests/SafetyTests.cs) and [release notes](RELEASE_NOTES_v1.1.0.md). Hardware power transitions, physical input-sensor behavior and live agent completion remain outside simulation evidence.

## Screenshots

New simulation renders are generated in the ignored test artifacts and are excluded from this source-only update. [Earlier watchdog](docs/screenshots/01_work_watchdog.png) and [charger](docs/screenshots/02_charger_guard.png) screenshots remain as legacy v1.0 references; their labels do not describe v1.1 behavior.

## বাংলা

PowerLockGuard CPU inactivity দেখে; Codex বা অন্য agent-এর কাজ শেষ হওয়া নিশ্চিত করে না। উচ্চ CPU বৈধ কাজ হতে পারে, তাই auto-kill বন্ধ। প্রথমে `--dry-run` ব্যবহার করুন—synthetic input ও recording executor থাকায় আসল sleep/shutdown/kill হবে না।

বাস্তবে চালানোর আগে Charger Guard দেখুন: নিশ্চিত disconnect হলে এটি watchdog countdown ছাড়াই action request করে। Watchdog enable-এর পর valid activity দেখে arm হয়; Unknown/failed sample idle সময় বাড়ায় না। Countdown বন্ধ করা, Cancel/Esc/Space, নতুন activity ও settings পরিবর্তনে pending action বাতিল হয়।

নতুন local follow-up-এ automatic watchdog action-এর আগে keyboard/mouse inactivity-ও যাচাই হয়। নতুন input বা input telemetry না পাওয়া গেলে automatic action আটকায়। Explicit Execute Now manual সিদ্ধান্ত; সেখানেও fresh CPU/sample যাচাই থাকে। Charger Guard-এর স্বাধীন unplug action এই নিয়মে পরিবর্তিত হয়নি।

এটি v1.1 release candidate; প্রকাশিত release এখনো v1.0.0। Historical `d829004`-এ hosted Windows test ৩৭টি করে পাস করেছিল। সর্বশেষ যাচাইকৃত `04a0fe4`-এর PR ও push CI-তে Debug/Release প্রতিটিতে ৪০টি পাস, ০ ব্যর্থ। পরের head-এর ফল [PR checks](https://github.com/GMKamrussama/PowerLockGuard/pull/1/checks)-এ দেখুন। বাস্তব power transition, physical sensor এবং Codex task completion যাচাই করা হয়নি। Windows sleep state, session বা token preservation নিশ্চিত করা হচ্ছে না। [বাংলা tutorial](docs/TUTORIAL_BN.md) ও [test evidence](docs/TEST_RESULTS.md) পড়ুন।

প্রথম সংস্করণ Antigravity-এর সহায়তায় তৈরি হয়েছিল বলে লেখক জানিয়েছেন। v1.1 safety refinement, regression tests ও simulation workflow Codex-এর সহায়তায় তৈরি হচ্ছে। ফল যাচাই করুন public PR ও CI evidence দেখে।
