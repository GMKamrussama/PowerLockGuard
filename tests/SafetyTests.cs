using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using PowerLockGuard;

internal sealed class ScriptedProcesses : IProcessSource
{
    public readonly List<ProcessObservation> Items = new List<ProcessObservation>();
    public bool Complete = true;
    public bool Throw;
    public ProcessSample Read(string mode, string target)
    {
        if (Throw) throw new UnauthorizedAccessException("fake access denied");
        ProcessSample s = new ProcessSample { Complete = Complete, Error = Complete ? null : "fake partial enumeration" };
        s.Processes.AddRange(Items); return s;
    }
}
internal sealed class ScriptedPower : IPowerStatusSource
{ public ChargerState State = ChargerState.Online; public ChargerState Read() { return State; } }
internal sealed class ScriptedUser : IUserActivitySource
{
    public int Idle = int.MaxValue;
    public bool Throw;
    public int IdleSeconds { get { if (Throw) throw new InvalidOperationException("input unavailable"); return Idle; } }
}
internal sealed class FakeHandle : IProcessHandle
{
    public ProcessIdentity Id;
    public bool Exited, Stopped, Disposed;
    public ProcessIdentity Identity { get { return Id; } }
    public bool HasExited { get { return Exited; } }
    public void RequestStop() { Stopped = true; }
    public void Dispose() { Disposed = true; }
}
internal sealed class FakeAccess : IProcessAccess
{
    public FakeHandle Handle;
    public bool Throw;
    public IProcessHandle Open(int pid) { if (Throw) throw new UnauthorizedAccessException("denied"); return Handle; }
}
internal sealed class ThrowingExecutor : IActionExecutor
{ public ActionResult Execute(ActionRequest r) { throw new InvalidOperationException("fake native failure"); } }
internal sealed class KeyCountdown : SafetyCountdownForm
{
    public KeyCountdown() : base("SIMULATION", 30, false) { }
    public void Key(Keys key) { OnKeyDown(new KeyEventArgs(key)); }
}
internal static class SafetyTests
{
    private static int passed, failed;
    private static readonly List<string> report = new List<string>();
    private static readonly DateTime Started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static string output;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    {
        try { body(); passed++; report.Add("PASS " + name); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; report.Add("FAIL " + name + ": " + ex); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static ProcessObservation Item(int pid, double cpu, int generation)
    { return new ProcessObservation { Name = "codex", Identity = new ProcessIdentity(pid, Started.AddSeconds(generation)), CpuTime = TimeSpan.FromSeconds(cpu) }; }
    private static WorkWatchdogEngine Engine(FakeClock c, ScriptedProcesses p, int processors)
    { return new WorkWatchdogEngine(c, p, processors); }
    private static void Poll(WorkWatchdogEngine e) { e.Poll("AgentsAndBuilds", "claude", 1000, 3.5); }
    private static void Active(FakeClock c, ScriptedProcesses p, WorkWatchdogEngine e)
    { c.Advance(1); p.Items.Add(Item(42, 0, 0)); Poll(e); c.Advance(1); p.Items[0].CpuTime = TimeSpan.FromSeconds(1); Poll(e); Assert(e.IsArmed, "must observe activity"); }
    private static void Idle(FakeClock c, WorkWatchdogEngine e, int seconds)
    { for (int i = 0; i < seconds; i++) { c.Advance(1); Poll(e); } }
    private static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    private static void Call(object obj, string name, params object[] args)
    { obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
    private static Button ButtonWith(Form form, string text)
    { foreach (Control c in form.Controls) if (c is Button && c.Text.Contains(text)) return (Button)c; throw new Exception("button missing"); }
    private static void Snapshot(Form form, string name)
    { using (Bitmap bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height)); bmp.Save(Path.Combine(output, name)); } }

    [STAThread]
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("POWERLOCKGUARD_NO_NATIVE_ACTIONS", "1");
        output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts");
        Directory.CreateDirectory(output);
        Test("CPU sampling warms up and normalizes across processors", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 4);
            p.Items.Add(Item(42, 0, 0)); Poll(e); Assert(!e.SampleHealthy, "first sample unknown");
            c.Advance(1); p.Items[0].CpuTime = TimeSpan.FromSeconds(1); Poll(e);
            Assert(e.SampleHealthy && e.LastSampledCpuPercent == 25 && e.HasActiveWork, "one busy core / 4 = 25%");
        });
        Test("High CPU never means stuck and legacy kill APIs are inert", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e);
            for (int i = 0; i < 700; i++) { c.Advance(1); p.Items[0].CpuTime += TimeSpan.FromSeconds(1); Poll(e); }
            Assert(!e.CurrentMetrics[0].IsStuck && !e.CanAct(60), "busy work protected");
            Assert(e.TerminateStuckProcesses().Count == 0 && e.AutoKillRunawayIfUserAway(60) == null, "no legacy kill behavior");
        });
        Test("Idle requires activity and a full continuous grace", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1);
            Idle(c, e, 200); Assert(!e.CanAct(60), "no targets at startup must not arm");
            Active(c, p, e); Idle(c, e, 60); Assert(!e.CanAct(60), "idle begins on first low sample");
            Idle(c, e, 1); Assert(e.CanAct(60), "full grace permits candidate");
        });
        Test("Activity returning at countdown expiry invalidates eligibility", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e); Idle(c, e, 61);
            c.Advance(1); p.Items[0].CpuTime += TimeSpan.FromSeconds(1); Poll(e); Assert(!e.CanAct(60), "resumed activity cancels");
        });
        Test("Failed and partial samples reset continuous idle", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e); Idle(c, e, 50);
            p.Complete = false; c.Advance(1); Poll(e); Assert(!e.SampleHealthy && e.ConsecutiveIdleSeconds == 0, "partial cannot count");
            p.Complete = true; p.Throw = true; c.Advance(1); Poll(e); Assert(!e.SampleHealthy && !e.CanAct(60), "exception cannot count");
            p.Throw = false; Idle(c, e, 20); Assert(!e.CanAct(60), "must earn fresh grace");
        });
        Test("PID reuse creates a new CPU baseline", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e);
            p.Items[0] = Item(42, 999, 10); c.Advance(1); Poll(e);
            Assert(!e.SampleHealthy && e.LastSampledCpuPercent == 0, "new instance not compared with old CPU");
        });
        Test("Exited targets allow idle only after prior activity", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e);
            p.Items.Clear(); Idle(c, e, 61); Assert(e.CanAct(60), "valid empty set after activity can idle");
        });
        Test("Rapid refresh cannot fabricate idle seconds", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e); Idle(c, e, 1);
            for (int i = 0; i < 100; i++) Poll(e); Assert(e.ConsecutiveIdleSeconds == 0, "refresh is not time");
        });
        Test("Clock gaps and backwards time disarm monitoring", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e);
            c.Advance(600); Poll(e); Assert(!e.IsArmed && !e.CanAct(60), "resume gap disarms");
            c.Seconds = 0; Poll(e); Assert(!e.SampleHealthy, "clock reversal fails closed");
        });
        Test("Restart and selection changes require new activity", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1); Active(c, p, e); Idle(c, e, 61);
            e.RestartMonitoring(); Assert(!e.CanAct(60), "enable resets");
            c.Advance(1); e.Poll("SpecificProcess", "python", 1000, 3.5); Assert(!e.IsArmed, "target reset");
        });
        Test("Threshold boundary and 32-core normalization remain explicit", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 32);
            p.Items.Add(Item(42, 0, 0)); Poll(e); c.Advance(1); p.Items[0].CpuTime = TimeSpan.FromSeconds(1); Poll(e);
            Assert(e.LastSampledCpuPercent == 3.125 && !e.IsArmed, "one core below 3.5% is not completion evidence");
            c.Advance(1); p.Items[0].CpuTime += TimeSpan.FromSeconds(1.12); Poll(e);
            Assert(e.HasActiveWork, "exact threshold is active");
        });
        Test("Ignore affects exactly one process instance", delegate {
            FakeClock c = new FakeClock(); ScriptedProcesses p = new ScriptedProcesses(); var e = Engine(c, p, 1);
            p.Items.Add(Item(42, 0, 0)); p.Items.Add(Item(43, 0, 0)); Poll(e); e.ToggleIgnoreProcess(42, "codex");
            Assert(e.IsProcessIgnored(42, "codex") && !e.IsProcessIgnored(43, "codex"), "same name stays monitored");
            p.Items[0] = Item(42, 0, 1); c.Advance(1); Poll(e); Assert(!e.IsProcessIgnored(42, "codex"), "PID reuse not ignored");
        });
        Test("Matching is exact and case insensitive with exe normalization", delegate {
            Assert(WorkWatchdogEngine.Matches("CODEX", "AIAgentsOnly", ""), "codex match");
            Assert(!WorkWatchdogEngine.Matches("unrelated-agent-service", "AllDevTools", ""), "substring false positive removed");
            Assert(WorkWatchdogEngine.Matches("python", "SpecificProcess", " Python.exe "), "custom exe match");
            Assert(!WorkWatchdogEngine.Matches("pythonw", "SpecificProcess", "python"), "custom exact");
        });
        Test("Charger Unknown cannot trigger or continue unplug", delegate {
            FakeClock c = new FakeClock(); ChargerGuard g = new ChargerGuard(c);
            g.Observe(ChargerState.Online, true); Assert(!g.Observe(ChargerState.Unknown, true), "unknown blocked");
            c.Advance(1); Assert(!g.Observe(ChargerState.Offline, true), "unknown breaks confirmation");
        });
        Test("Charger debounce, bounce, one-shot and reconnect", delegate {
            FakeClock c = new FakeClock(); ChargerGuard g = new ChargerGuard(c);
            Assert(!g.Observe(ChargerState.Offline, true), "startup battery no action");
            g.Observe(ChargerState.Online, true); g.Observe(ChargerState.Offline, true); c.Advance(0.25);
            Assert(!g.Observe(ChargerState.Offline, true), "debounce not expired");
            g.Observe(ChargerState.Online, true); g.Observe(ChargerState.Offline, true); c.Advance(0.5);
            Assert(g.Observe(ChargerState.Offline, true), "confirmed disconnect");
            c.Advance(5); Assert(!g.Observe(ChargerState.Offline, true), "one shot");
            g.Observe(ChargerState.Online, true); g.Observe(ChargerState.Offline, false); c.Advance(1);
            Assert(!g.Observe(ChargerState.Offline, true), "disable resets");
        });
        Test("Dispatcher blocks automatic kills and unknown actions", delegate {
            var r = new RecordingActionExecutor(); var d = new ActionDispatcher(r);
            foreach (ActionOrigin origin in new ActionOrigin[] { ActionOrigin.Watchdog, ActionOrigin.Charger, ActionOrigin.Demo })
                Assert(d.Request("Terminate", origin, new ProcessIdentity(42, Started)).Status == ActionStatus.Rejected, "automatic forbidden");
            Assert(d.Request("Typo", ActionOrigin.Manual, null).Status == ActionStatus.Rejected && r.Requests.Count == 0, "no fallback sleep");
        });
        Test("Every power action and origin is record-only in simulation", delegate {
            var r = new RecordingActionExecutor(); var d = new ActionDispatcher(r);
            foreach (string action in new string[] { "Sleep", "Hibernate", "Shutdown", "Lock" })
                foreach (ActionOrigin origin in Enum.GetValues(typeof(ActionOrigin))) Assert(d.Request(action, origin, null).Status == ActionStatus.Simulated, "record only");
            Assert(d.Request("Terminate", ActionOrigin.Manual, new ProcessIdentity(42, Started)).Status == ActionStatus.Simulated, "manual also simulated");
            Assert(r.Requests.Count == 17, "all requests recorded");
        });
        Test("Executor exceptions produce failure evidence", delegate {
            var d = new ActionDispatcher(new ThrowingExecutor()); Assert(d.Request("Sleep", ActionOrigin.Watchdog, null).Status == ActionStatus.Failed, "failure surfaced");
            Assert(WindowsActionExecutor.ShutdownArguments == "/s /t 0", "no forced shutdown");
        });
        Test("Verification interlock forbids production adapters", delegate {
            bool denied = false;
            try { new WindowsActionExecutor(); } catch (InvalidOperationException) { denied = true; }
            Assert(denied, "production adapter must not initialize under test interlock");
        });
        Test("Manual termination rejects PID reuse and disposes handle", delegate {
            var h = new FakeHandle { Id = new ProcessIdentity(42, Started.AddSeconds(1)) }; var a = new FakeAccess { Handle = h };
            var result = ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Manual, new ProcessIdentity(42, Started)), a, 99);
            Assert(result.Status == ActionStatus.Rejected && !h.Stopped && h.Disposed, "wrong instance protected");
        });
        Test("Manual termination rejects exited, self, automatic and denied targets", delegate {
            var id = new ProcessIdentity(42, Started); var h = new FakeHandle { Id = id, Exited = true }; var a = new FakeAccess { Handle = h };
            Assert(ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Manual, id), a, 99).Status == ActionStatus.Rejected && !h.Stopped, "exit protected");
            Assert(ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Manual, id), a, 42).Status == ActionStatus.Rejected, "self protected");
            Assert(ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Watchdog, id), a, 99).Status == ActionStatus.Rejected, "automatic protected");
            a.Throw = true; Assert(ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Manual, id), a, 99).Status == ActionStatus.Failed, "access denial surfaced");
        });
        Test("Manual identity match requests stop only on fake handle", delegate {
            var id = new ProcessIdentity(42, Started); var h = new FakeHandle { Id = id };
            Assert(ManualTermination.Execute(new ActionRequest("Terminate", ActionOrigin.Manual, id), new FakeAccess { Handle = h }, 99).Status == ActionStatus.Requested && h.Stopped && h.Disposed, "fake request and cleanup");
        });
        Test("Config legacy migration and automatic kill disabled", delegate {
            string path = Path.Combine(output, "legacy.ini"); File.WriteAllText(path, "IsEnabled=False\nWatchdogMode=AutoDev\nAutoKillStuckAgentsOnSleep=True\n");
            var s = AppSettings.Load(path); Assert(!s.ChargerGuardEnabled && s.WatchdogMode == "AgentsAndBuilds" && !s.AutoKillStuckAgentsOnSleep && s.Warnings.Count > 0, "legacy preserved safely");
        });
        Test("Malformed config values disable automatic guards", delegate {
            foreach (string value in new string[] { "WatchdogGraceMinutes=abc", "WatchdogGraceMinutes=-1", "WatchdogGraceMinutes=2147483648", "WatchdogIdleThreshold=NaN", "WatchdogIdleThreshold=Infinity", "CountdownSeconds=0", "CountdownSeconds=-5", "WatchdogAction=Typo", "WatchdogMode=Typo", "ChargerGuardEnabled=garbage" })
            {
                string path = Path.Combine(output, "bad.ini"); File.WriteAllText(path, "WatchdogEnabled=True\nChargerGuardEnabled=True\n" + value);
                var s = AppSettings.Load(path); Assert((value.StartsWith("Charger") ? s.WatchdogEnabled && !s.ChargerGuardEnabled : !s.WatchdogEnabled && s.ChargerGuardEnabled) && s.Warnings.Count > 0, value);
            }
        });
        Test("Config atomically round-trips and surfaces write failure", delegate {
            string path = Path.Combine(output, "roundtrip.ini"); var s = new AppSettings(path); Assert(s.Save(), "initial save");
            s.WatchdogGraceMinutes = 10; Assert(s.Save() && AppSettings.Load(path).WatchdogGraceMinutes == 10, "replace succeeds");
            var bad = new AppSettings(Path.Combine(output, "missing", "config.ini")); Assert(!bad.Save() && bad.LastError != null, "failure reported");
            var ephemeral = new AppSettings(Path.Combine(output, "must-not-exist.ini")) { Ephemeral = true }; Assert(ephemeral.Save() && !File.Exists(Path.Combine(output, "must-not-exist.ini")), "simulation does not write");
            var unreadable = AppSettings.Load(output); Assert(!unreadable.ChargerGuardEnabled && !unreadable.WatchdogEnabled && unreadable.LastError != null, "unreadable path disables guards");
        });
        Test("Headless simulation generates actual scenario evidence", delegate {
            string file = Path.Combine(output, "simulation.log"); Assert(SimulationRunner.Run("all", file) == 0, "scenario passes");
            string text = File.ReadAllText(file); Assert(text.Contains("Simulated: Would Sleep (Watchdog)") && text.Contains("Simulated: Would Sleep (Charger)") && text.Contains("Native actions invoked: 0"), "expected branches reached");
            Assert(SimulationRunner.Run("unknown", file) == 2, "unknown scenario fails");
        });
        Test("Unknown CLI options cannot silently start live mode", delegate {
            Program.Main(new string[] { "--dryrun" }); Assert(Environment.ExitCode == 2, "typo rejected");
            Program.Main(new string[] { "--dry-run", "unexpected" }); Assert(Environment.ExitCode == 2, "extra options rejected");
            Environment.ExitCode = 0;
        });

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Test("UI countdown X close defaults to cancellation", delegate { using (var f = new SafetyCountdownForm("SIMULATION", 30, false)) { f.Show(); Application.DoEvents(); f.Close(); Assert(f.WasCancelled, "X close cancels"); } });
        Test("UI countdown Escape, Space and Cancel cancel", delegate {
            foreach (Keys key in new Keys[] { Keys.Escape, Keys.Space }) using (var f = new KeyCountdown()) { f.Show(); f.Key(key); Assert(f.WasCancelled, "key cancellation"); }
            using (var f = new SafetyCountdownForm("SIMULATION", 30, false)) { f.Show(); ButtonWith(f, "CANCEL").PerformClick(); Assert(f.WasCancelled, "button cancels"); }
        });
        Test("UI Execute Now revalidates before approval", delegate {
            using (var f = new SafetyCountdownForm("SIMULATION", 30, false, delegate { return false; })) { f.Show(); ButtonWith(f, "Now").PerformClick(); Assert(f.WasCancelled, "eligibility false"); }
            using (var f = new SafetyCountdownForm("SIMULATION: Sleep", 30, false, delegate { return true; })) { f.Show(); Snapshot(f, "countdown-ui.png"); ButtonWith(f, "Now").PerformClick(); Assert(!f.WasCancelled, "explicit approval"); }
        });
        Test("UI countdown expiry approves only while eligible", delegate {
            using (var f = new SafetyCountdownForm("SIMULATION", 1, false, delegate { return true; })) { f.Show(); Call(f, "CountdownTimer_Tick", null, EventArgs.Empty); Assert(!f.WasCancelled, "expiry approved"); }
            using (var f = new SafetyCountdownForm("SIMULATION", 1, false, delegate { return false; })) { f.Show(); Call(f, "CountdownTimer_Tick", null, EventArgs.Empty); Assert(f.WasCancelled, "invalid expiry cancels"); }
        });
        Test("UI simulation initializes, renders and uses recording actions", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses(); var power = new ScriptedPower();
            using (var f = new MainForm(true, c, p, power, recording))
            {
                f.Show(); Application.DoEvents(); Assert(f.Text.Contains("SIMULATION"), "visible label");
                var settings = (AppSettings)Field(f, "settings"); Assert(settings.Ephemeral && !settings.StartWithWindows, "ephemeral startup disabled");
                Call(f, "RequestAction", "Sleep", ActionOrigin.Manual, null); Assert(recording.Requests.Count == 1, "manual route recording");
                Call(f, "SetWatchdogEnabled", true); Assert(!((WorkWatchdogEngine)Field(f, "watchdogEngine")).IsArmed, "toggle rearm");
                power.State = ChargerState.Online; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty);
                power.State = ChargerState.Offline; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); c.Advance(0.5); Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty);
                Assert(recording.Requests.Count == 2 && recording.Requests[1].Origin == ActionOrigin.Charger, "charger route recording");
                Snapshot(f, "simulation-ui.png");
                Call(f, "SwitchTab", 0); Snapshot(f, "charger-ui.png");
                Call(f, "SwitchTab", 2); Snapshot(f, "settings-ui.png");
            }
        });
        Test("UI dry-run enable restarts the synthetic workload after idle", delegate {
            var c = new FakeClock();
            using (var f = new MainForm(true, c, null, new ScriptedPower(), new RecordingActionExecutor()))
            {
                c.Seconds = 100; Call(f, "SetWatchdogEnabled", true);
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Poll(e); c.Advance(1); Poll(e);
                Assert(e.IsArmed && e.HasActiveWork, "late enable receives new synthetic activity");
            }
        });
        Test("UI warning preview preserves genuine charger protection", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var power = new ScriptedPower();
            using (var f = new MainForm(true, c, new ScriptedProcesses(), power, recording))
            using (var trigger = new Timer())
            {
                f.Show(); ((AppSettings)Field(f, "settings")).CountdownSeconds = 1;
                trigger.Interval = 100; trigger.Tick += delegate { power.State = ChargerState.Offline; Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); c.Advance(0.5); Call(f, "PowerMonitorTimer_Tick", null, EventArgs.Empty); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", true); trigger.Stop();
                Assert(recording.Requests.Count == 1 && recording.Requests[0].Origin == ActionOrigin.Charger && !(bool)Field(f, "isCountdownActive"), "genuine charger routed to fake adapter while preview cancelled");
            }
        });
        Test("UI running countdown cancels when activity returns", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses();
            using (var f = new MainForm(true, c, p, new ScriptedPower(), recording))
            using (var trigger = new Timer())
            {
                f.Show(); var settings = (AppSettings)Field(f, "settings"); settings.WatchdogEnabled = true; settings.WatchdogGraceMinutes = 1; settings.CountdownSeconds = 1; settings.PlayAlertSound = false;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Active(c, p, e); Idle(c, e, 61);
                trigger.Interval = 100; trigger.Tick += delegate { trigger.Stop(); c.Advance(1); p.Items[0].CpuTime += TimeSpan.FromSeconds(4); Call(f, "WatchdogTimer_Tick", null, EventArgs.Empty); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", false);
                Assert(recording.Requests.Count == 0 && !(bool)Field(f, "isCountdownActive"), "resumed activity cancels full UI orchestration");
            }
        });
        Test("UI final fresh sample catches replacement after Execute Now", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses();
            using (var f = new MainForm(true, c, p, new ScriptedPower(), recording))
            using (var trigger = new Timer())
            {
                f.Show(); var settings = (AppSettings)Field(f, "settings"); settings.WatchdogEnabled = true; settings.WatchdogGraceMinutes = 1; settings.CountdownSeconds = 1; settings.PlayAlertSound = false;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Active(c, p, e); Idle(c, e, 61);
                trigger.Interval = 100; trigger.Tick += delegate { trigger.Stop(); c.Advance(1); p.Items[0] = Item(42, 0, 5); ButtonWith((Form)Field(f, "activeCountdown"), "Now").PerformClick(); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", false);
                Assert(recording.Requests.Count == 0, "replacement invalidates final sample");
            }
        });
        Test("UI settings change cancels pending action and tray shares reset path", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses();
            using (var f = new MainForm(true, c, p, new ScriptedPower(), recording))
            using (var trigger = new Timer())
            {
                f.Show(); var settings = (AppSettings)Field(f, "settings"); settings.WatchdogEnabled = true; settings.WatchdogGraceMinutes = 1; settings.CountdownSeconds = 1; settings.PlayAlertSound = false;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Active(c, p, e); Idle(c, e, 61);
                trigger.Interval = 100; trigger.Tick += delegate { trigger.Stop(); settings.WatchdogAction = "Hibernate"; Call(f, "SaveSettings"); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", false);
                Assert(recording.Requests.Count == 0 && !e.IsArmed, "settings cancellation");
                ContextMenuStrip menu = (ContextMenuStrip)Field(f, "trayMenu");
                foreach (ToolStripItem entry in menu.Items) if (entry.Text == "Toggle Work Watchdog") { entry.PerformClick(); entry.PerformClick(); }
                Assert(!e.IsArmed && e.ConsecutiveIdleSeconds == 0, "tray toggle resets eligibility");
            }
        });
        Test("User input and unavailable telemetry block automatic eligibility", delegate {
            var input = new ScriptedUser { Idle = 0 };
            Assert(!UserActivityPolicy.IsIdle(input, 60), "recent input blocks");
            input.Idle = -1; Assert(!UserActivityPolicy.IsIdle(input, 60), "unknown blocks");
            input.Throw = true; Assert(!UserActivityPolicy.IsIdle(input, 60), "failure blocks");
            input.Throw = false; input.Idle = 60; Assert(UserActivityPolicy.IsIdle(input, 60), "boundary permits eligibility");
        });
        Test("UI automatic countdown cancels when keyboard or mouse activity resumes", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses(); var input = new ScriptedUser();
            using (var f = new MainForm(true, c, p, new ScriptedPower(), recording, input))
            using (var trigger = new Timer())
            {
                f.Show(); var settings = (AppSettings)Field(f, "settings"); settings.WatchdogEnabled = true; settings.WatchdogGraceMinutes = 1; settings.CountdownSeconds = 1;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Active(c, p, e); Idle(c, e, 61);
                trigger.Interval = 100; trigger.Tick += delegate { trigger.Stop(); input.Idle = 0; Call(f, "WatchdogTimer_Tick", null, EventArgs.Empty); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", false);
                Assert(recording.Requests.Count == 0, "human activity cancels automatic action");
            }
        });
        Test("UI explicit Execute Now remains manual but revalidates CPU", delegate {
            var recording = new RecordingActionExecutor(); var c = new FakeClock(); var p = new ScriptedProcesses(); var input = new ScriptedUser();
            using (var f = new MainForm(true, c, p, new ScriptedPower(), recording, input))
            using (var trigger = new Timer())
            {
                f.Show(); var settings = (AppSettings)Field(f, "settings"); settings.WatchdogEnabled = true; settings.WatchdogGraceMinutes = 1; settings.CountdownSeconds = 1;
                var e = (WorkWatchdogEngine)Field(f, "watchdogEngine"); Active(c, p, e); Idle(c, e, 61);
                trigger.Interval = 100; trigger.Tick += delegate { trigger.Stop(); c.Advance(1); input.Idle = 0; ButtonWith((Form)Field(f, "activeCountdown"), "Now").PerformClick(); }; trigger.Start();
                Call(f, "TriggerCountdownAndAction", false);
                Assert(recording.Requests.Count == 1 && recording.Requests[0].Origin == ActionOrigin.Manual, "explicit approval is a simulated manual request");
            }
        });
        FunctionalTests.Run(Test, Assert, output);
        report.Add("Passed: " + passed + "; Failed: " + failed + "; real power/kill calls: none by test design");
        File.WriteAllLines(Path.Combine(output, "test-results.txt"), report.ToArray());
        Console.WriteLine(report[report.Count - 1]);
        return failed == 0 ? 0 : 1;
    }
}
