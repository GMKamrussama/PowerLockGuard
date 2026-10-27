using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PowerLockGuard
{
    public interface IClock { double Seconds { get; } }
    public sealed class MonotonicClock : IClock
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        public double Seconds { get { return watch.Elapsed.TotalSeconds; } }
    }
    public sealed class FakeClock : IClock
    {
        public double Seconds { get; set; }
        public void Advance(double seconds) { Seconds += seconds; }
    }

    public sealed class ProcessIdentity : IEquatable<ProcessIdentity>
    {
        public readonly int Pid;
        public readonly DateTime StartTimeUtc;
        public ProcessIdentity(int pid, DateTime startTimeUtc) { Pid = pid; StartTimeUtc = startTimeUtc; }
        public bool Equals(ProcessIdentity other) { return other != null && Pid == other.Pid && StartTimeUtc == other.StartTimeUtc; }
        public override bool Equals(object obj) { return Equals(obj as ProcessIdentity); }
        public override int GetHashCode() { return Pid.GetHashCode() ^ StartTimeUtc.GetHashCode(); }
        public override string ToString() { return Pid + "@" + StartTimeUtc.ToString("o"); }
    }
    public sealed class ProcessObservation
    {
        public ProcessIdentity Identity;
        public string Name;
        public TimeSpan CpuTime;
        public double MemoryMb;
    }
    public sealed class ProcessSample
    {
        public readonly List<ProcessObservation> Processes = new List<ProcessObservation>();
        public bool Complete = true;
        public string Error;
    }
    public interface IProcessSource { ProcessSample Read(string mode, string target); }

    public enum ActionOrigin { Manual, Watchdog, Charger, Demo }
    public sealed class ActionRequest
    {
        public readonly string Action;
        public readonly ActionOrigin Origin;
        public readonly ProcessIdentity Target;
        public ActionRequest(string action, ActionOrigin origin, ProcessIdentity target)
        { Action = action; Origin = origin; Target = target; }
    }
    public enum ActionStatus { Requested, Failed, Rejected, Simulated }
    public sealed class ActionResult
    {
        public readonly ActionStatus Status;
        public readonly string Message;
        public ActionResult(ActionStatus status, string message) { Status = status; Message = message; }
        public override string ToString() { return Status + ": " + Message; }
    }
    public interface IActionExecutor { ActionResult Execute(ActionRequest request); }
    public interface IProcessHandle : IDisposable
    {
        ProcessIdentity Identity { get; }
        bool HasExited { get; }
        void RequestStop();
    }
    public interface IProcessAccess { IProcessHandle Open(int pid); }
    public static class ManualTermination
    {
        public static ActionResult Execute(ActionRequest request, IProcessAccess access, int ownPid)
        {
            if (request.Action != "Terminate" || request.Origin != ActionOrigin.Manual || request.Target == null || request.Target.Pid <= 4 || request.Target.Pid == ownPid || request.Target.StartTimeUtc == DateTime.MinValue)
                return new ActionResult(ActionStatus.Rejected, "Confirmed manual identity required; protected/self process rejected");
            try
            {
                using (IProcessHandle process = access.Open(request.Target.Pid))
                {
                    if (process.HasExited || !request.Target.Equals(process.Identity))
                        return new ActionResult(ActionStatus.Rejected, "Process exited or identity changed");
                    process.RequestStop();
                    return new ActionResult(ActionStatus.Requested, "Termination requested for " + request.Target);
                }
            }
            catch (Exception ex) { return new ActionResult(ActionStatus.Failed, ex.Message); }
        }
    }
    public sealed class RecordingActionExecutor : IActionExecutor
    {
        public readonly List<ActionRequest> Requests = new List<ActionRequest>();
        public ActionResult Execute(ActionRequest request)
        {
            Requests.Add(request);
            return new ActionResult(ActionStatus.Simulated, "Would " + request.Action + " (" + request.Origin + ")");
        }
    }
    public sealed class ActionDispatcher
    {
        private readonly IActionExecutor executor;
        public ActionDispatcher(IActionExecutor executor)
        { if (executor == null) throw new ArgumentNullException("executor"); this.executor = executor; }
        public static bool IsPowerAction(string action)
        { return action == "Sleep" || action == "Hibernate" || action == "Shutdown" || action == "Lock"; }
        public ActionResult Request(string action, ActionOrigin origin, ProcessIdentity target)
        {
            if (!IsPowerAction(action) && action != "Terminate")
                return new ActionResult(ActionStatus.Rejected, "Unsupported action: " + action);
            if (action == "Terminate" && (origin != ActionOrigin.Manual || target == null || target.Pid <= 4 || target.StartTimeUtc == DateTime.MinValue))
                return new ActionResult(ActionStatus.Rejected, "Only explicitly confirmed manual termination of an identified process is supported.");
            try { return executor.Execute(new ActionRequest(action, origin, target)); }
            catch (Exception ex) { return new ActionResult(ActionStatus.Failed, ex.Message); }
        }
    }

    public enum ChargerState { Unknown, Online, Offline }
    public interface IPowerStatusSource { ChargerState Read(); }
    public sealed class ChargerGuard
    {
        private readonly IClock clock;
        private bool armed;
        private double offlineSince = -1;
        public ChargerGuard(IClock clock) { this.clock = clock; }
        public void Reset() { armed = false; offlineSince = -1; }
        public bool Observe(ChargerState state, bool enabled)
        {
            if (!enabled) { Reset(); return false; }
            if (state == ChargerState.Online) { armed = true; offlineSince = -1; return false; }
            // Unknown cannot establish or continue a confirmed unplug transition.
            if (state == ChargerState.Unknown) { Reset(); return false; }
            if (!armed) return false;
            if (offlineSince < 0) { offlineSince = clock.Seconds; return false; }
            if (clock.Seconds - offlineSince < 0.5) return false;
            Reset();
            return true;
        }
    }

    public sealed class CountdownDecision
    {
        public bool WasCancelled { get; private set; }
        public CountdownDecision() { WasCancelled = true; }
        public void Cancel() { WasCancelled = true; }
        public void Approve() { WasCancelled = false; }
    }

    public sealed class WorkWatchdogEngine
    {
        public static readonly string[] AIAgentPatterns = { "claude", "aider", "codex", "copilot", "gemini", "cline", "roo-cline", "continue", "agent" };
        public static readonly string[] BuildAndRuntimePatterns = { "node", "python", "pythonw", "cargo", "rustc", "dotnet", "javac", "java", "gradlew", "tsc", "git" };
        public static readonly string[] IDEPatterns = { "cursor", "code", "antigravity", "windsurf", "devenv", "idea64", "pycharm64", "webstorm64", "rider64", "sublime_text" };
        private readonly IClock clock;
        private readonly IProcessSource source;
        private readonly int processors;
        private Dictionary<ProcessIdentity, TimeSpan> previous = new Dictionary<ProcessIdentity, TimeSpan>();
        private readonly HashSet<ProcessIdentity> ignored = new HashSet<ProcessIdentity>();
        private double lastSample = -1;
        private double idleSince = -1;
        private string selection;
        public double LastSampledCpuPercent { get; private set; }
        public int MonitoredProcessCount { get; private set; }
        public string MonitoredProcessSummary { get; private set; }
        public bool HasActiveWork { get; private set; }
        public bool SampleHealthy { get; private set; }
        public bool IsArmed { get; private set; }
        public int ConsecutiveIdleSeconds { get { return idleSince < 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Max(0, clock.Seconds - idleSince)); } }
        public List<ProcessMetricItem> CurrentMetrics { get; private set; }
        public WorkWatchdogEngine() : this(new MonotonicClock(), new WindowsProcessSource(), Environment.ProcessorCount) { }
        public WorkWatchdogEngine(IClock clock, IProcessSource source, int processors)
        {
            this.clock = clock; this.source = source; this.processors = Math.Max(1, processors);
            CurrentMetrics = new List<ProcessMetricItem>(); MonitoredProcessSummary = "Waiting for a valid activity sample";
        }
        public static bool Matches(string name, string mode, string target)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (mode == "SpecificProcess")
            {
                target = (target ?? "").Trim();
                if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target = target.Substring(0, target.Length - 4);
                return target.Length > 0 && string.Equals(name, target, StringComparison.OrdinalIgnoreCase);
            }
            if (Array.Exists(AIAgentPatterns, delegate(string p) { return string.Equals(name, p, StringComparison.OrdinalIgnoreCase); })) return true;
            if (mode != "AIAgentsOnly" && Array.Exists(BuildAndRuntimePatterns, delegate(string p) { return string.Equals(name, p, StringComparison.OrdinalIgnoreCase); })) return true;
            return mode == "AllDevTools" && Array.Exists(IDEPatterns, delegate(string p) { return string.Equals(name, p, StringComparison.OrdinalIgnoreCase); });
        }
        public void ResetIdleTimer() { idleSince = -1; }
        public void RestartMonitoring()
        {
            ResetIdleTimer(); IsArmed = false; SampleHealthy = false; HasActiveWork = false;
            previous.Clear(); lastSample = -1;
        }
        public void ToggleIgnoreProcess(int pid, string name)
        {
            ProcessMetricItem item = CurrentMetrics.Find(delegate(ProcessMetricItem p) { return p.Pid == pid && p.Name == name; });
            if (item == null || item.Identity == null) return;
            if (!ignored.Remove(item.Identity)) ignored.Add(item.Identity);
            RestartMonitoring();
        }
        public bool IsProcessIgnored(int pid, string name)
        { return CurrentMetrics.Exists(delegate(ProcessMetricItem p) { return p.Pid == pid && p.Name == name && ignored.Contains(p.Identity); }); }
        public void ClearIgnored() { ignored.Clear(); RestartMonitoring(); }
        public bool CanAct(int graceSeconds)
        { return graceSeconds > 0 && SampleHealthy && IsArmed && !HasActiveWork && ConsecutiveIdleSeconds >= graceSeconds; }
        public void Poll(string mode, string target, int tickIntervalMs, double threshold, bool force = false)
        {
            string nextSelection = mode + "|" + target + "|" + threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (selection != nextSelection) { RestartMonitoring(); selection = nextSelection; }
            double now = clock.Seconds;
            double elapsed = lastSample < 0 ? 0 : now - lastSample;
            // Refresh clicks cannot fabricate time or destroy the previous baseline.
            if (!force && lastSample >= 0 && elapsed < 0.1 && elapsed >= 0) return;
            ProcessSample sample;
            try { sample = source.Read(mode, target); }
            catch (Exception ex) { sample = new ProcessSample(); sample.Complete = false; sample.Error = ex.Message; }
            if (sample == null) { sample = new ProcessSample(); sample.Complete = false; sample.Error = "No process sample"; }
            bool gap = elapsed < 0 || elapsed > 5;
            bool healthy = sample.Complete && lastSample >= 0 && !gap && elapsed > 0 && !double.IsNaN(threshold) && !double.IsInfinity(threshold) && threshold > 0 && threshold <= 100;
            if (gap) IsArmed = false;
            Dictionary<ProcessIdentity, TimeSpan> current = new Dictionary<ProcessIdentity, TimeSpan>();
            List<ProcessMetricItem> metrics = new List<ProcessMetricItem>();
            double total = 0;
            foreach (ProcessObservation p in sample.Processes)
            {
                if (p.Identity == null || p.Identity.StartTimeUtc == DateTime.MinValue || p.CpuTime < TimeSpan.Zero) { healthy = false; continue; }
                TimeSpan old;
                double cpu = 0;
                bool known = previous.TryGetValue(p.Identity, out old);
                if (!known || p.CpuTime < old) healthy = false;
                else if (elapsed > 0 && !gap) cpu = Math.Min(100, Math.Max(0, (p.CpuTime - old).TotalSeconds / elapsed / processors * 100));
                current[p.Identity] = p.CpuTime;
                bool skip = ignored.Contains(p.Identity);
                if (!skip) total += cpu;
                metrics.Add(new ProcessMetricItem { Pid = p.Identity.Pid, Identity = p.Identity, Name = p.Name, CpuPercent = cpu, MemoryMb = p.MemoryMb, IsIgnored = skip, IsActive = cpu >= 0.2, IsStuck = false });
            }
            ignored.RemoveWhere(delegate(ProcessIdentity id) { return !current.ContainsKey(id); });
            previous = current; lastSample = now;
            CurrentMetrics = metrics; MonitoredProcessCount = metrics.Count;
            LastSampledCpuPercent = Math.Min(100, total); SampleHealthy = healthy;
            // Bias floating-point boundary noise toward keeping the computer awake.
            HasActiveWork = healthy && total >= threshold - 1e-9;
            if (!healthy) ResetIdleTimer();
            else if (HasActiveWork) { IsArmed = true; ResetIdleTimer(); }
            else if (IsArmed && idleSince < 0) idleSince = now;
            MonitoredProcessSummary = !healthy ? "Unknown / warming up: " + (sample.Error ?? "fresh CPU baseline required") :
                string.Format("{0} processes, {1:F1}% CPU; {2}", metrics.Count, LastSampledCpuPercent, IsArmed ? "activity observed; completion unverified" : "waiting for activity before arming");
            metrics.Sort(delegate(ProcessMetricItem a, ProcessMetricItem b) { int cmp = a.IsIgnored.CompareTo(b.IsIgnored); return cmp != 0 ? cmp : b.CpuPercent.CompareTo(a.CpuPercent); });
        }
        // Legacy API kept inert so an old setting or caller cannot enable CPU-based killing.
        public List<string> TerminateStuckProcesses() { return new List<string>(); }
        public string AutoKillRunawayIfUserAway(int maxSeconds) { return null; }
    }
}
