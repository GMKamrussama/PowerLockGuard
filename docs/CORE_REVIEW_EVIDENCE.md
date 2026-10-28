# Core implementation review evidence

Prepared 28 October 2026 for `feature/core-restoration-review`, based on public master `c827aa8196ab3e4e086ffc27a62e6b3746782fe5`. The application, tests and build sources are unchanged from `6568f0de4aa6981e2438a0528d83c0a3d6ab5288`; later review preparation changes documentation only. This branch is local, not pushed, merged, released or deployed to Microsoft Store. Local results are not hosted CI results.

PowerLockGuard is an existing functional offline Windows utility. Charger Disconnect Protection independently requests its configured action after confirmed unplug and debounce. Work Watchdog protects selected active work with a Windows SystemRequired request and applies configured work, inactivity, grace and countdown checks before automatic action. See [implementation and provider scope](CORE_FUNCTIONALITY.md).

## Two distinct builds

| Build | Source | SHA256 | Validation |
|---|---|---|---|
| Original approved build | Application implementation `4fc957feabcaa94db6b31fb2e3560f631e8b54e5`; documentation snapshot `f89c8f754c0dfa2d5063d22059765c5ba8fec40f` has identical application sources | `97534A0A81E41374174AEDFE17394B0CF7FA8E0D74BB0D386F4DA28E5E48F59F` | **OWNER-TESTED: SUCCESS**. Owner confirms charger behavior, input cancellation and Sleep/Resume. This is owner-reported real-world acceptance, without instrumented timings or native API traces. |
| Defensive-fix candidate | `6568f0de4aa6981e2438a0528d83c0a3d6ab5288` | `2A42CA0E7103BEDD4437859EFBAAF4E776BD24585E1C0A32201D5D7D5B449953` | Local 76-test suites plus the instrumented Windows subsets below. Unknown and invalid charger enums reset pending confirmation and require fresh Online before re-arming. |

Both binaries remain separate and unchanged. The approved original was not executed or rebuilt during candidate validation. It retains the invalid-enum provider-boundary defect fixed in the candidate; an injected enum failure does not show that Windows emitted such a value. Owner acceptance of the original is retained without requiring that build to be tested again. It does not automatically certify the newer hash. Neither candidate hash is a signature or a deterministic-build guarantee.

## Safe automated verification

On 28 October 2026, the candidate source was exported to an isolated source-only directory so `Build.bat` could not overwrite the approved executable. All tracked files in that tested export matched commit `6568f0d` by Git blob hash.

| Gate | Actual result |
|---|---|
| Debug assertions, policy, native-fake and WinForms tests | **76 passed / 0 failed** |
| Release assertions, policy, native-fake and WinForms tests | **76 passed / 0 failed** |
| Debug and Release solution rebuilds | Both passed, zero warnings/errors |
| Unmodified `Build.bat --no-pause` in isolated source root | Passed |
| Debug/Release and copied-candidate `--simulate all` | Passed; native actions invoked: 0 |

The 40-test public baseline became 66 tests at `4fc957f`, then 76 at `6568f0d`. The ten newest tests cover valid disconnect/reconnect, Unknown/-1/99, lost telemetry during debounce, recovery, repeated events, threshold boundaries and UI recording dispatch. The 66-test stage is historical local evidence; 37/40-test hosted runs belong to older snapshots in [TEST_RESULTS](TEST_RESULTS.md).

Environment: Windows 10 Home Single Language 10.0.19045 x64; four logical processors; compiler 4.8.9232.0; CLR 4.0.30319.42000; installed .NET release registry value 533325; PowerShell 5.1.19041.6456. Target references remain .NET Framework 4.0 from SHA256-pinned reference package 1.0.3. This is not testing an original .NET 4.0 runtime or an exhaustive Windows matrix.

