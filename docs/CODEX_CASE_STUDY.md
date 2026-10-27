# Improving a Real Windows Utility with OpenAI Codex: PowerLockGuard v1.1

Prepared 27 October 2026 for the owner-approved source/documentation update to PR #1. The original application was developed with Antigravity assistance, as reported by its owner. This case study concerns subsequent Codex-assisted maintenance. It does not imply OpenAI endorsement. Release publication remains separate.

## 1. Problem

Developers leave coding agents, builds and Python jobs running overnight. PowerLockGuard is an offline C# WinForms utility that observes selected Windows processes and can request a power action after an inactivity buffer. An independent Charger Guard reacts to confirmed AC disconnection. These decisions can interrupt useful work, making false assumptions more consequential than ordinary UI defects.

## 2. Original implementation

The reviewed [baseline commit](https://github.com/GMKamrussama/PowerLockGuard/commit/130998f7037cf3d25501f4463dc7cd80bc673c41) used .NET Framework 4.0. Its CPU thresholds classified high load as stuck; user absence could authorize automatic termination. Normal computation could therefore satisfy a destructive rule. Closing a countdown did not default to cancellation. Unknown charger status became a boolean false, and sampling failures could appear as low CPU. Process histories and termination targets lacked start-time identity.

These are source-level findings, not claims that a particular user lost data. The earlier application's Antigravity origin is distinct from the Codex work described below.

## 3. Why Codex was introduced

The owner asked Codex to audit the source before executing the original binary, separate confirmed defects from runtime hypotheses, and prepare small safety changes. The recorded workflow read the source, traced action entry points, edited a separate worktree, compiled candidates, added regression assertions, inspected synthetic UI renders and checked Windows CI artifacts. The owner supplied the intended overnight/charger use case and required private preparation material to stay local.

No reconstructed prompt is presented as an original transcript. Private session records support provenance but are not public evidence or signed certification. The public evidence is the code diff, tests, CI and an independently visible Codex GitHub review record.

## 4. Technical improvements

`SafetyCore.cs` keeps CPU as telemetry. Legacy automatic-kill APIs return without terminating; `ActionDispatcher` rejects non-manual termination. `ProcessIdentity` pairs PID with UTC start time. Confirmed manual termination validates a retained process handle and rejects replacement, exit, unavailable identity and self-targeting.

`WorkWatchdogEngine` separates healthy observations from warm-up/failure, uses a monotonic clock and requires observed activity before arming. No matching process at startup is not completion. `SafetyCountdownForm` defaults to cancellation; the form and final sample recheck block resumed work or changed settings. `ChargerGuard` distinguishes Online/Offline/Unknown and confirms Offline over at least 500 ms before a one-shot request.

Recording-only executors power `--dry-run`, CLI scenarios and screenshot mode. Verification sets an interlock that prevents production action adapters. Configuration is validated and atomically replaced; errors are surfaced. Shutdown no longer force-closes applications. The WinForms architecture and .NET 4.0 API target remain.

A final local audit found that the existing keyboard/mouse helper was not used in watchdog eligibility. Local implementation commit `e45eed7e46eca1a9fe1b8b6b8048927fab26a1b9` adds an injectable input source: recent or unavailable input readings block automatic action, and resumed input cancels its countdown. Explicit Execute Now remains manual while still validating CPU/sample state. The approved PR update integrates this fix as 73ab1e9; check the latest PR checks for hosted validation. A UI-test fixture was also corrected to use the same target selection as the form, so unrelated selection resets cannot make a cancellation assertion pass accidentally.

## 5. Before and after

| Decision | Baseline | v1.1 candidate / local follow-up |
|---|---|---|
| High CPU | Could label stuck and authorize kill | Activity only; no CPU-based automatic kill |
| Process identity | PID | PID + start time and retained handle check |
| Failed observation | Could advance idle | Blocks eligibility and resets continuous idle |
| Countdown close | Could continue toward action | Cancel by default |
| User input | Helper unused in eligibility | Local follow-up blocks/cancels automatic action |
| Charger Unknown | Treated as not online | Breaks unplug confirmation |
| Demonstration | Live timers could remain active | Synthetic/record-only modes and test interlock |
| Validation | No regression suite in reviewed baseline | Historical hosted 37/configuration; latest verified hosted snapshot 40/configuration |

## 6. Testing and evidence

- [PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1) remains open against master.
- Latest verified hosted snapshot before this documentation update: [04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa](https://github.com/GMKamrussama/PowerLockGuard/commit/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa).
- [Latest verified PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) and [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420): **40 passed, 0 failed in Debug and 40 passed, 0 failed in Release**, including the user-input follow-up; both solution builds and Build.bat passed. Each downloaded configuration report from both runs contains 40 PASS entries. No real power/kill action was performed.
- Historical snapshot d829004: [earlier PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495736646) and [earlier push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495731233) passed 37 tests per configuration. This is historical evidence, not the current HEAD.
- [Test source at 04a0fe4](https://github.com/GMKamrussama/PowerLockGuard/blob/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa/tests/SafetyTests.cs) and [branch test source](../tests/SafetyTests.cs) identify the tested scope. This update changes documentation only; [PR checks](https://github.com/GMKamrussama/PowerLockGuard/pull/1/checks) track subsequent heads.
- [Codex GitHub review summary](https://github.com/GMKamrussama/PowerLockGuard/pull/1#issuecomment-6440439088) reports completed code review with no findings for d829004. It is not human approval, a security-review completion or proof of absence of defects.
- The hosted `safety-evidence` artifact contains test reports, CLI traces, renders and candidate hashes. These were downloaded and inspected; artifacts may require sign-in and have limited retention.
- [TEST_RESULTS.md](TEST_RESULTS.md) records local follow-up results and limits. Synthetic countdown renders are generated in ignored test artifacts; no new binary image is included in this source-only update.

No real power or process-termination operation was used for validation. Compilation, fake-executor behavior and hardware validation are separate claims.

## 7. What Codex actually contributed

Codex assisted source inspection, proposed and implemented safety changes, wrote tests/build automation/documentation, executed permitted checks and inspected their output. The owner set the use case, constraints, privacy rules and publication boundaries. Owner review, actual introduction recording and publication decisions remain human responsibilities. The original application is not reattributed to Codex, and Git author metadata alone does not identify which tool produced a change.

## 8. Known limitations

Low CPU can mean network waits, approval prompts or low-intensity useful work. There is no Codex task-completion protocol integration. Exact name matching can miss versioned binaries, hosted extensions and child work. User-input observations are session-local; the physical Windows input adapter was compiled/reviewed but exercised through fakes in tests.

Charger actions intentionally bypass the watchdog warning. Polling can be delayed. S3/Modern Standby, actual suspend/hibernate/shutdown outcomes, privileges, battery usage, network continuation and token preservation are not established. Long-duration resource soak testing and signing/Store packaging are also unverified. Legacy .NET 4.0 API-target compilation on a current installed Framework runtime is not testing every old Windows/runtime combination.

## 9. Lessons for other developers

Trace every destructive entry point before running a utility. Model Unknown separately from Idle. Check identity at execution, not just discovery. Make cancellation the default and revalidate after UI delays. Use injected clocks/readings/executors so failure paths are testable without invoking the operating system action. Include positive-path assertions: a cancellation test can pass for the wrong reason if fixtures change unrelated selection state. Attach counts to exact source snapshots and keep private application records outside Git.

## 10. How to reproduce

On Windows in the reviewed checkout:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1
# With MSBuild on PATH, add -CheckMSBuild.
& .\bin\Verification\Release\PowerLockGuard.exe --dry-run
```

The GUI must say SIMULATION. Inputs are synthetic; no startup/config mutation occurs. Enable the watchdog to restart its fake workload or preview Settings & Logs → Test Warning Dialog. Inspect `artifacts/Debug/test-results.txt`, `artifacts/Release/test-results.txt` and their CLI logs. Do not double-click an unflagged executable for this demonstration: that starts live mode. The public source/documentation update is owner-approved; PR #1 remains the review boundary.
