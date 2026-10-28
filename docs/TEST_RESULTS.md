# PowerLockGuard validation record

## Current implementation and local verification

[PR #3](https://github.com/GMKamrussama/PowerLockGuard/pull/3) contains the core restoration and the two review corrections. Application/test/build source: `8b7fdd41e6eaaf9de68080d9e4cfd6c7deecda1f`. Documentation-only follow-ups preserve those code inputs.

| Local gate, 28 October 2026 | Actual result |
|---|---|
| Debug regression suite | **103 passed / 0 failed** |
| Release regression suite | **103 passed / 0 failed** |
| Debug / Release solution rebuilds | Both passed, zero warnings/errors |
| Unmodified Build.bat in isolated source root | Passed |
| Debug / Release CLI simulation | Passed; native actions invoked: 0 |

All 97 previous cases remain. The original review follow-up added seven invocation/mutex and fourteen parser-bound cases to 76; six further partial-startup cases now cover drain preparation before launch, first/second worker failure, launch failure, successful release and single-use startup. Commit `b9ca23b0c90bbb9716f9fff85ab34dd67d4dbb35` derives simulation/mutex mode solely from the validated top-level option; child `--dry-run` cannot select the simulation mutex for live work. Commit `688e24d` bounds pending items/ID storage before insertion, clears invalid state and preserves running-process protection. These address the two P2 findings on earlier review head `509fef9`. A later review of `0f71d8b` confirmed a third P2: child launch preceded drain setup, so setup failure could leave unmonitored work. Source `8b7fdd4` prepares both workers before launch and aborts waiting readers on setup failure; review status for later heads is recorded on the PR, not inferred from local tests.

During the 97-test stage, an initial new-fixture run had Debug 96 passes and one failure: an invalid overlapping restart was expected to remain Failed, while existing fail-safe semantics return Unknown. The corrected test verifies both stages and still rejects completion. The 97-test rerun passed; the later startup-corrected complete run passed all 103 cases without failures. No application behavior was weakened for that fixture.

Tests ran against an isolated source export with the native-action interlock enabled. All exported tracked files matched `8b7fdd4` by Git blob hash. Markdown cleanup does not justify rerunning unchanged local tests. The 40-test public baseline became 66 at `4fc957f`, 76 at `6568f0d`, then 97 at `688e24d`, and 103 at `8b7fdd4`; test-case totals are not coverage percentages.

## Hosted CI: commit-specific records

| Hosted source | Push run | PR run | Verified reports |
|---|---|---|---|
| `509fef9ba506230a6ec40e1b49b07a92be8ead33` | [40633600394](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40633600394) | [40633608731](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40633608731) | Each run: Debug 76/0 and Release 76/0; both solution builds, Build.bat and CLI passed |
| `0f71d8bd20227496b5658d9229141a12a77d1c3e` | [40640053876](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40640053876) | [40640058822](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40640058822) | Each run: Debug 97/0 and Release 97/0; both solution builds, Build.bat and CLI passed |

Downloaded artifacts were inspected: each 509fef9 run has exactly 76 PASS entries per configuration; each 0f71d8b run has exactly 97. These results belong to their source heads, not hosted proof of the later 103-test startup correction. Consult [PR #3 checks](https://github.com/GMKamrussama/PowerLockGuard/pull/3/checks) and [Windows workflow runs](https://github.com/GMKamrussama/PowerLockGuard/actions/workflows/windows-safety.yml) for the current published head; attach any later result to its exact SHA and artifact reports before claiming it passed.

## Build identities and owner acceptance

| Build | Source | SHA256 | Validation |
|---|---|---|---|
| Original approved build | Application implementation `4fc957feabcaa94db6b31fb2e3560f631e8b54e5`; documentation snapshot `f89c8f754c0dfa2d5063d22059765c5ba8fec40f` has identical application sources | `97534A0A81E41374174AEDFE17394B0CF7FA8E0D74BB0D386F4DA28E5E48F59F` | **OWNER-TESTED: SUCCESS**. Owner confirms charger behavior, input cancellation and Sleep/Resume. This is owner-reported real-world acceptance, without instrumented timings or native API traces. |
| Defensive-fix candidate | `6568f0de4aa6981e2438a0528d83c0a3d6ab5288` | `2A42CA0E7103BEDD4437859EFBAAF4E776BD24585E1C0A32201D5D7D5B449953` | Local 76-test suites plus the instrumented Windows subsets below. Unknown and invalid charger enums reset pending confirmation and require fresh Online before re-arming. |

Both binaries remain separate and unchanged. The approved original was not executed or rebuilt during candidate validation. It retains the invalid-enum provider-boundary defect fixed in the candidate; an injected enum failure does not show that Windows emitted such a value. Owner acceptance of the original is retained without requiring that build to be tested again. It does not automatically certify the newer hash. Neither candidate hash is a signature or a deterministic-build guarantee.


| Latest review-fix candidate | Source | SHA256 | Validation |
|---|---|---|---|
| Separate local Release build | `688e24d19e2e683006033110d0adbef5ae2d4273` | `9F52E38319DCC8C41D6F6E193954B0A69FBE14599709332C0B735ADF0F873BFA` | 97 local tests per configuration; no new physical power-transition claim |

On 28 October 2026, the 6568f0d defensive candidate also passed 76 local tests in each Debug/Release configuration, both solution rebuilds with zero warnings/errors, isolated unmodified Build.bat, and Debug/Release plus copied-candidate CLI simulation (native actions 0). Its isolated tracked source export matched 6568f0d by Git blob hash. These earlier local results remain separate from the 509fef9 hosted runs and the current 688e24d local results.

The separately built startup-corrected Release executable from `8b7fdd41e6eaaf9de68080d9e4cfd6c7deecda1f` has SHA256 `3190359BDAAEC5C4115C363D2FB0181FEFE8C8773E28B97EC4B9715F0E603DA5`. It passed the 103-test automated verification and does not replace either preserved candidate or establish new physical acceptance.

The 6568f0d stage added ten charger regressions: valid Online/Offline transitions, Unknown/-1/99, lost telemetry during debounce, recovery, repeated events, exact thresholds and UI recording dispatch. The newer review fixes leave that charger/native policy unchanged; they do not transfer physical acceptance automatically between hashes.

## Instrumented Stage 1: source 6568f0d

Tests loaded an exact-hash copy of the 6568f0d candidate assembly into a private harness; the unmodified live entry point was not launched. The global interlock blocked production action/suspend/process adapters. All power-state and termination routes were recording-only. A restricted, source-compared bridge exposed only Create/Set/Clear/SystemRequired/Close for the explicitly authorized real lease experiment. Harness code, raw logs, machine paths and owner preparation materials remain local; the summary below is not a claim of independently reproducible hosted hardware testing.

| Checklist ID | Observed subset | Remaining scope for the 6568f0d candidate |
|---|---|---|
| A/C | Unknown/-1/99 rejection and fresh Online/debounce one-shot recovery passed by injection | Physical unplug/reconnect and measured detection latency not captured for this hash; original build owner-tested successfully |
| E | Actual bounded compilation had a SystemRequired request; elevated read-only Windows request list showed it and then showed it absent | Low-CPU endurance beyond OS idle timeout and policy overrides not tested |
| F | Successful compiler exit plus a fresh healthy sample produced ConfirmedCompleted and released the lease | Real elapsed full grace followed by a physical action not tested |
| G | A visible intercepted countdown was cancelled when a real new compiler was registered Active by the harness; requests remained zero | Prior completion/grace/user-idle setup used fixtures; genuine input and physical resume not instrumented for this hash; original build owner-tested successfully |
| H | One successful Create/Set/Clear/Close; no stale request in subsequent sampled interval | Full charger-priority/disable/re-enable/resume hardware sequence not tested |

On 28 October, UTC: compiler start observed `04:07:59.4594441Z`; SystemRequired Set `04:07:59.4724311Z`; compiler exit observed `04:08:03.2747071Z` (exit 0); Clear `04:08:03.4883944Z`; Close `04:08:03.4894354Z`. Observed compiler duration was about 3815 ms and Set-to-Clear about 4016 ms. The approximately 214 ms observed exit-to-Clear interval includes a deliberate 200 ms fresh-sample wait and is not a production response bound. A deliberate 3000 ms diagnostic observation window was also used.

The elevated collector produced 33 successful read-only snapshots, seven with the harness request. First observed absent after release: `04:08:03.6498215Z`; five subsequent samples through `04:08:05.7464708Z` were absent. The native API calls returned no errors. Non-elevated diagnostics required administrator permission; owner-approved elevation was used for read-only diagnostics only. No power plans changed. The lease belonged to the harness using candidate core policy, not the live candidate process.

Resumed-work cancellation was observed at `04:09:15.0550959Z`, after a running disposable compiler was registered Active at `04:09:15.0192147Z`. That compilation exited naturally with code 0; no compiled fixture was executed. Unknown/unavailable charger, input, CPU and task fault injection also blocked automatic action. Physical charger latency remains unmeasured; configured debounce is not a latency measurement.


## Acceptance boundaries

S3 is reported available on the preparation device; Hibernate is disabled and S0 unavailable. These capability reports do not prove candidate Sleep/Resume. No candidate Sleep/Hibernate/Shutdown/Lock/process-kill operation or production/paid Codex task was executed. B and D were not run live; full A-H acceptance remains separate from the subsets above. See the [acceptance checklist](WINDOWS_ACCEPTANCE.md).

The bounded command/Codex lifecycle implementation and parser fixtures do not establish universal IDE task completion, successful project output or production Codex compatibility. Native request visibility does not establish long-duration idle inhibition on all Windows devices. Owner-tested original behavior and instrumented candidate behavior both remain valid within their recorded scope.


## Reproduce and inspect reports

Environment: Windows 10 Home Single Language 10.0.19045 x64; four logical processors; compiler 4.8.9232.0; CLR 4.0.30319.42000; installed .NET release registry value 533325; PowerShell 5.1.19041.6456. Target references remain .NET Framework 4.0 from SHA256-pinned reference package 1.0.3. This is not testing an original .NET 4.0 runtime or an exhaustive Windows matrix.

In a disposable source checkout, with Framework MSBuild on PATH:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1 -CheckMSBuild
```

The script creates a root build output: use a separate source checkout when preserving an approved executable. Reports are under ignored `artifacts/Debug/`, `artifacts/Release/` and `artifacts/build-evidence.txt`. Native state/kill operations remain prohibited during automated verification. Raw harness logs and owner preparation materials are not public documentation. See [architecture/provider scope](CORE_FUNCTIONALITY.md) and [hardware checklist](WINDOWS_ACCEPTANCE.md).

## Historical local restoration — 66 tests

Branch `feature/core-functionality`, tested implementation commit `4fc957feabcaa94db6b31fb2e3560f631e8b54e5`, base `c827aa8196ab3e4e086ffc27a62e6b3746782fe5`, tested 27 October 2026. Local Windows 10 Home Single Language 10.0.19045, .NET 4.0 reference target and installed legacy compiler 4.8.9232.0:

| Safe gate | Actual result |
|---|---|
| Debug deterministic policy/native-fake/UI tests | 66 passed; 0 failed |
| Release deterministic policy/native-fake/UI tests | 66 passed; 0 failed |
| Debug and Release CLI `--simulate all` | Passed; native actions invoked: 0 |
| Debug / Release solution rebuilds | Both passed; 0 warnings, 0 errors |
| `Build.bat --no-pause` | Passed |
| Real native lease/privilege/power operations | Not run; verification interlock enabled |
| Owned process launch / real Codex session / production API | Not run; launch blocked by interlock; outcome/JSON fixtures only |
| Physical AC sensor / power transition / resume | Not run; scripted fixtures only |

The 26 added tests exercise command outcomes, settings round trips/isolation, native error and cleanup paths, balanced lease ownership, priority/in-flight arbitration, low-CPU running work, all-task completion and grace, unknown/failure policy, resume, malformed/oversized Codex events, launch blocking/argument quoting, and UI charger/countdown integration. Two prior tests now assert the approved corrected behavior: isolated configuration validation and charger protection during warning preview. No baseline test was removed. During development a newly added completion-path fixture failed because its CPU selection key differed from the UI configuration; the fresh final sample correctly reset eligibility. Correcting the fixture made that test pass without weakening the safety gate.

Reports are generated in ignored `artifacts/Debug/test-results.txt`, `artifacts/Release/test-results.txt` and `artifacts/build-evidence.txt`. Full local command/error logs and read-only hardware diagnostics remain private. Expected unknown-option tests emit CLI usage on stderr; that output is not a failed build. At this initial 66-test stage there was no hosted result for those sources. Later PR #3 hosted records are listed above; the workflow discovers all test source files. See [current capabilities and risks](CORE_FUNCTIONALITY.md) and [acceptance scenarios and evidence status](WINDOWS_ACCEPTANCE.md).

## Historical merged and hosted evidence

Prepared 27 October 2026. Reported pass counts belong to the snapshots below, not every possible configuration or a coverage percentage.

PR #1 is merged into `master` at [70cd1b8ac8ad2441c6ac75b87124aac500ce3f36](https://github.com/GMKamrussama/PowerLockGuard/commit/70cd1b8ac8ad2441c6ac75b87124aac500ce3f36). Automated Windows validation completed; a v1.1 binary release has not been published. Physical power transitions, physical sensors and Codex task completion are not verified.

## Source and environments

| Scope | Source SHA | Environment | Observed result |
|---|---|---|---|
| Merged master snapshot | 70cd1b8ac8ad2441c6ac75b87124aac500ce3f36 | GitHub workflow windows-2022, .NET 4.0 references; post-merge push run | 40 passed / 0 failed in Debug; 40 / 0 in Release; both solution builds and Build.bat passed |
| Historical PR snapshot | d8290049c586d01d0c85ed57fc9a89b64d88af30 | GitHub workflow windows-2022, .NET 4.0 references | 37 passed / 0 failed in Debug; 37 / 0 in Release; both solution builds and Build.bat passed |
| Earlier verified PR snapshot | 04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa | GitHub workflow windows-2022, .NET 4.0 references; PR and push runs | 40 passed / 0 failed in Debug; 40 / 0 in Release; both solution builds and Build.bat passed |
| Local finalization implementation | e45eed7e46eca1a9fe1b8b6b8048927fab26a1b9 | Windows 10 Home Single Language, 10.0.19045; legacy compiler 4.8.9232.0 | 40 passed / 0 failed in Debug; 40 / 0 in Release; both solution builds and Build.bat passed |

The historical follow-up is based on d829004 and integrated as 73ab1e9. Its application/test sources are unchanged at 04a0fe4. Both hosted runs for 04a0fe4 succeeded; the downloaded Debug and Release reports from each run contain exactly 40 PASS entries and a 40-passed/0-failed summary. The prior documentation-only update preserved those sources; the later core restoration changes them and is reported separately above. Check [Windows workflow runs](https://github.com/GMKamrussama/PowerLockGuard/actions/workflows/windows-safety.yml) for validation of published heads. The target API references are .NET Framework 4.0 from Microsoft.NETFramework.ReferenceAssemblies.net40 1.0.3; the installed runtime/compiler is newer. No unpatched .NET 4.0 machine or exhaustive OS matrix was tested.

## Commands and reports

Post-merge evidence: [run 40509133147](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147) succeeded for merge SHA `70cd1b8ac8ad2441c6ac75b87124aac500ce3f36`. Its downloaded Debug/Release reports each contain 40 PASS entries and a 40-passed/0-failed summary. Build evidence confirms the native-action interlock, both solution builds and Build.bat. The merged tree matches approved head `948a680226dadb89ed8cacfa930132ce1c9ffffb`. These are automated policy/UI/CLI checks, not physical hardware tests.

```powershell
$env:PATH = (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319') + ';' + $env:PATH
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1 -CheckMSBuild
```

The script verifies reference-package SHA256 `54D6E20A1B61CAF79395D6D71D091265E81F5A5705AC4AE52AF45CA143C3C694`, compiles both configurations against those references, runs `SafetyTests.exe`, invokes the new reviewed CLI with `--simulate all`, checks solution builds and calls `Build.bat --no-pause`. The CLI output file is removed before invocation so a stale log cannot satisfy its check.

Outputs under ignored `artifacts/Debug/` and `artifacts/Release/` include `test-results.txt`, `simulation.log`, `cli-simulation.log`, four UI PNGs, candidate executable/config/icon and SHA256. `artifacts/build-evidence.txt` describes the build gates. The full local command log is private, not committed. Candidate signing inspection returned **NotSigned**.

The local Release executable built from e45eed7 has SHA256 `9B1DC963323095B8F706A09F0ACEB4DE7B4667BDEC293F096E33B1FE449200F7`. This identifies that build, not a reproducible-build guarantee or signature. A local unsigned candidate bundle includes its own build manifest and file checksums; it has not been uploaded. Future builds may differ in hash even with unchanged source.

Earlier verified hosted evidence at **04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa**: [PR run 40503655880](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880), [push run 40503648420](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420), [test source](https://github.com/GMKamrussama/PowerLockGuard/blob/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa/tests/SafetyTests.cs). Both runs' downloaded `safety-evidence` reports and workflow logs were inspected, including the user-input follow-up. Historical 37-test evidence: [PR run 40495736646](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495736646) and [push run 40495731233](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495731233), both at d829004. These historical runs predate the merged master snapshot.

## Historical public safety-snapshot verification matrix

| Feature | Automated test | Windows validation | Real hardware test | Result |
|---|---|---|---|---|
| High CPU does not authorize kill | Sustained CPU and rejected automatic origins | Local + hosted fake readings | Not run | Verified policy |
| Process identity | PID reuse, exit, self, access denial, retained fake handle | Local + hosted | Real termination not run | Verified policy; production termination unverified |
| Countdown cancellation | Close, Esc/Space/Cancel, expiry, resumed work, settings/final replacement | WinForms local + hosted | Physical Alt+F4 not automated | Verified UI/code paths |
| Keyboard/mouse inactivity | Recent/negative/throwing sensor and resumed-input modal test | Local + hosted WinForms fakes; suite includes 3 follow-up tests | Physical GetLastInputInfo behavior not tested | Verified tested policy; physical sensor unverified |
| Explicit Execute Now | Positive manual request still requires CPU eligibility | Local + hosted WinForms fake executor | Real power action not run | Verified tested policy |
| Charger | Unknown, startup battery, bounce, debounce, one-shot/reconnect | Local + hosted fake readings | Physical AC unplug not tested | Verified policy; hardware response unverified |
| Simulation | All action/origin combinations, interlock, actual CLI | Local + hosted synthetic UI/CLI | No real action permitted | Verified recording-only paths |
| Config | Invalid/overflow/NaN values, legacy keys, atomic replace, failed write, ephemeral no-write | Controlled Windows test files local + hosted | Protected installed folder not manually tested | Verified tested cases |
| Error results | Throwing fake executor; native source review/build | Local + hosted | Real native failure/privilege outcomes not tested | Partial; native outcomes unverified |
| Resources | Fake handle disposal and repeated form lifecycle, shared fonts in source | Local + hosted | Long-duration soak not run | Limited verification |
| .NET/Windows compatibility | Target-reference compilation and WinForms tests | Local Windows 10 + hosted Windows runner | Full OS/runtime matrix not run | Limited to these environments |

## Scenarios and scope

`all` exercises synthetic high CPU, observed inactivity, a would-Sleep watchdog request, Unknown charger suppression and confirmed unplug recording; automatic termination is rejected. `watchdog` and `charger` restrict that trace. Headless policy uses virtual time; it does not pretend to be a 30-second real countdown. Countdown assertions use WinForms with recording executors. Native-action zero output is corroborated by adapter confinement and the verification interlock, not treated as a standalone instrumentation proof.

No original release binary was executed. Tests did not call real Process.Kill or sleep/hibernate/shutdown/lock. Simulation does not establish completion of a Codex task, S3 support, network/token preservation, signed provenance, battery benchmarks or physical power-state success. Do not enable live mode while recording this evidence. Do not publish raw session logs, private application answers or machine/account identifiers with artifacts.

The source-only PR integration at 73ab1e9 was rechecked in a separate clean worktree before push: Debug 40/0, Release 40/0, both solution builds and Build.bat passed with the native-action interlock enabled. Subsequent documentation edits do not change those application/test sources.
