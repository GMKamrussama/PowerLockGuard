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
                if (request.Action == "Sleep" || request.Action == "Hibernate")
                    return SuspendAction.Execute(request.Action == "Hibernate", new WindowsSuspendNative());
                bool ok = LockWorkStation();
                return ok ? new ActionResult(ActionStatus.Requested, request.Action + " API returned success; hardware state not independently verified") :
                    new ActionResult(ActionStatus.Failed, request.Action + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
            catch (Exception ex) { return new ActionResult(ActionStatus.Failed, ex.GetType().Name + ": " + ex.Message); }
        }
    }
    public sealed class WindowsSuspendNative : ISuspendNative
    {
        public WindowsSuspendNative() { NativeActionSafety.EnsureAllowed(); }
        [DllImport("Powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate, [MarshalAs(UnmanagedType.U1)] bool force, [MarshalAs(UnmanagedType.U1)] bool disableWake);
        [DllImport("Powrprof.dll")][return: MarshalAs(UnmanagedType.U1)]
        private static extern bool IsPwrSuspendAllowed();
        [DllImport("Powrprof.dll")][return: MarshalAs(UnmanagedType.U1)]
        private static extern bool IsPwrHibernateAllowed();
        public bool Supports(bool hibernate)
        { NativeActionSafety.EnsureAllowed(); return hibernate ? IsPwrHibernateAllowed() : IsPwrSuspendAllowed(); }
        public IShutdownPrivilege EnableShutdownPrivilege() { NativeActionSafety.EnsureAllowed(); return new ShutdownPrivilege(); }
        public NativeCallResult Suspend(bool hibernate)
        {
            NativeActionSafety.EnsureAllowed();
            bool success = SetSuspendState(hibernate, false, false);
            int error = success ? 0 : Marshal.GetLastWin32Error();
            return new NativeCallResult(success, error);
        }
        [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] private struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string system, string name, out Luid luid);
        [DllImport("advapi32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, uint length, out TokenPrivileges previous, out uint returned);
        [DllImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RestoreTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, uint length, IntPtr previous, IntPtr returned);
        private sealed class ShutdownPrivilege : IShutdownPrivilege
        {
            private IntPtr token;
            private TokenPrivileges previous;
            public string RestoreError { get; private set; }
            public ShutdownPrivilege()
            {
                if (!OpenProcessToken(GetCurrentProcess(), 0x20 | 0x8, out token)) throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed");
                try
                {
                    Luid luid;
                    if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out luid)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Shutdown privilege lookup failed");
                    TokenPrivileges requested = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                    uint returned;
                    bool ok = AdjustTokenPrivileges(token, false, ref requested, (uint)Marshal.SizeOf(typeof(TokenPrivileges)), out previous, out returned);
                    int error = Marshal.GetLastWin32Error();
                    SuspendAction.ValidatePrivilegeAdjustment(ok, error);
                }
                catch (Exception activation)
                {
                    int cleanupError = CloseHandle(token) ? 0 : Marshal.GetLastWin32Error();
                    token = IntPtr.Zero;
                    if (cleanupError != 0) throw new InvalidOperationException(activation.Message + "; token cleanup error " + cleanupError, activation);
                    throw;
                }
            }
            public void Dispose()
            {
                if (token == IntPtr.Zero) return;
                try
                {
                    if (previous.Count != 0)
                    {
                        bool ok = RestoreTokenPrivileges(token, false, ref previous, 0, IntPtr.Zero, IntPtr.Zero);
                        int error = Marshal.GetLastWin32Error();
                        if (!ok || error != 0) RestoreError = "Win32 error " + error;
                    }
                }
                finally
                {
                    if (!CloseHandle(token)) RestoreError = (RestoreError ?? "") + " token close error " + Marshal.GetLastWin32Error();
                    token = IntPtr.Zero;
                }
            }
        }
    }
    public sealed class WindowsPowerRequestNative : IPowerRequestNative
    {
        public WindowsPowerRequestNative() { NativeActionSafety.EnsureAllowed(); }
        [StructLayout(LayoutKind.Sequential)] private struct DetailedReason { public IntPtr Module; public uint Id; public uint Count; public IntPtr Strings; }
        [StructLayout(LayoutKind.Explicit)] private struct ReasonUnion
        { [FieldOffset(0)] public IntPtr Simple; [FieldOffset(0)] public DetailedReason Detailed; }
        [StructLayout(LayoutKind.Sequential)] private struct ReasonContext { public uint Version; public uint Flags; public ReasonUnion Reason; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr PowerCreateRequest(ref ReasonContext context);
        [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerSetRequest(IntPtr request, int type);
        [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerClearRequest(IntPtr request, int type);
        [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
        public IntPtr Create()
        {
            NativeActionSafety.EnsureAllowed();
            IntPtr reason = Marshal.StringToHGlobalUni("PowerLockGuard: selected developer work is active");
            try
            {
                ReasonContext context = new ReasonContext { Version = 0, Flags = 1, Reason = new ReasonUnion { Simple = reason } };
                IntPtr handle = PowerCreateRequest(ref context);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "PowerCreateRequest failed");
                return handle;
            }
            finally { Marshal.FreeHGlobal(reason); }
        }
        public void SetSystemRequired(IntPtr handle)
        { NativeActionSafety.EnsureAllowed(); if (!PowerSetRequest(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "PowerSetRequest failed"); }
        public void ClearSystemRequired(IntPtr handle)
        { NativeActionSafety.EnsureAllowed(); if (!PowerClearRequest(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "PowerClearRequest failed"); }
        public void Close(IntPtr handle)
        { NativeActionSafety.EnsureAllowed(); if (!CloseHandle(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Power request CloseHandle failed"); }
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
