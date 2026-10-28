# Improving a Real Windows Utility with OpenAI Codex: PowerLockGuard v1.1

PowerLockGuard is an existing Windows utility with Charger Disconnect Protection and Work Watchdog. Subsequent Codex-assisted work adds system-awake leases, suspend privileges, independent charger handling and bounded task observations. Current application source `688e24d19e2e683006033110d0adbef5ae2d4273` passes **97 local tests per Debug/Release configuration**, solution builds, Build.bat and CLI. PR #3 includes fixes for child-argument mutex selection and cumulative parser state. See [canonical evidence](TEST_RESULTS.md), [architecture](CORE_FUNCTIONALITY.md) and [PR #3](https://github.com/GMKamrussama/PowerLockGuard/pull/3) for exact-head CI/review status.

Updated 28 October 2026. Original development used Antigravity assistance, as reported by the owner; this case study concerns subsequent Codex-assisted maintenance, without implying OpenAI endorsement. Earlier safety source merged in PR #1 and documentation in PR #2. Core restoration and the reviewed follow-ups are presented in PR #3. Historical hosted evidence below does not validate the newer candidate; a v1.1 binary release has not been published.

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

`WorkWatchdogEngine` separates healthy observations from warm-up/failure, uses a monotonic clock and requires observed activity before arming. No matching process at startup is not completion. `SafetyCountdownForm` defaults to cancellation; the form and final sample recheck block resumed work or changed settings. `ChargerGuard` distinguishes Online/Offline/Unknown and confirms Offline over the configured debounce (default 500 ms) before a one-shot request. Unknown or invalid enums reset confirmation and require fresh Online.

Recording-only executors power `--dry-run`, CLI scenarios and screenshot mode. Verification sets an interlock that prevents production action adapters. Configuration is validated and atomically replaced; errors are surfaced. Shutdown no longer force-closes applications. The WinForms architecture and .NET 4.0 API target remain.

A final local audit found that the existing keyboard/mouse helper was not used in watchdog eligibility. Local implementation commit `e45eed7e46eca1a9fe1b8b6b8048927fab26a1b9` adds an injectable input source: recent or unavailable input readings block automatic action, and resumed input cancels its countdown. Explicit Execute Now remains manual while still validating CPU/sample state. The merged PR integrates this fix as 73ab1e9; the post-merge run below verifies its automated tests. A UI-test fixture was also corrected to use the same target selection as the form, so unrelated selection resets cannot make a cancellation assertion pass accidentally.

## 5. Before and after

| Decision | Baseline | Merged v1.1 |
|---|---|---|
| High CPU | Could label stuck and authorize kill | Activity only; no CPU-based automatic kill |
| Process identity | PID | PID + start time and retained handle check |
| Failed observation | Could advance idle | Blocks eligibility and resets continuous idle |
| Countdown close | Could continue toward action | Cancel by default |
| User input | Helper unused in eligibility | Merged follow-up blocks/cancels automatic action |
| Charger Unknown | Treated as not online | Breaks unplug confirmation |
| Demonstration | Live timers could remain active | Synthetic/record-only modes and test interlock |
| Validation | No regression suite in reviewed baseline | Historical hosted 37/configuration; latest verified hosted snapshot 40/configuration |

## 6. Testing and evidence

- [PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1) merged into `master` at [70cd1b8ac8ad2441c6ac75b87124aac500ce3f36](https://github.com/GMKamrussama/PowerLockGuard/commit/70cd1b8ac8ad2441c6ac75b87124aac500ce3f36). Its tree matches approved PR head `948a680226dadb89ed8cacfa930132ce1c9ffffb`.
- [Post-merge Windows run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147): **40 passed, 0 failed in Debug and 40 passed, 0 failed in Release**; both solution builds and Build.bat passed with the native-action interlock enabled. Each downloaded configuration report contains 40 PASS entries. No real power/kill action was performed.
- Earlier 40-test snapshot [04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa](https://github.com/GMKamrussama/PowerLockGuard/commit/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa): [PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) and [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420) passed both configurations, solution builds and Build.bat. Each downloaded configuration report from both runs contains 40 PASS entries, including the user-input follow-up.
- Historical snapshot d829004: [earlier PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495736646) and [earlier push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495731233) passed 37 tests per configuration. This is historical evidence, not the current HEAD.
- [Test source at 04a0fe4](https://github.com/GMKamrussama/PowerLockGuard/blob/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa/tests/SafetyTests.cs) and [branch test source](../tests/SafetyTests.cs) identify the tested scope. Those historical finalization edits changed documentation only; this core review also changes application/test sources. [Windows workflow runs](https://github.com/GMKamrussama/PowerLockGuard/actions/workflows/windows-safety.yml) track subsequent heads.
- The [Codex GitHub review summary](https://github.com/GMKamrussama/PowerLockGuard/pull/1#issuecomment-6440439088) is a mutable latest-status comment. It now records completed review with no findings for approved head `948a680226dadb89ed8cacfa930132ce1c9ffffb`, following the earlier review of d829004. It is not human approval, a security-review completion or proof of absence of defects.
- The hosted `safety-evidence` artifact contains test reports, CLI traces, renders and candidate hashes. These were downloaded and inspected; artifacts may require sign-in and have limited retention.
- [TEST_RESULTS.md](TEST_RESULTS.md) records historical and post-merge results and limits. Synthetic countdown renders are generated in ignored test artifacts; no new binary image is included in this source-only update.

Automated candidate validation used no real power-state or process-termination operation. Later Stage 1 permitted native keep-awake lease calls only. Original-build owner-tested charger/input/Sleep-Resume success is separate evidence; compilation, fake actions, native lease visibility and physical transitions are distinct claims.

## 7. What Codex actually contributed

Codex assisted source inspection, proposed and implemented safety changes, wrote tests/build automation/documentation, executed permitted checks and inspected their output. The owner set the use case, constraints, privacy rules and publication boundaries. Owner review, actual introduction recording and publication decisions remain human responsibilities. The original application is not reattributed to Codex, and Git author metadata alone does not identify which tool produced a change.

## 8. Known limitations

Low CPU can mean network waits, approval prompts or useful work. The current implementation includes an opt-in bounded Codex JSON lifecycle parser, with fixture evidence only, not production compatibility or arbitrary IDE completion. Exact name matching can miss extensions and child work. Input is session-local. Original-build input cancellation is owner-tested; newer-candidate genuine input cancellation lacks instrumented evidence.

Charger actions bypass the watchdog warning; polling can be delayed. Owner reports successful original-build charger and Sleep/Resume behavior without instrumented latency measurements. The 6568f0d candidate has native lease request visibility/release evidence; later candidate physical transitions, battery behavior, network continuation and token preservation remain unverified. Long-duration soak, signing/Store packaging and a broad OS/runtime matrix remain outside the evidence.

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

The GUI must say SIMULATION. Inputs are synthetic; no startup/config mutation occurs. Enable the watchdog to restart its fake workload or preview Settings & Logs → Test Warning Dialog. Inspect `artifacts/Debug/test-results.txt`, `artifacts/Release/test-results.txt` and their CLI logs. Do not double-click an unflagged executable for this demonstration: that starts live mode. PR #1 is the merged review record; it does not publish a v1.1 binary release.
