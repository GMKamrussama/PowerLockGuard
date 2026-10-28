# Charger Guard, Work Watchdog and Windows keep-awake

PowerLockGuard provides independent charger-disconnect protection and work-aware power management. Current application source `8b7fdd41e6eaaf9de68080d9e4cfd6c7deecda1f` is reviewed in [PR #3](https://github.com/GMKamrussama/PowerLockGuard/pull/3), based on public master `c827aa8196ab3e4e086ffc27a62e6b3746782fe5`. [TEST_RESULTS](TEST_RESULTS.md) holds build identities, current local results, hosted history and owner/candidate evidence; [WINDOWS_ACCEPTANCE](WINDOWS_ACCEPTANCE.md) retains the hardware checklist.

## Audit findings and changes

| Finding | Severity | Before | Local implementation | Regression evidence |
|---|---|---|---|---|
| F1: no actual Windows idle-sleep inhibition | High | CPU eligibility alone could not prevent OS idle sleep | `FunctionalCore.cs`: one owned system-awake lease; `WindowsAdapters.cs`: PowerCreate/Set/ClearRequest and CloseHandle | Balanced acquire/release/dispose, failed Set/Clear/Close, disabled monitoring |
| F2: CPU idle mistaken for semantic completion | High | Long API waits could reach legacy watchdog action | `TaskMonitoring.cs`: explicit registered-task policy; known running/waiting sessions remain protected at CPU zero; heuristic fallback remains opt-in through the existing default mode | Low-CPU active task, all-task completion/grace, unknown/failure, new-work cancellation |
| F3: suspend privilege not activated | High | SetSuspendState called without enabling SE_SHUTDOWN_NAME | `WindowsAdapters.cs`: enable, inspect boolean and last error including 1300, restore previous state and close token; `SuspendAction` injectable boundary | Unsupported state, denied privilege, partial success, native error, restore failure; fake adapters only |
| F4: unrelated resets disarm charger | High | Watchdog save and countdown cleanup reset AC arming | `PowerLockGuard.cs`: reset only when charger enable/action/debounce changes; countdown cleanup leaves charger alone; resume refreshes AC | UI settings change during debounce, preview cancellation, reconnect/resume |
| F5: warning preview suppresses real unplug | High | Preview bypassed charger monitoring | Preview only previews its own countdown. A genuine confirmed charger event cancels the dialog and requests its configured action | UI preview with confirmed fake unplug routes exactly one charger request |
| F6: watchdog validation disables charger | High | Malformed watchdog-only keys disabled both guards | `AppSettings.cs`: per-feature validation; structural/unreadable configuration still disables both | Old configuration cases updated; new-key round trip and both directions of validation isolation |
| F7: invalid charger enums entered Offline path | High safety risk at provider boundary | Only Online/Unknown explicitly excluded | Explicit Online/Offline switch; Unknown/default resets and requires fresh Online | Unknown/-1/99, interrupted debounce, recovery, repeats and UI recording dispatch |

## Action priority and lifecycle

Manual actions retain their confirmation and identity checks. Charger actions use the configured charger action independently of task/CPU/user-idle eligibility. Automatic watchdog actions require current task/CPU eligibility, completion/heuristic grace, user inactivity and a fresh final process sample. User input, settings changes, unknown readings and new work cancel automatic countdowns. Explicit Execute Now remains manual but still checks workload eligibility.

`PowerActionArbiter` serializes dispatch and rejects overlapping requests. It releases its lease before Sleep/Hibernate/Shutdown, suppresses reacquisition until resume or explicit rearming, and allows protection to recover after a definite failure. Release errors are logged; they do not silently veto an intended manual/charger action. A failed charger request is observable and is not automatically retried repeatedly on the same disconnect; reconnect or deliberate reconfiguration is required. An overlapping request is rejected and logged, not silently executed twice.

The lease requests **SystemRequired only**: no display request and no Away Mode. One Set per handle is balanced by Clear and Close, including shutdown/disable paths. A failed Set never claims a held lease or clears an increment that did not succeed. A failed Close remains owned for a later cleanup attempt. Native keep-awake failure is visible in logs/status; no claim that Windows is protected follows merely from a CPU reading.

Microsoft documents that [SetSuspendState](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-setsuspendstate) needs SE_SHUTDOWN_NAME and distinguishes Sleep from Hibernate. [AdjustTokenPrivileges](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-adjusttokenprivileges) can return success while reporting ERROR_NOT_ALL_ASSIGNED; previous-state capture needs TOKEN_QUERY. The implementation requests only token query/adjust access, does not assume elevation, and does not attempt to grant missing privileges. Native BOOLEAN parameters/results use one-byte marshalling; last errors are captured immediately.

[PowerSetRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest) increments a count; [PowerClearRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powerclearrequest) decrements it; [PowerCreateRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powercreaterequest) owns a handle closed at cleanup. These APIs require Windows 7 or later. Missing entry points are logged as failures; .NET Framework 4.0 targeting is retained, not a promise of working idle inhibition on XP. Modern Standby on battery can terminate system/execution requests five minutes after the sleep timeout; user-initiated sleep and policy overrides can also defeat requests. Original charger/input/Sleep-Resume behavior is owner-tested successfully; candidate native lease/visibility/release is instrumented. Candidate physical transitions and cross-device endurance remain unverified.

## Observation and provider scope

| Source | Activity/completion evidence | Verification | Limit |
|---|---|---|---|
| Existing matched processes / CPU | Heuristic activity or inactivity | Deterministic CPU/process fixtures and UI tests | Low CPU/API waits never prove completion; low activity may miss the configured cutoff |
| Explicit finite command launched by PowerLockGuard | Retained live process handle; successful exit plus drained streams | Outcome fixtures, argument-quoting tests and launch interlock | Parent command only; no claim for detached work, remote jobs or arbitrary terminal contents |
| Explicit owned Codex CLI `exec --json` | Live owned session; valid turn start/completion, no failed turn/pending items, successful exit and drained output | JSON fixtures only; no production Codex invocation | Bounded command completion, not general project success or arbitrary IDE completion |
| VS Code, Antigravity, Claude/Cursor/Gemini IDE sessions | Existing exact-name CPU heuristic only | No authoritative integration implemented or tested | Open window/process is not an active task or completion event |
| Unknown/unavailable provider | Unknown | Failure, malformed/truncated/oversized JSON and resume fixtures | Blocks watchdog automatic actions; configurable lease policy |

The parser retains at most 1024 pending IDs, each at most 1024 UTF-16 code units, with total pending-ID storage limited to 65536 units (128 KiB payload plus bounded collection overhead). These are defensive budgets, not protocol maxima. Limits apply before insertion; duplicates do not consume budget and matched completion returns it. Invalidation clears retained IDs and blocks successful completion, while a retained running handle still establishes activity. Invocation/mutex mode comes only from the validated top-level option, so child flags cannot bypass the global live-instance mutex.

The protocol follows documented [Codex noninteractive JSON events](https://developers.openai.com/codex/noninteractive). A turn.completed event by itself is insufficient. A malformed protocol cannot fabricate completion; a retained still-running session remains active even if JSON is malformed. Successful process exit with incomplete streams is Unknown. Any failed turn remains a failure for that session. No real Codex integration test or paid/authenticated API session was performed.

`--watch-command <absolute-native-exe-path> [arguments...]` launches one explicit finite command. `--watch-codex <absolute-codex-exe-path> [exec-arguments...]` prepends `exec --json`. These are **live opt-in launch paths**, not simulation flags; they select registered-task monitoring and enable Watchdog. Later settings saves persist the usual INI values. Review Charger Guard and action/grace settings before using them. Native `.exe` paths are required; npm `.cmd` wrappers are not supported. Existing single-instance protection prevents a second launch; use a separate application run for another finite task. The in-memory registry supports multiple registered tasks for policy tests; there is no universal IDE task registration service or multi-session UI.

Both drain workers and their buffers are prepared before launching an owned child. A setup failure releases waiting workers without launching or touching child streams; a successful launch releases both readers. This avoids leaving a child unmonitored if drain setup fails.

Arguments are Windows-quoted, not shell-concatenated. No network interception, credential reading, session-file scraping or raw conversation logging is added. JSON line parsing is bounded at 262,144 characters; stderr/ordinary command output is discarded. Only lifecycle metadata survives parsing. Readers keep draining when monitoring is disposed so a running child is not deliberately interrupted by closing its pipes; monitoring never kills it. Exiting the application ends protection and OS process/pipe behavior is not a guarantee that every descendant continues. A descendant retaining inherited pipes can keep status Unknown until EOF; choose the unknown policy consciously. Interactive terminal programs and arbitrary shell sessions are outside this finite-command contract.

## Settings and recovery

| INI key | Default / accepted values | Meaning |
|---|---|---|
| ChargerDebounceMilliseconds | 500; 100..10000 | Continuous offline interval after fresh Online; 500 ms polling/message scheduling adds latency |
| WatchdogEvidenceMode | Heuristic / TaskAware | UI: CPU heuristic / Registered tasks. Heuristic preserves legacy arming; TaskAware never automatically acts on an unregistered idle IDE |
| KeepAwakeEnabled | True | Used only when Watchdog is enabled and work policy requests protection |
| UnknownWorkPolicy | HoldKnownWork / ReleaseProtection | UI: Unknown: keep awake / release hold. Hold preserves previously known work; both policies block unknown-state automatic watchdog action |

Existing charger enabled/action defaults are preserved. Disabling keep-awake intentionally allows OS idle policy to act even while work is running. Failed/cancelled finite tasks release their lease and block automatic watchdog actions for owner review. Healthy CPU activity can protect heuristic workloads; legacy CPU heuristic can still act during an uninstrumented low-CPU wait. Select registered tasks for bounded authoritative monitoring. An unregistered idle IDE does not acquire a permanent lease in registered-task mode.

Resume invalidates completed task evidence, resets CPU baselines, refreshes AC state and cancels countdowns. A retained running child handle can establish fresh activity; an old completed session does not automatically regain completion authority after resume. Configuration remains compatible with old keys, including inert legacy auto-kill; no automatic process termination is introduced. Event timestamps are UTC with milliseconds, not measured physical-transition timings.

## Validation boundary

All automated action paths use recording/fake adapters with `POWERLOCKGUARD_NO_NATIVE_ACTIONS=1`. It also blocks real lease adapters, privilege activation and owned-task launches during verification. Current local results are 103 passed / 0 failed per configuration, separate from historical hosted evidence. Stage 1 for source 6568f0d used an audited lease-only bridge while all power-state/termination actions remained recorded. See [owner acceptance and candidate evidence](TEST_RESULTS.md); implemented functionality is distinct from universal hardware certification. Store packaging, releases and publication were not changed.

The JSON parser uses the full-framework `System.Web.Extensions` assembly and stays within .NET Framework 4.0 APIs. Validation used newer installed 4.8 compiler/runtime with pinned 4.0 references; an original .NET 4.0 runtime, Client Profile-only installation and exhaustive Windows/architecture matrix were not tested. No compatibility certification follows from reference compilation alone.

Source review and CI status are recorded on PR #3 for its exact head. Hardware power-transition testing still requires separate authorization and acceptance preconditions; universal IDE-completion and hardware-certification claims remain unsupported.