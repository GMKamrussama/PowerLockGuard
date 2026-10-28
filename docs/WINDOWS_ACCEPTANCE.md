# Owner laptop acceptance checklist and evidence status

Updated 28 October 2026. Original approved binary: **OWNER-TESTED: SUCCESS** for charger, input cancellation and Sleep/Resume, explicitly confirmed by the owner. Those original behaviors are not NOT RUN; detailed timings/native traces were not collected. The newer candidate has instrumented E/F/G/H subsets plus injected C/telemetry evidence. [Build identities and measurements](CORE_REVIEW_EVIDENCE.md).

The original scenario IDs A-H below remain unchanged. Keep owner acceptance, candidate instrumented subsets and future physical testing distinct.

This plan prepares physical testing; it authorizes no Sleep, Hibernate, Shutdown, Lock, process termination or charger unplug test. A separate owner authorization is required before changing power state. Automated fake-adapter results are not physical evidence. Do not use a production AI/API job for these tests.

## Preconditions and evidence

Save work, back up configuration and use a disposable local finite workload. Identify the exact locally reviewed binary SHA256 and source commit. The shipped default Charger Guard is enabled; review/disable it before any intentional unplug that is outside an authorized scenario. Record intended action, debounce, watchdog mode, cutoff, grace, countdown and unknown policy. Never rely on “preview” to disable Charger Guard; use `--dry-run` for a completely recording-only application.

Record locally, redacting machine/account names before sharing:

- Windows edition/version/build, processor count and .NET runtime/compiler; S3 versus Modern Standby.
- Read-only `powercfg /a` and `powercfg /requests` before/during/after each lease scenario. If permission is denied, record that result; obtain owner-approved elevation only for the read-only diagnostic if needed.
- AC/battery state and battery percentage, supported Hibernate setting, lid/power-button policy and power-request overrides. Do not change these silently.
- App log with UTC timestamps, physical observation, start/end time, cancellation/input timing and Windows event evidence if available. API request success does not replace observed outcome.

Local preparation snapshot on 27 October 2026: Windows 10 Home Single Language 10.0.19045; read-only `/a` reported S3 available, Hibernate disabled and S0 Low Power Idle unavailable. `/requests` required administrator privileges and was not obtained. This records capability reporting only; no transition, real lease or physical sensor validation was performed in that initial snapshot. Later candidate native lease visibility/release evidence is recorded separately above. AC/battery conditions must be freshly recorded by the owner at test time; no physical conditions are inferred from this snapshot.

## Scenarios

| ID | Setup / action after authorization | Expected | Evidence / stop condition |
|---|---|---|---|
| A | Charger enabled, Watchdog disabled, fresh AC observation; sustained unplug with chosen Sleep | One configured request after debounce; no Watchdog prerequisite | Record offline-observed/request/physical-sleep times separately; no exact millisecond guarantee; stop on wrong/duplicate action |
| B | Same, configured Hibernate only if `/a` supports it | Hibernate selection preserved, or clear unsupported/privilege failure | Current preparation machine has Hibernate disabled: mark blocked, do not enable it without separate approval |
| C | Charger unplug/replug bounce; then sustained disconnect and reconnect | Short bounce/Unknown never dispatches; one request per confirmed transition; reconnect re-arms | Separate polling latency from debounce; stop on unexpected transition |
| D | Watchdog-only setting change and warning preview while charger is armed | Arming survives; genuine unplug remains independent and cancels preview | Preview scope must be understood before unplug; no live test under an assumption of global simulation |
| E | Registered disposable finite command running with very low CPU; wait beyond configured OS idle timeout | SystemRequired request visible while active; display may turn off; no completion inference from low CPU | `/requests` before/during/after, actual awake interval and command lifetime; stop if lease missing or policy override prevents protection |
| F | Disposable command succeeds, healthy samples and no user activity for grace; countdown | Lease released at completion, countdown/action only after required grace; all observations logged | OS idle timeout may act independently once lease is released; isolate this from app-initiated action; do not attribute native sleep to the app without evidence |
| G | During pending countdown inject user input, then repeat with new registered work; resume afterward | Automatic countdown cancels; fresh work protected; resume resets stale completion/CPU/AC | Verify no late action after cancellation; no real AI invocation required; source fixtures cover multiple-task policy but current live CLI owns one session |
| H | Known work holds lease; authorized charger action; then monitoring disabled/app exit/re-enable | Clear/close before configured charger action; no stale request; disabled/exit releases; fresh resume can protect running work | Compare requests before/during/after; failures visible; stop on persistent unexplained request |

No scenario requires killing a process. Do not test real Shutdown/Lock incidentally; they need their own explicitly approved scope. Codex production task completion remains unverified even if the local disposable command succeeds. A native Codex executable/version/protocol compatibility session would require separate non-production/API approval and evidence; it is not part of this plan's default workload.

## Result record

For each A–H record: source SHA; binary hash; timestamp/time zone; settings; Windows power capabilities; AC/battery start/end; workload/exit status; lease requests; expected/observed result; native return/error; measured latency and measurement method; PASS/FAIL/BLOCKED/NOT RUN; logs and owner notes. Original approved build: owner-tested charger/input/Sleep-Resume SUCCESS, without inferring unrecorded checklist steps. Newer candidate: physical A/C, B/D live and genuine-input/physical-resume parts of G remain uninstrumented; E native API/visibility, F completion/release, G resumed-work cancellation and H balanced cleanup subsets passed. Full A-H acceptance remains incomplete. B is blocked by disabled Hibernate. Approved elevated read-only `/requests` showed the candidate-harness request appear/disappear; idle-timeout endurance remains unverified.

Stop immediately on the wrong power action, duplicate request, ignored cancellation, unexpected data loss, failure to resume, missing protection or unexplained persistent lease. Preserve evidence and return to recording-only reproduction. Do not publish or claim hardware acceptance until results have been reviewed.
