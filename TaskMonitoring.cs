using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace PowerLockGuard
{
    public enum TaskState { Active, Waiting, Completed, Failed, Cancelled, Unknown }
    public enum WorkObservation { NoMonitoredTask, HeuristicActivity, ConfirmedActive, ConfirmedCompleted, Unknown }
    public static class FiniteTaskOutcome
    {
        public static TaskState Command(bool exited, int exitCode, bool streamsDrained)
        {
            if (!exited) return TaskState.Active;
            if (exitCode != 0) return TaskState.Failed;
            return streamsDrained ? TaskState.Completed : TaskState.Unknown;
        }
    }
    public sealed class TaskSnapshot
    {
        public int Registered, Active, Unknown, Faulted;
        public long Generation;
        public bool AllCompleted { get { return Registered > 0 && Active == 0 && Unknown == 0 && Faulted == 0; } }
    }
    public interface ITaskStatusSource { TaskSnapshot Read(); void OnResume(); }
    public sealed class TaskRegistry : ITaskStatusSource
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, TaskState> states = new Dictionary<string, TaskState>();
        private long generation;
        public void Set(string id, TaskState state)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Task identity required");
            lock (gate) { TaskState old; if (!states.TryGetValue(id, out old) || old != state) { states[id] = state; generation++; } }
        }
        public TaskSnapshot Read()
        {
            lock (gate)
            {
                TaskSnapshot s = new TaskSnapshot { Registered = states.Count, Generation = generation };
                foreach (TaskState state in states.Values)
                    if (state == TaskState.Active || state == TaskState.Waiting) s.Active++;
                    else if (state == TaskState.Failed || state == TaskState.Cancelled) s.Faulted++;
                    else if (state != TaskState.Completed) s.Unknown++;
                return s;
            }
        }
        public void OnResume() { lock (gate) { foreach (string key in new List<string>(states.Keys)) states[key] = TaskState.Unknown; generation++; } }
    }
    public sealed class WorkloadPolicy
    {
        private readonly IClock clock;
        private readonly ITaskStatusSource tasks;
        private long generation = -1;
        private double completedSince = -1;
        private bool knownWork;
        public WorkObservation Observation { get; private set; }
        public bool Protect { get; private set; }
        public bool CoreEligible { get; private set; }
        public string Summary { get; private set; }
        public WorkloadPolicy(IClock clock, ITaskStatusSource tasks) { this.clock = clock; this.tasks = tasks; }
        public void Reset() { completedSince = -1; generation = -1; CoreEligible = false; }
        public void Resume() { tasks.OnResume(); Reset(); }
        public void Refresh(AppSettings settings, WorkWatchdogEngine cpu)
        {
            CoreEligible = false; Protect = false;
            TaskSnapshot s;
            try { s = tasks.Read(); if (s == null) throw new InvalidOperationException(); }
            catch { s = new TaskSnapshot { Registered = 1, Unknown = 1, Generation = long.MinValue }; }
            if (generation != s.Generation) { completedSince = -1; generation = s.Generation; }
            if (!settings.WatchdogEnabled) { Observation = WorkObservation.NoMonitoredTask; knownWork = false; Summary = "Work Watchdog paused"; return; }
            int grace = settings.WatchdogGraceMinutes * 60;
            if (s.Active > 0)
            { Observation = WorkObservation.ConfirmedActive; knownWork = true; Protect = true; Summary = "Confirmed active/waiting task; low CPU is not completion."; }
            else if (s.Faulted > 0)
            { Observation = WorkObservation.Unknown; knownWork = false; Summary = "Finite task failed/cancelled; lease released, automatic action blocked for review."; }
            else if (s.Unknown > 0)
            { Observation = WorkObservation.Unknown; Protect = knownWork && settings.UnknownWorkPolicy == "HoldKnownWork"; Summary = "Task unknown/failed/cancelled; automatic action blocked."; }
            else if (!cpu.SampleHealthy)
            { Observation = WorkObservation.Unknown; Protect = knownWork && settings.UnknownWorkPolicy == "HoldKnownWork"; Summary = "Process telemetry unavailable/warming; automatic action blocked."; }
            else if (cpu.HasActiveWork)
            { Observation = WorkObservation.HeuristicActivity; knownWork = true; Protect = true; Summary = "Heuristic CPU activity; completion unverified."; }
            else if (s.AllCompleted)
            {
                Observation = WorkObservation.ConfirmedCompleted; knownWork = false;
                if (completedSince < 0) completedSince = clock.Seconds;
                CoreEligible = grace > 0 && clock.Seconds - completedSince >= grace;
                Summary = "All registered finite tasks succeeded; completion/user grace required.";
            }
            else if (settings.WatchdogEvidenceMode == "Heuristic")
            {
                Observation = WorkObservation.NoMonitoredTask; CoreEligible = cpu.CanAct(grace);
                Protect = cpu.IsArmed; // Explicit legacy mode protects its buffer/countdown from OS idle sleep.
                Summary = "Legacy CPU heuristic; low-CPU work may still be running.";
            }
            else { Observation = WorkObservation.NoMonitoredTask; knownWork = false; Summary = "No registered task; an IDE window is not task completion."; }
            if (!s.AllCompleted || cpu.HasActiveWork || !cpu.SampleHealthy) completedSince = -1;
            Protect = Protect && settings.KeepAwakeEnabled;
        }
    }
    // Retains event type/id/status only. Never logs or stores prompt/response/reasoning text.
    public sealed class CodexTurnTracker
    {
        // Defensive budgets, not protocol guarantees: allow large opaque IDs and many
        // concurrent items while retaining at most 128 KiB of UTF-16 ID payload.
        // Count also bounds string/HashSet overhead; completed items return the budget.
        public const int MaxPendingItems = 1024;
        public const int MaxItemIdLength = 1024;
        public const int MaxPendingIdCharacters = 65536;
        private bool started, completed, invalid, failed;
        private readonly HashSet<string> pending = new HashSet<string>();
        private int pendingIdCharacters;
        public long Revision { get; private set; }
        private void Invalidate()
        {
            invalid = true; completed = false;
            pending.Clear(); pendingIdCharacters = 0; Revision++;
        }
        public void Observe(string line)
        {
            if (invalid) return;
            try
            {
                if (line == null || line.Length > 262144) throw new FormatException();
                var value = new JavaScriptSerializer { MaxJsonLength = 262144, RecursionLimit = 32 }.DeserializeObject(line) as Dictionary<string, object>;
                if (value == null || !value.ContainsKey("type")) throw new FormatException();
                string type = value["type"] as string;
                if (string.IsNullOrEmpty(type)) throw new FormatException();
                if (type == "turn.started") { if (started && !completed) throw new FormatException(); started = true; completed = false; Revision++; }
                else if (type == "turn.completed") { if (!started || completed) throw new FormatException(); completed = true; Revision++; }
                else if (type == "turn.failed") { failed = true; completed = false; Revision++; }
                else if (type == "error") { Invalidate(); }
                else if (type == "item.started" || type == "item.completed")
                {
                    object raw; var item = value.TryGetValue("item", out raw) ? raw as Dictionary<string, object> : null;
                    if (item == null) throw new FormatException();
                    object identity; string id = item.TryGetValue("id", out identity) ? identity as string : null;
                    if (string.IsNullOrEmpty(id) || id.Length > MaxItemIdLength) throw new FormatException();
                    if (type == "item.started")
                    {
                        if (!pending.Contains(id))
                        {
                            if (pending.Count >= MaxPendingItems || id.Length > MaxPendingIdCharacters - pendingIdCharacters)
                                throw new FormatException();
                            pending.Add(id); pendingIdCharacters += id.Length;
                        }
                    }
                    else if (pending.Remove(id)) pendingIdCharacters -= id.Length;
                    Revision++;
                }
            }
            catch { Invalidate(); }
        }
        public TaskState State(bool exited, int exitCode, bool outputDrained)
        {
            // A retained live process handle proves this owned session is still running,
            // even if its event protocol is unavailable. It does not prove task success.
            if (!exited) return TaskState.Active;
            if (invalid) return TaskState.Unknown;
            if (failed || (exited && exitCode != 0)) return TaskState.Failed;
            if (!outputDrained) return TaskState.Unknown;
            return started && completed && pending.Count == 0 ? TaskState.Completed : TaskState.Unknown;
        }
    }
    // Prepare drain workers before launching a child. An aborted setup releases
    // waiting workers without touching redirected streams or starting a process.
    public sealed class OwnedTaskStartup
    {
        private readonly object gate = new object();
        private bool started, ready, launched;
        public bool AwaitLaunch()
        {
            lock (gate) { while (!ready) Monitor.Wait(gate); return launched; }
        }
        public void Run(Action prepareOutput, Action prepareError, Func<bool> launch)
        {
            lock (gate)
            {
                if (started) throw new InvalidOperationException("Task startup is single-use");
                started = true;
            }
            bool success = false;
            try
            {
                prepareOutput(); prepareError();
                if (!launch()) throw new InvalidOperationException("Finite task did not start");
                success = true;
            }
            finally
            {
                lock (gate) { launched = success; ready = true; Monitor.PulseAll(gate); }
            }
        }
    }
    public sealed class OwnedTaskSession : ITaskStatusSource, IDisposable
    {
        private readonly object gate = new object();
        private readonly Process process;
        private readonly CodexTurnTracker codex;
        private readonly OwnedTaskStartup startup = new OwnedTaskStartup();
        private bool outputDrained, errorDrained, streamFailed, invalidated, disposed;
        private TaskState last = TaskState.Unknown;
        private long generation;
        public OwnedTaskSession(string executable, string[] arguments, bool codexSession)
        {
            NativeActionSafety.EnsureAllowed(); // Tests use fixtures; never launch an AI/build workload.
            if (!Path.IsPathRooted(executable) || !File.Exists(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select an existing native .exe using its absolute path; shell wrappers are not supported.");
            if (codexSession) codex = new CodexTurnTracker();
            process = new Process();
            var argv = new List<string>();
            if (codexSession) { argv.Add("exec"); argv.Add("--json"); }
            if (arguments != null) argv.AddRange(arguments);
            process.StartInfo = new ProcessStartInfo(executable, JoinArguments(argv.ToArray())) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            try
            {
                startup.Run(delegate { StartDrain(true); }, delegate { StartDrain(false); }, delegate { return process.Start(); });
            }
            catch { process.Dispose(); throw; } // Never kill a child.
        }
        private void StartDrain(bool stdout)
        {
            // Allocate buffers and start both threads before Process.Start can run.
            var line = new StringBuilder(); char[] buffer = new char[4096];
            var thread = new Thread(delegate()
            {
                StreamReader reader = null;
                try
                {
                    if (!startup.AwaitLaunch()) return;
                    reader = stdout ? process.StandardOutput : process.StandardError;
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (!stdout || codex == null) continue; // Discard command output/stderr without retaining it.
                        for (int i = 0; i < count; i++)
                        {
                            char ch = buffer[i];
                            if (ch == '\n') { lock (gate) { codex.Observe(line.ToString().TrimEnd('\r')); } line.Length = 0; }
                            else if (line.Length < 262144) line.Append(ch);
                            else { lock (gate) { codex.Observe(null); } line.Length = 0; }
                        }
                    }
                    if (stdout && codex != null && line.Length > 0) lock (gate) { codex.Observe(line.ToString()); }
                }
                catch { lock (gate) { streamFailed = true; } }
                finally
                {
                    try { if (reader != null) reader.Dispose(); } catch { lock (gate) { streamFailed = true; } }
                    lock (gate)
                    {
                        if (stdout) outputDrained = true; else errorDrained = true;
                        if (disposed && outputDrained && errorDrained)
                            try { process.Dispose(); } catch { streamFailed = true; }
                    }
                }
            });
            thread.IsBackground = true; thread.Start();
        }
        public TaskSnapshot Read()
        {
            lock (gate)
            {
                TaskState state = TaskState.Unknown;
                if (!disposed)
                {
                    try
                    {
                        bool exited = process.HasExited; int code = exited ? process.ExitCode : 0;
                        if (invalidated && !exited) invalidated = false; // Fresh retained handle confirms running after resume.
                        if (!invalidated && (!streamFailed || !exited)) state = codex == null ? FiniteTaskOutcome.Command(exited, code, outputDrained && errorDrained) :
                            codex.State(exited, code, outputDrained && errorDrained);
                    }
                    catch { state = TaskState.Unknown; }
                }
                if (last != state) { generation++; last = state; }
                return new TaskSnapshot { Registered = 1, Active = state == TaskState.Active || state == TaskState.Waiting ? 1 : 0,
                    Unknown = state == TaskState.Unknown ? 1 : 0, Faulted = state == TaskState.Failed || state == TaskState.Cancelled ? 1 : 0,
                    Generation = generation + (codex == null ? 0 : codex.Revision) };
            }
        }
        public void OnResume() { lock (gate) { invalidated = true; generation++; } }
        public void Dispose()
        {
            // Continue draining a running child: closing its stdout pipe can interrupt work.
            // Background readers own cleanup until EOF; closing this application never kills it.
            lock (gate)
            {
                if (disposed) return; disposed = true;
                if (outputDrained && errorDrained) process.Dispose();
            }
        }
        public static string JoinArguments(string[] arguments)
        {
            var result = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (result.Length != 0) result.Append(' ');
                result.Append('"'); int slashes = 0;
                foreach (char c in argument ?? "")
                {
                    if (c == '\\') { slashes++; continue; }
                    if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); slashes = 0; continue; }
                    result.Append('\\', slashes); slashes = 0; result.Append(c);
                }
                result.Append('\\', slashes * 2); result.Append('"');
            }
            return result.ToString();
        }
    }
}