Reproduction in a disposable source checkout, with Framework MSBuild on PATH:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1 -CheckMSBuild
```

The script creates a root build output: use a separate source checkout if preserving an approved root executable. Reports are generated under ignored `artifacts/Debug/`, `artifacts/Release/` and `artifacts/build-evidence.txt`. Do not infer hosted results until a separately approved push runs GitHub Actions.

## Instrumented Stage 1 Windows subsets

Tests loaded an exact-hash copy of the candidate assembly into a private harness; the unmodified live entry point was not launched. The global interlock blocked production action/suspend/process adapters. All power-state and termination routes were recording-only. A restricted, source-compared bridge exposed only Create/Set/Clear/SystemRequired/Close for the explicitly authorized real lease experiment. Harness code, raw logs, machine paths and owner preparation materials remain local; the summary below is not a claim of independently reproducible hosted hardware testing.

| Checklist ID | Observed subset | Remaining scope for the new candidate |
|---|---|---|
| A/C | Unknown/-1/99 rejection and fresh Online/debounce one-shot recovery passed by injection | Physical unplug/reconnect and measured detection latency not captured for this hash; original build owner-tested successfully |
| E | Actual bounded compilation had a SystemRequired request; elevated read-only Windows request list showed it and then showed it absent | Low-CPU endurance beyond OS idle timeout and policy overrides not tested |
| F | Successful compiler exit plus a fresh healthy sample produced ConfirmedCompleted and released the lease | Real elapsed full grace followed by a physical action not tested |
| G | A visible intercepted countdown was cancelled when a real new compiler was registered Active by the harness; requests remained zero | Prior completion/grace/user-idle setup used fixtures; genuine input and physical resume not instrumented for this hash; original build owner-tested successfully |
| H | One successful Create/Set/Clear/Close; no stale request in subsequent sampled interval | Full charger-priority/disable/re-enable/resume hardware sequence not tested |

On 28 October, UTC: compiler start observed `04:07:59.4594441Z`; SystemRequired Set `04:07:59.4724311Z`; compiler exit observed `04:08:03.2747071Z` (exit 0); Clear `04:08:03.4883944Z`; Close `04:08:03.4894354Z`. Observed compiler duration was about 3815 ms and Set-to-Clear about 4016 ms. The approximately 214 ms observed exit-to-Clear interval includes a deliberate 200 ms fresh-sample wait and is not a production response bound. A deliberate 3000 ms diagnostic observation window was also used.

The elevated collector produced 33 successful read-only snapshots, seven with the harness request. First observed absent after release: `04:08:03.6498215Z`; five subsequent samples through `04:08:05.7464708Z` were absent. The native API calls returned no errors. Non-elevated diagnostics required administrator permission; owner-approved elevation was used for read-only diagnostics only. No power plans changed. The lease belonged to the harness using candidate core policy, not the live candidate process.

Resumed-work cancellation was observed at `04:09:15.0550959Z`, after a running disposable compiler was registered Active at `04:09:15.0192147Z`. That compilation exited naturally with code 0; no compiled fixture was executed. Unknown/unavailable charger, input, CPU and task fault injection also blocked automatic action. Physical charger latency remains unmeasured; configured debounce is not a latency measurement.

## Remaining acceptance and review status

S3 is reported available on the preparation device; Hibernate is disabled and S0 unavailable. These capability reports do not prove candidate Sleep/Resume. No candidate Sleep/Hibernate/Shutdown/Lock/process-kill operation or production/paid Codex task was executed. B and D were not run live; full A-H acceptance remains separate from the subsets above. See the [acceptance checklist](WINDOWS_ACCEPTANCE.md).

The bounded command/Codex lifecycle implementation and parser fixtures do not establish universal IDE task completion, successful project output or production Codex compatibility. Native request visibility does not establish long-duration idle inhibition on all Windows devices. Owner-tested original behavior and instrumented candidate behavior both remain valid within their recorded scope.

**GO for source PR review preparation.** New hosted CI and review are pending a separately authorized push. Actual candidate power-transition testing, a release, Store deployment and unattended hardware certification are separate decisions; this review authorizes none of them.
