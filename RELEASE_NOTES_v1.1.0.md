# PowerLockGuard v1.1.0 candidate

Keeps .NET Framework 4.0 / WinForms and adds safety controls and recording-only demonstrations.

## Fixes

- CPU load no longer proves a stuck process or authorizes automatic termination. Legacy automatic-kill settings are disabled.
- Countdown close defaults to cancellation; resumed activity, invalid samples and settings changes invalidate actions. Execution requires fresh observations.
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
