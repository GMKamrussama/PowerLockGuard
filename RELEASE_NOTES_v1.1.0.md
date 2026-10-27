# PowerLockGuard v1.1.0 candidate

Keeps .NET Framework 4.0 / WinForms and adds safety controls and recording-only demonstrations.

## Fixes

- CPU load no longer proves a stuck process or authorizes automatic termination. Legacy automatic-kill settings are disabled.
- Countdown close defaults to cancellation; resumed activity, invalid samples and settings changes invalidate actions. Execution requires fresh observations.
- Local finalization follow-up: automatic watchdog action also requires keyboard/mouse inactivity for the configured grace. Unknown/recent input blocks it; resumed input cancels the countdown. Explicit Execute Now is manual but still revalidates CPU/sample state. Check the latest PR checks for hosted validation of this follow-up.
- CPU history, ignore and manual termination identify instances by PID + start time. Manual termination remains confirmed and rejects replacement/exit.
- Unknown/warming/partial samples cannot advance idle. Activity after enabling arms the watchdog. Monotonic timing replaces per-poll inflation; tray/form toggles share reset behavior and resume disarms.
- Charger Online/Offline/Unknown and debounce prevent false unplug actions.
- Non-forced shutdown, native results/errors and command exit codes replace silent failure. Unsupported actions have no Sleep fallback.
- Validated, atomic config with visible errors; startup shortcut errors and unique temporary-script cleanup.
- Deterministic process/timer/row-font lifetimes and bounded, exportable local event logs.

## Demonstration and verification

- `--dry-run`: synthetic WinForms UI and recording actions; no startup/config mutation.
- `--simulate all|watchdog|charger [output-path]`: deterministic headless scenarios.
- `--capture-screenshots`: simulation-only screenshots.
- Verification interlock forbids production action adapters in test processes.
- Windows CI builds Debug/Release against pinned .NET 4.0 references and records regression/CLI/UI evidence. Inspect the actual PR checks/artifacts.

## Compatibility and limits

INI keys/actions/modes remain. Matching is exact and ignore is instance-specific. Shipped Windows startup is disabled. The stale committed executable is removed; new binaries are local outputs or CI candidates.

CPU inactivity remains a heuristic, with no verified Codex/Claude completion integration. S3, zero sleep battery usage, exact unplug latency, network continuation and token preservation are not guaranteed. Confirmed charger actions intentionally bypass watchdog countdown. No real process kill or hardware power transition validates this candidate. The PR does not publish a release or deploy Store packaging.

## Test results and upgrade notes

Latest verified hosted snapshot before this documentation update: `04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa`, with 40 passed / 0 failed in each Debug/Release configuration in both the [PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) and [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420). Both solution builds and Build.bat passed. Historical d829004 had 37 passing tests per configuration; it is not the current HEAD. See [TEST_RESULTS](docs/TEST_RESULTS.md) for exact evidence and physical-sensor/power limitations, and PR checks for subsequent heads.

Keep a backup of your old INI before upgrading; the legacy automatic-kill key is disabled rather than honored. Name matching is exact and ignore is per instance. Review charger protection before starting live mode: shipped config enables it, and confirmed-unplug requests bypass the watchdog countdown. Failed settings writes are logged and cannot be assumed persisted.

Start with `--dry-run` under `POWERLOCKGUARD_NO_NATIVE_ACTIONS=1`. Do not double-click an unflagged candidate for a safe demo. Manual termination and live power actions can interrupt useful or unsaved work even with these guards. Review any exported process/event log before sharing.

Publication recommendation: label the first owner-approved candidate as a **pre-release**, since hardware power behavior, physical sensor integration and a broad OS matrix remain unverified. Local candidate binaries are unsigned. No tag, asset upload or release publication is authorized by preparing these notes.
