using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PowerLockGuard
{
    public sealed class WindowsUserActivitySource : IUserActivitySource
    { public int IdleSeconds { get { return UserInactivityDetector.GetUserIdleSeconds(); } } }
    public sealed class WindowsProcessSource : IProcessSource
    {
        public ProcessSample Read(string mode, string target)
        {
            ProcessSample result = new ProcessSample();
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch (Exception ex) { result.Complete = false; result.Error = ex.Message; return result; }
            foreach (Process p in all)
            {
                using (p)
                {
                    try
                    {
                        if (p.Id == 0 || p.Id == 4) continue;
                        string name = p.ProcessName;
                        if (!WorkWatchdogEngine.Matches(name, mode, target)) continue;
                        ProcessObservation item = new ProcessObservation();
                        item.Name = name;
                        item.Identity = new ProcessIdentity(p.Id, p.StartTime.ToUniversalTime());
                        item.CpuTime = p.TotalProcessorTime;
                        try { item.MemoryMb = p.WorkingSet64 / (1024.0 * 1024.0); }
                        catch (Win32Exception) { item.MemoryMb = 0; } // RAM is display-only.
                        result.Processes.Add(item);
                    }
                    catch (Exception ex)
                    {
                        // An ordinary exit during enumeration is not a telemetry failure.
                        bool exited;
                        try { exited = p.HasExited; } catch { exited = false; }
                        if (!exited) { result.Complete = false; result.Error = ex.GetType().Name + ": " + ex.Message; }
                    }
                }
            }
            return result;
        }
    }
    public sealed class WindowsPowerStatusSource : IPowerStatusSource
    {
        public ChargerState Read()
        {
            try
            {
                PowerLineStatus state = SystemInformation.PowerStatus.PowerLineStatus;
                return state == PowerLineStatus.Online ? ChargerState.Online : state == PowerLineStatus.Offline ? ChargerState.Offline : ChargerState.Unknown;
            }
            catch { return ChargerState.Unknown; }
        }
    }
    public sealed class WindowsProcessAccess : IProcessAccess
    {
        public IProcessHandle Open(int pid) { NativeActionSafety.EnsureAllowed(); return new WindowsProcessHandle(pid); }
        private sealed class WindowsProcessHandle : IProcessHandle
        {
            private readonly Process process;
            public WindowsProcessHandle(int pid)
            {
                process = Process.GetProcessById(pid);
                try { IntPtr handle = process.Handle; }
                catch { process.Dispose(); throw; }
            }
            public ProcessIdentity Identity { get { return new ProcessIdentity(process.Id, process.StartTime.ToUniversalTime()); } }
            public bool HasExited { get { return process.HasExited; } }
            public void RequestStop() { process.Kill(); }
            public void Dispose() { process.Dispose(); }
        }
    }
    // The only production adapter permitted to terminate a process or change power state.
    // Tests and all simulation modes must never construct this class.
    public sealed class WindowsActionExecutor : IActionExecutor
    {
        public WindowsActionExecutor() { NativeActionSafety.EnsureAllowed(); }
        [DllImport("Powrprof.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool disableWake);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        public const string ShutdownArguments = "/s /t 0";
        public ActionResult Execute(ActionRequest request)
        {
            NativeActionSafety.EnsureAllowed();
            try
            {
                if (request.Action == "Terminate")
                {
                    using (Process self = Process.GetCurrentProcess())
                        return ManualTermination.Execute(request, new WindowsProcessAccess(), self.Id);
                }
                if (request.Action == "Shutdown")
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
                    using (Process p = Process.Start(new ProcessStartInfo(path, ShutdownArguments) { CreateNoWindow = true, UseShellExecute = false }))
                    {
                        if (p == null) return new ActionResult(ActionStatus.Failed, "Shutdown command did not start");
                        if (!p.WaitForExit(5000)) return new ActionResult(ActionStatus.Failed, "Shutdown command result timed out; outcome unknown");
                        return new ActionResult(p.ExitCode == 0 ? ActionStatus.Requested : ActionStatus.Failed, "Shutdown command exit code " + p.ExitCode + "; completion not observed");
                    }
                }
                if (!ActionDispatcher.IsPowerAction(request.Action)) return new ActionResult(ActionStatus.Rejected, "Unsupported action");
                bool ok = request.Action == "Lock" ? LockWorkStation() : SetSuspendState(request.Action == "Hibernate", false, false);
                return ok ? new ActionResult(ActionStatus.Requested, request.Action + " API returned success; hardware state not independently verified") :
                    new ActionResult(ActionStatus.Failed, request.Action + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
            catch (Exception ex) { return new ActionResult(ActionStatus.Failed, ex.GetType().Name + ": " + ex.Message); }
        }
    }
    public static class NativeActionSafety
    {
        public static void EnsureAllowed()
        {
            if (Environment.GetEnvironmentVariable("POWERLOCKGUARD_NO_NATIVE_ACTIONS") == "1")
                throw new InvalidOperationException("Native actions are forbidden in this verification process.");
        }
    }
}
