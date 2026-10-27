using System;

namespace PowerLockGuard
{
    public sealed class NativeCallResult
    {
        public readonly bool Success;
        public readonly int Error;
        public NativeCallResult(bool success, int error) { Success = success; Error = error; }
    }
    public interface IShutdownPrivilege : IDisposable { string RestoreError { get; } }
    public interface ISuspendNative
    {
        bool Supports(bool hibernate);
        IShutdownPrivilege EnableShutdownPrivilege();
        NativeCallResult Suspend(bool hibernate);
    }
    public static class SuspendAction
    {
        public static void ValidatePrivilegeAdjustment(bool success, int error)
        {
            if (!success || error != 0) throw new System.ComponentModel.Win32Exception(error,
                error == 1300 ? "SE_SHUTDOWN_NAME is not assigned (ERROR_NOT_ALL_ASSIGNED)" : "Shutdown privilege activation failed");
        }
        public static ActionResult Execute(bool hibernate, ISuspendNative native)
        {
            string action = hibernate ? "Hibernate" : "Sleep";
            IShutdownPrivilege privilege = null;
            ActionResult result;
            try
            {
                if (!native.Supports(hibernate)) return new ActionResult(ActionStatus.Failed, action + " is unavailable under current Windows power capabilities/policy.");
                privilege = native.EnableShutdownPrivilege();
                if (privilege == null) throw new InvalidOperationException("Shutdown privilege activation was not confirmed.");
                NativeCallResult call = native.Suspend(hibernate);
                result = call.Success ? new ActionResult(ActionStatus.Requested, action + " API returned success; physical state not independently verified") :
                    new ActionResult(ActionStatus.Failed, action + " native error " + call.Error);
            }
            catch (Exception ex) { result = new ActionResult(ActionStatus.Failed, action + ": " + ex.Message); }
            if (privilege != null)
            {
                string cleanup = null;
                try { privilege.Dispose(); cleanup = privilege.RestoreError; }
                catch (Exception ex) { cleanup = ex.Message; }
                if (!string.IsNullOrEmpty(cleanup))
                    result = new ActionResult(ActionStatus.Failed, result + "; privilege restoration failed: " + cleanup);
            }
            return result;
        }
    }
    public interface IKeepAwakeLease : IDisposable
    {
        bool IsHeld { get; }
        void Acquire();
        void Release();
    }
    public sealed class RecordingKeepAwakeLease : IKeepAwakeLease
    {
        public bool IsHeld { get; private set; }
        public int Acquisitions { get; private set; }
        public int Releases { get; private set; }
        public void Acquire() { if (!IsHeld) { IsHeld = true; Acquisitions++; } }
        public void Release() { if (IsHeld) { IsHeld = false; Releases++; } }
        public void Dispose() { Release(); }
    }
    public interface IPowerRequestNative
    {
        IntPtr Create();
        void SetSystemRequired(IntPtr handle);
        void ClearSystemRequired(IntPtr handle);
        void Close(IntPtr handle);
    }
    // One owned handle, one increment; never sets DisplayRequired or AwayModeRequired.
    public sealed class SystemAwakeLease : IKeepAwakeLease
    {
        private readonly IPowerRequestNative native;
        private IntPtr handle;
        private bool disposed, active;
        public bool IsHeld { get { return active; } }
        public SystemAwakeLease(IPowerRequestNative native) { this.native = native; }
        public void Acquire()
        {
            if (disposed) throw new ObjectDisposedException("SystemAwakeLease");
            if (IsHeld) return;
            if (handle != IntPtr.Zero) { native.Close(handle); handle = IntPtr.Zero; }
            IntPtr created = native.Create();
            if (created == IntPtr.Zero || created == new IntPtr(-1)) throw new InvalidOperationException("Power request handle unavailable");
            // Keep ownership even if Set fails, so Close failures remain retryable at cleanup.
            handle = created;
            try { native.SetSystemRequired(handle); active = true; }
            catch (Exception setError)
            {
                try { native.Close(handle); handle = IntPtr.Zero; }
                catch (Exception cleanup) { throw new InvalidOperationException(setError.Message + "; power request cleanup failed: " + cleanup.Message, setError); }
                throw;
            }
        }
        public void Release()
        {
            if (handle == IntPtr.Zero) return;
            Exception error = null;
            try { if (active) { native.ClearSystemRequired(handle); active = false; } } catch (Exception ex) { error = ex; }
            try { native.Close(handle); handle = IntPtr.Zero; active = false; }
            catch (Exception ex) { throw new InvalidOperationException("Power request handle cleanup failed: " + ex.Message, ex); }
            if (error != null) throw new InvalidOperationException("Power request clear failed; handle closed: " + error.Message, error);
        }
        public void Dispose() { try { Release(); } finally { disposed = true; } }
    }
    // UI callbacks are serialized; the gate also prevents injected/background callers from
    // reacquiring a lease or overlapping dispatch while a native request is in flight.
    public sealed class PowerActionArbiter : IDisposable
    {
        private readonly object gate = new object();
        private readonly ActionDispatcher actions;
        private readonly IKeepAwakeLease lease;
        private readonly Action<string> log;
        private bool inFlight, suppressed, disposed;
        public bool ProtectionHeld { get { lock (gate) { return lease.IsHeld; } } }
        public PowerActionArbiter(ActionDispatcher actions, IKeepAwakeLease lease, Action<string> log)
        { this.actions = actions; this.lease = lease; this.log = log ?? delegate { }; }
        public void UpdateProtection(bool required)
        {
            lock (gate)
            {
                if (disposed) return;
                try
                {
                    bool wasHeld = lease.IsHeld;
                    if (required && !suppressed && !inFlight) lease.Acquire(); else lease.Release();
                    if (wasHeld != lease.IsHeld) log(lease.IsHeld ? "System-awake lease acquired (system only)." : "System-awake lease released.");
                }
                catch (Exception ex) { log("Keep-awake FAILED: " + ex.Message); }
            }
        }
        public void Rearm() { lock (gate) { if (!inFlight) suppressed = false; } }
        public void Resume() { lock (gate) { suppressed = false; } }
        public ActionResult Request(string action, ActionOrigin origin, ProcessIdentity target, Func<bool> watchdogCheck = null)
        {
            bool transition = action == "Sleep" || action == "Hibernate" || action == "Shutdown";
            lock (gate)
            {
                if (disposed || inFlight) return new ActionResult(ActionStatus.Rejected, "A power/action request is already in flight or the application is closing.");
                if (origin == ActionOrigin.Watchdog && (watchdogCheck == null || !watchdogCheck()))
                    return new ActionResult(ActionStatus.Rejected, "Fresh watchdog eligibility required.");
                inFlight = true;
                if (transition)
                {
                    suppressed = true;
                    try { lease.Release(); log("System-awake lease released before " + origin + " " + action + "."); }
                    catch (Exception ex) { log("Keep-awake cleanup FAILED before explicit transition: " + ex.Message); }
                }
            }
            ActionResult result;
            try { result = actions.Request(action, origin, target); }
            finally { lock (gate) { inFlight = false; } }
            // A definite failed/rejected request can protect still-active work on the next poll.
            if (result.Status == ActionStatus.Failed || result.Status == ActionStatus.Rejected) Rearm();
            return result;
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                try { lease.Dispose(); } catch (Exception ex) { log("Keep-awake disposal FAILED: " + ex.Message); }
            }
        }
    }
}
