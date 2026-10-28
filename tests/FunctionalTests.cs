using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using PowerLockGuard;

internal sealed class LeaseNativeFake : IPowerRequestNative
{
    public int Creates, Sets, Clears, Closes;
    public bool SetFails, ClearFails, CloseFails;
    public IntPtr Create() { Creates++; return new IntPtr(Creates); }
    public void SetSystemRequired(IntPtr h) { Sets++; if (SetFails) throw new Exception("set failed"); }
    public void ClearSystemRequired(IntPtr h) { Clears++; if (ClearFails) throw new Exception("clear failed"); }
    public void Close(IntPtr h) { Closes++; if (CloseFails) throw new Exception("close failed"); }
}
internal sealed class PrivilegeFake : IShutdownPrivilege
{
    public bool Disposed;
    public string RestoreError { get; set; }
    public void Dispose() { Disposed = true; }
}
internal sealed class SuspendFake : ISuspendNative
{
    public bool Supported = true, Denied, Called, Hibernate;
    public NativeCallResult Result = new NativeCallResult(true, 0);
    public readonly PrivilegeFake Privilege = new PrivilegeFake();
    public bool Supports(bool h) { return Supported; }
    public IShutdownPrivilege EnableShutdownPrivilege() { if (Denied) throw new Exception("privilege denied"); return Privilege; }
    public NativeCallResult Suspend(bool h) { Called = true; Hibernate = h; return Result; }
}
internal sealed class CallbackExecutor : IActionExecutor
{
    public Func<ActionRequest, ActionResult> Callback;
    public ActionResult Execute(ActionRequest r) { return Callback(r); }
}
internal sealed class MissingTasks : ITaskStatusSource
{
    public TaskSnapshot Read() { throw new Exception("unavailable"); }
    public void OnResume() { }
}
internal static class FunctionalTests
{
    private static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    private static void Call(object obj, string name, params object[] args) { obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
    private static WorkWatchdogEngine IdleEngine(FakeClock c)
    {
        var e = new WorkWatchdogEngine(c, new ScriptedProcesses(), 4); e.Poll("AgentsAndBuilds", "", 1000, 3.5); c.Advance(1); e.Poll("AgentsAndBuilds", "", 1000, 3.5); return e;
    }
    private static IEnumerable<string> LogItems(MainForm f) { foreach (object item in ((ListBox)Field(f, "lstActivityLog")).Items) yield return item.ToString(); }
    private static AppSettings Settings() { return new AppSettings { Ephemeral = true, WatchdogEnabled = true, WatchdogEvidenceMode = "TaskAware", WatchdogGraceMinutes = 1, PlayAlertSound = false }; }
    public static void Run(Action<string, Action> test, Action<bool, string> check, string output)
    {
        ChargerStateRegressionTests.Run(test, check);
        InvocationModeRegressionTests.Run(test, check);
        test("Bounded command outcomes distinguish running success failure and incomplete drain", delegate {
            check(FiniteTaskOutcome.Command(false, 0, false) == TaskState.Active, "running handle is authoritative activity");
            check(FiniteTaskOutcome.Command(true, 0, true) == TaskState.Completed, "finite command success only");
            check(FiniteTaskOutcome.Command(true, 1, true) == TaskState.Failed, "failed build cannot complete successfully");
            check(FiniteTaskOutcome.Command(true, 0, false) == TaskState.Unknown, "unclosed inherited streams do not fabricate activity/completion");
        });
        test("New settings round trip and invalid per-feature keys remain isolated", delegate {
            string path = Path.Combine(output, "functional-settings.ini");
            var settings = new AppSettings(path) { WatchdogEnabled = true, ChargerDebounceMilliseconds = 1200, WatchdogEvidenceMode = "TaskAware", KeepAwakeEnabled = false, UnknownWorkPolicy = "ReleaseProtection" };
            check(settings.Save(), "write"); var loaded = AppSettings.Load(path);
            check(loaded.WatchdogEnabled && loaded.ChargerGuardEnabled && loaded.ChargerDebounceMilliseconds == 1200 && loaded.WatchdogEvidenceMode == "TaskAware" && !loaded.KeepAwakeEnabled && loaded.UnknownWorkPolicy == "ReleaseProtection", "all new options round trip");
            foreach (string invalid in new[] { "WatchdogEvidenceMode=Typo", "UnknownWorkPolicy=Typo", "KeepAwakeEnabled=Typo" }) {
                File.WriteAllText(path, "ChargerGuardEnabled=True\nWatchdogEnabled=True\n" + invalid);
                loaded = AppSettings.Load(path); check(loaded.ChargerGuardEnabled && !loaded.WatchdogEnabled, "watchdog validation isolation");
            }
            File.WriteAllText(path, "WatchdogEnabled=True\nChargerDebounceMilliseconds=0"); loaded = AppSettings.Load(path); check(loaded.WatchdogEnabled && !loaded.ChargerGuardEnabled, "charger validation isolation");
        });
        test("Native suspend rejects unsupported states before privilege acquisition", delegate {
            var n = new SuspendFake { Supported = false }; var r = SuspendAction.Execute(true, n);
            check(r.Status == ActionStatus.Failed && !n.Called && !n.Privilege.Disposed, "no suspend or unowned cleanup");
        });
        test("Native privilege partial success ERROR_NOT_ALL_ASSIGNED is failure", delegate {
            bool rejected = false; try { SuspendAction.ValidatePrivilegeAdjustment(true, 1300); } catch (System.ComponentModel.Win32Exception) { rejected = true; }
            check(rejected, "boolean success alone is insufficient");
            var n = new SuspendFake { Denied = true }; check(SuspendAction.Execute(false, n).Status == ActionStatus.Failed && !n.Called, "privilege failure blocks call");
        });
        test("Native suspend preserves hibernate selection and restores privileges on error", delegate {
            var n = new SuspendFake { Result = new NativeCallResult(false, 5) }; var r = SuspendAction.Execute(true, n);
            check(r.Status == ActionStatus.Failed && r.ToString().Contains("5") && n.Hibernate && n.Privilege.Disposed, "reported error and cleanup");
        });
        test("Native API success is only a request and cleanup failure is observable", delegate {
            var n = new SuspendFake(); check(SuspendAction.Execute(false, n).Status == ActionStatus.Requested && n.Privilege.Disposed, "request only");
            n = new SuspendFake(); n.Privilege.RestoreError = "fake restore denied";
            check(SuspendAction.Execute(false, n).Status == ActionStatus.Failed, "restore failure visible");
        });
        test("System-awake lease balances repeated acquisition release and disposal", delegate {
            var n = new LeaseNativeFake(); var l = new SystemAwakeLease(n); l.Acquire(); l.Acquire(); l.Release(); l.Release(); l.Acquire(); l.Dispose(); l.Dispose();
            check(n.Creates == 2 && n.Sets == 2 && n.Clears == 2 && n.Closes == 2 && !l.IsHeld, "one increment per handle");
        });
        test("Lease Set failure closes without clearing a nonexistent increment", delegate {
            var n = new LeaseNativeFake { SetFails = true, CloseFails = true }; var l = new SystemAwakeLease(n);
            try { l.Acquire(); } catch (Exception ex) { check(ex.Message.Contains("set failed") && ex.Message.Contains("close failed"), "both native failures observable"); }
            check(!l.IsHeld && n.Clears == 0, "failed set does not claim active lease");
            n.CloseFails = false; l.Dispose(); check(n.Closes == 2 && n.Clears == 0, "retry owned handle cleanup");
        });
        test("Lease clear failure still closes its native handle", delegate {
            var n = new LeaseNativeFake { ClearFails = true }; var l = new SystemAwakeLease(n); l.Acquire(); bool failed = false;
            try { l.Release(); } catch (Exception) { failed = true; }
            l.Dispose(); check(failed && n.Closes == 1 && !l.IsHeld, "cleanup and error both preserved");
        });
        test("Charger releases lease before dispatch independently of idle eligibility", delegate {
            var lease = new RecordingKeepAwakeLease(); var executor = new CallbackExecutor();
            executor.Callback = delegate { check(!lease.IsHeld, "release precedes dispatch"); return new ActionResult(ActionStatus.Simulated, "fake"); };
            using (var a = new PowerActionArbiter(new ActionDispatcher(executor), lease, null)) {
                a.UpdateProtection(true); check(lease.IsHeld, "initial protection");
                check(a.Request("Hibernate", ActionOrigin.Charger, null).Status == ActionStatus.Simulated, "charger has independent authority");
                a.UpdateProtection(true); check(!lease.IsHeld, "cannot immediately reacquire after transition");
                a.Resume(); a.UpdateProtection(true); check(lease.IsHeld, "fresh resume permits protection");
            }
        });
        test("Arbiter rejects overlapping requests and stale watchdog eligibility", delegate {
            var lease = new RecordingKeepAwakeLease(); var executor = new CallbackExecutor(); PowerActionArbiter a = null;
            executor.Callback = delegate { check(a.Request("Sleep", ActionOrigin.Charger, null).Status == ActionStatus.Rejected, "no nested duplicate"); return new ActionResult(ActionStatus.Simulated, "fake"); };
            using (a = new PowerActionArbiter(new ActionDispatcher(executor), lease, null)) {
                check(a.Request("Sleep", ActionOrigin.Watchdog, null, delegate { return false; }).Status == ActionStatus.Rejected, "fresh final gate");
                check(a.Request("Sleep", ActionOrigin.Manual, null).Status == ActionStatus.Simulated, "manual authority preserved");
            }
        });
        test("Failed power request rearms known-work protection and disposal releases", delegate {
            var l = new RecordingKeepAwakeLease(); var a = new PowerActionArbiter(new ActionDispatcher(new ThrowingExecutor()), l, null);
            a.UpdateProtection(true); check(a.Request("Sleep", ActionOrigin.Charger, null).Status == ActionStatus.Failed, "failure propagated");
            a.UpdateProtection(true); check(l.IsHeld, "active work can recover protection after failure"); a.Dispose(); check(!l.IsHeld, "exit releases");
        });
        test("Confirmed API-wait task remains protected during low CPU beyond grace", delegate {
            var c = new FakeClock(); var e = IdleEngine(c); var tasks = new TaskRegistry(); var p = new WorkloadPolicy(c, tasks); var s = Settings();
            tasks.Set("finite-build", TaskState.Waiting); p.Refresh(s, e); c.Advance(600); p.Refresh(s, e);
            var l = new RecordingKeepAwakeLease(); using (var a = new PowerActionArbiter(new ActionDispatcher(new RecordingActionExecutor()), l, null)) {
                a.UpdateProtection(p.Protect); check(p.Observation == WorkObservation.ConfirmedActive && l.IsHeld && !p.CoreEligible, "CPU is not authoritative completion");
                s.WatchdogEnabled = false; p.Refresh(s, e); a.UpdateProtection(p.Protect); check(!l.IsHeld, "disabled monitoring releases");
            }
        });
        test("All finite tasks must complete and then satisfy full completion grace", delegate {
            var c = new FakeClock(); var e = IdleEngine(c); var t = new TaskRegistry(); var p = new WorkloadPolicy(c, t); var s = Settings();
            t.Set("a", TaskState.Completed); t.Set("b", TaskState.Active); p.Refresh(s, e); check(!p.CoreEligible && p.Protect, "second task still active");
            t.Set("b", TaskState.Completed); p.Refresh(s, e); check(!p.CoreEligible && !p.Protect, "completion starts grace and releases");
            c.Advance(59); p.Refresh(s, e); check(!p.CoreEligible, "full grace"); c.Advance(1); p.Refresh(s, e);
            check(p.CoreEligible && p.Observation == WorkObservation.ConfirmedCompleted, "confirmed completion eligible");
            t.Set("new", TaskState.Active); p.Refresh(s, e); check(!p.CoreEligible && p.Protect, "new work cancels eligibility");
        });
        test("Unknown policy is explicit and unsupported integrations never complete", delegate {
            var c = new FakeClock(); var e = IdleEngine(c); var t = new TaskRegistry(); var p = new WorkloadPolicy(c, t); var s = Settings();
            p.Refresh(s, e); check(p.Observation == WorkObservation.NoMonitoredTask && !p.CoreEligible && !p.Protect, "open IDE unregistered");
            t.Set("a", TaskState.Active); p.Refresh(s, e); t.Set("a", TaskState.Unknown); p.Refresh(s, e);
            check(p.Protect && !p.CoreEligible && p.Observation == WorkObservation.Unknown, "default holds known work");
            s.UnknownWorkPolicy = "ReleaseProtection"; p.Refresh(s, e); check(!p.Protect && !p.CoreEligible, "release policy still blocks auto action");
            p = new WorkloadPolicy(c, new MissingTasks()); p.Refresh(s, e); check(p.Observation == WorkObservation.Unknown && !p.CoreEligible, "unavailable source never completes");
        });
        test("Failed or cancelled tasks block automatic completion without perpetual lease", delegate {
            foreach (TaskState state in new[] { TaskState.Failed, TaskState.Cancelled }) {
                var c = new FakeClock(); var e = IdleEngine(c); var t = new TaskRegistry(); t.Set("a", state); var p = new WorkloadPolicy(c, t); p.Refresh(Settings(), e);
                check(!p.Protect && !p.CoreEligible && p.Observation == WorkObservation.Unknown, "owner review required");
            }
        });
        test("Resume invalidates completed task evidence", delegate {
            var c = new FakeClock(); var e = IdleEngine(c); var t = new TaskRegistry(); t.Set("a", TaskState.Completed); var p = new WorkloadPolicy(c, t); p.Refresh(Settings(), e); c.Advance(60); p.Refresh(Settings(), e); check(p.CoreEligible, "before resume");
            p.Resume(); p.Refresh(Settings(), e); check(!p.CoreEligible && p.Observation == WorkObservation.Unknown, "stale completion cannot sleep");
        });
        test("Codex fixture requires successful exit and fully drained terminal event", delegate {
            var t = new CodexTurnTracker(); t.Observe("{\"type\":\"thread.started\",\"thread_id\":\"fixture\"}"); t.Observe("{\"type\":\"turn.started\"}"); t.Observe("{\"type\":\"turn.completed\",\"usage\":{}}");
            check(t.State(false, 0, true) == TaskState.Active && t.State(true, 0, false) == TaskState.Unknown, "turn event alone not session exit");
            check(t.State(true, 0, true) == TaskState.Completed, "bounded successful session");
            check(t.State(true, 1, true) == TaskState.Failed, "nonzero exit overrides turn");
        });
        test("Malformed truncated error and unfinished Codex fixtures never complete", delegate {
            foreach (string line in new[] { "garbage", "{}", "{\"type\":null}", "{\"type\":\"turn.completed\"}", "{\"type\":\"error\"}" }) {
                var t = new CodexTurnTracker(); t.Observe(line); check(t.State(true, 0, true) == TaskState.Unknown && t.State(false, 0, false) == TaskState.Active, "invalid protocol cannot complete; retained running process remains protected");
            }
            var pending = new CodexTurnTracker(); pending.Observe("{\"type\":\"turn.started\"}"); pending.Observe("{\"type\":\"item.started\",\"item\":{\"id\":\"x\"}}"); pending.Observe("{\"type\":\"turn.completed\"}");
            check(pending.State(true, 0, true) == TaskState.Unknown, "unfinished item blocks");
            var failed = new CodexTurnTracker(); failed.Observe("{\"type\":\"turn.failed\"}"); check(failed.State(true, 0, true) == TaskState.Failed, "failure not completion");
            failed.Observe("{\"type\":\"turn.started\"}"); failed.Observe("{\"type\":\"turn.completed\"}"); check(failed.State(true, 0, true) == TaskState.Failed, "later turn cannot erase a failed turn");
        });
        test("Codex new-turn generation changes even between status polls", delegate {
            var t = new CodexTurnTracker(); t.Observe("{\"type\":\"turn.started\"}"); t.Observe("{\"type\":\"turn.completed\"}"); long old = t.Revision;
            t.Observe("{\"type\":\"turn.started\"}"); check(t.Revision > old && t.State(false, 0, false) == TaskState.Active, "new turn resets grace");
            t.Observe(new string('x', 262145)); check(t.State(true, 0, true) == TaskState.Unknown, "oversized JSON blocked");
        });
        test("Owned task launch is blocked by verification interlock", delegate {
            bool blocked = false; try { using (var t = new OwnedTaskSession("nonexistent-codex.exe", new string[0], true)) { } } catch (InvalidOperationException) { blocked = true; }
            check(blocked, "no production task or API session launched");
            check(OwnedTaskSession.JoinArguments(new[] { "a b", "", "a\"b", "c\\" }) == "\"a b\" \"\" \"a\\\"b\" \"c\\\\\"", "Windows argument quoting");
        });
        test("Configured charger debounce rejects bounce and dispatches once with watchdog off", delegate {
            var c = new FakeClock(); var g = new ChargerGuard(c); var r = new RecordingActionExecutor(); var a = new PowerActionArbiter(new ActionDispatcher(r), new RecordingKeepAwakeLease(), null);
            g.Observe(ChargerState.Online, true, 1000); g.Observe(ChargerState.Offline, true, 1000); c.Advance(.6); check(!g.Observe(ChargerState.Offline, true, 1000), "custom debounce");
            g.Observe(ChargerState.Online, true, 1000); g.Observe(ChargerState.Offline, true, 1000); c.Advance(1);
            if (g.Observe(ChargerState.Offline, true, 1000)) a.Request("Hibernate", ActionOrigin.Charger, null);
            c.Advance(10); check(!g.Observe(ChargerState.Offline, true, 1000) && r.Requests.Count == 1 && r.Requests[0].Action == "Hibernate", "single configured action"); a.Dispose();
        });
        test("UI watchdog-only settings preserve in-progress charger debounce", delegate {
            var c = new FakeClock(); var power = new ScriptedPower(); var r = new RecordingActionExecutor();
            using (var f = new MainForm(true, c, new ScriptedProcesses(), power, r)) {
                power.State = ChargerState.Offline; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); c.Advance(.25);
                var s = (AppSettings)Field(f, "settings"); s.WatchdogGraceMinutes = 5; Call(f, "SaveSettings"); c.Advance(.25); Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty);
                check(r.Requests.Count == 1 && r.Requests[0].Origin == ActionOrigin.Charger, "watchdog save cannot disarm charger");
            }
        });
        test("UI resume refreshes charger AC state and invalidates task evidence", delegate {
            var c = new FakeClock(); var power = new ScriptedPower(); var r = new RecordingActionExecutor(); var t = new TaskRegistry(); t.Set("a", TaskState.Completed);
            using (var f = new MainForm(true, c, new ScriptedProcesses(), power, r, null, null, t)) {
                power.State = ChargerState.Offline; Call(f, "HandleResume"); c.Advance(1); Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); check(r.Requests.Count == 0 && t.Read().Unknown == 1, "battery resume not a fabricated unplug");
                power.State = ChargerState.Online; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); power.State = ChargerState.Offline; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); c.Advance(.5); Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); check(r.Requests.Count == 1, "fresh AC re-arms");
            }
        });
        test("UI confirmed completed task and inactive user reaches fake watchdog action", delegate {
            var c = new FakeClock(); var p = new ScriptedProcesses(); var r = new RecordingActionExecutor(); var t = new TaskRegistry(); var input = new ScriptedUser(); t.Set("a", TaskState.Completed);
            using (var f = new MainForm(true, c, p, new ScriptedPower(), r, input, null, t)) {
                f.Show(); var s = (AppSettings)Field(f, "settings"); s.WatchdogEnabled = true; s.WatchdogEvidenceMode = "TaskAware"; s.WatchdogGraceMinutes = 1; s.CountdownSeconds = 1;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5); c.Advance(1); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5);
                var w = (WorkloadPolicy)Field(f, "workload"); w.Refresh(s, e); for (int i = 0; i < 60; i++) { c.Advance(1); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5); } w.Refresh(s, e);
                using (var timer = new Timer()) {
                    timer.Interval = 100; timer.Tick += delegate { timer.Stop(); c.Advance(1); Call(Field(f, "activeCountdown"), "CountdownTimer_Tick", null, EventArgs.Empty); }; timer.Start();
                    Call(f, "TriggerCountdownAndAction", false);
                }
                check(r.Requests.Count == 1 && r.Requests[0].Origin == ActionOrigin.Watchdog, "real action path, recording executor only: " + string.Join(" | ", new List<string>(LogItems(f)).ToArray()));
            }
        });
        test("UI new authoritative work cancels pending watchdog countdown", delegate {
            var c = new FakeClock(); var p = new ScriptedProcesses(); var r = new RecordingActionExecutor(); var t = new TaskRegistry(); t.Set("a", TaskState.Completed);
            using (var f = new MainForm(true, c, p, new ScriptedPower(), r, null, null, t))
            using (var timer = new Timer()) {
                f.Show(); var s = (AppSettings)Field(f, "settings"); s.WatchdogEnabled = true; s.WatchdogEvidenceMode = "TaskAware"; s.WatchdogGraceMinutes = 1; s.CountdownSeconds = 1;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5); c.Advance(1); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5); var w = (WorkloadPolicy)Field(f, "workload"); w.Refresh(s, e); for (int i = 0; i < 60; i++) { c.Advance(1); e.Poll(s.WatchdogMode, s.WatchdogTargetProcess, 1000, 3.5); } w.Refresh(s, e);
                timer.Interval = 100; timer.Tick += delegate { timer.Stop(); t.Set("b", TaskState.Active); Call(f, "WatchdogTimer_Tick", null, EventArgs.Empty); }; timer.Start();
                Call(f, "TriggerCountdownAndAction", false); check(r.Requests.Count == 0, "new active task cancels");
            }
        });
    }
}
