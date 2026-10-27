# PowerLockGuard v1.1 test results

Prepared 27 October 2026. Reported pass counts belong to the snapshots below, not every possible configuration or a coverage percentage.

## Source and environments

| Scope | Source SHA | Environment | Observed result |
|---|---|---|---|
| Historical PR snapshot | d8290049c586d01d0c85ed57fc9a89b64d88af30 | GitHub workflow windows-2022, .NET 4.0 references | 37 passed / 0 failed in Debug; 37 / 0 in Release; both solution builds and Build.bat passed |
| Latest verified hosted snapshot before this documentation update | 04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa | GitHub workflow windows-2022, .NET 4.0 references; PR and push runs | 40 passed / 0 failed in Debug; 40 / 0 in Release; both solution builds and Build.bat passed |
| Local finalization implementation | e45eed7e46eca1a9fe1b8b6b8048927fab26a1b9 | Windows 10 Home Single Language, 10.0.19045; legacy compiler 4.8.9232.0 | 40 passed / 0 failed in Debug; 40 / 0 in Release; both solution builds and Build.bat passed |

The follow-up is based on d829004 and integrated as 73ab1e9. Its application/test sources are unchanged at 04a0fe4. Both hosted runs for 04a0fe4 succeeded; the downloaded Debug and Release reports from each run contain exactly 40 PASS entries and a 40-passed/0-failed summary. This documentation-only update preserves those sources; check [current PR checks](https://github.com/GMKamrussama/PowerLockGuard/pull/1/checks) for validation of subsequent heads. The target API references are .NET Framework 4.0 from Microsoft.NETFramework.ReferenceAssemblies.net40 1.0.3; the installed runtime/compiler is newer. No unpatched .NET 4.0 machine or exhaustive OS matrix was tested.

## Commands and reports

```powershell
$env:PATH = (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319') + ';' + $env:PATH
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1 -CheckMSBuild
```

The script verifies reference-package SHA256 `54D6E20A1B61CAF79395D6D71D091265E81F5A5705AC4AE52AF45CA143C3C694`, compiles both configurations against those references, runs `SafetyTests.exe`, invokes the new reviewed CLI with `--simulate all`, checks solution builds and calls `Build.bat --no-pause`. The CLI output file is removed before invocation so a stale log cannot satisfy its check.

Outputs under ignored `artifacts/Debug/` and `artifacts/Release/` include `test-results.txt`, `simulation.log`, `cli-simulation.log`, four UI PNGs, candidate executable/config/icon and SHA256. `artifacts/build-evidence.txt` describes the build gates. The full local command log is private, not committed. Candidate signing inspection returned **NotSigned**.

The local Release executable built from e45eed7 has SHA256 `9B1DC963323095B8F706A09F0ACEB4DE7B4667BDEC293F096E33B1FE449200F7`. This identifies that build, not a reproducible-build guarantee or signature. A local unsigned candidate bundle includes its own build manifest and file checksums; it has not been uploaded. Future builds may differ in hash even with unchanged source.

Latest verified hosted evidence at **04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa**: [PR run 40503655880](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880), [push run 40503648420](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420), [test source](https://github.com/GMKamrussama/PowerLockGuard/blob/04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa/tests/SafetyTests.cs). Both runs' downloaded `safety-evidence` reports and workflow logs were inspected, including the user-input follow-up. Historical 37-test evidence: [PR run 40495736646](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495736646) and [push run 40495731233](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40495731233), both at d829004. These historical runs do not describe the current PR head.

## Verification matrix

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
