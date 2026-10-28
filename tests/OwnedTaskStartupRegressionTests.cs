using System;
using System.Collections.Generic;
using System.Threading;
using PowerLockGuard;

internal static class OwnedTaskStartupRegressionTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> check)
    {
        test("Owned startup prepares both drains before launching a child", delegate {
            var startup = new OwnedTaskStartup(); var order = new List<string>();
            startup.Run(delegate { order.Add("stdout"); }, delegate { order.Add("stderr"); }, delegate { order.Add("launch"); return true; });
            check(string.Join(",", order.ToArray()) == "stdout,stderr,launch" && startup.AwaitLaunch(), "prepared drains precede single successful launch");
        });
        test("Owned startup first drain failure never launches a child", delegate {
            var startup = new OwnedTaskStartup(); bool launched = false, errorPrepared = false; Exception caught = null;
            var failure = new InvalidOperationException("injected first worker failure");
            try { startup.Run(delegate { throw failure; }, delegate { errorPrepared = true; }, delegate { launched = true; return true; }); }
            catch (Exception ex) { caught = ex; }
            check(object.ReferenceEquals(caught, failure) && !errorPrepared && !launched && !startup.AwaitLaunch(), "original error preserved; no child or live reader");
        });
        test("Owned startup second drain failure releases waiting worker without launching", delegate {
            var startup = new OwnedTaskStartup(); bool launched = false, readerTouched = false; Thread worker = null; Exception caught = null;
            var failure = new InvalidOperationException("injected second worker failure");
            try {
                startup.Run(delegate {
                    worker = new Thread(delegate() { readerTouched = startup.AwaitLaunch(); });
                    worker.IsBackground = true; worker.Start();
                }, delegate { throw failure; }, delegate { launched = true; return true; });
            }
            catch (Exception ex) { caught = ex; }
            check(worker != null && worker.Join(5000), "partial drain worker exits after abort");
            check(object.ReferenceEquals(caught, failure) && !launched && !readerTouched, "no redirected stream access or child on partial preparation failure");
        });
        test("Owned startup false or throwing launch releases prepared workers as aborted", delegate {
            foreach (bool throws in new[] { false, true }) {
                var startup = new OwnedTaskStartup(); bool readOutput = false, readError = false; Thread output = null, error = null; Exception caught = null;
                try {
                    startup.Run(delegate { output = new Thread(delegate() { readOutput = startup.AwaitLaunch(); }); output.IsBackground = true; output.Start(); },
                        delegate { error = new Thread(delegate() { readError = startup.AwaitLaunch(); }); error.IsBackground = true; error.Start(); },
                        delegate { if (throws) throw new InvalidOperationException("injected launch failure"); return false; });
                }
                catch (Exception ex) { caught = ex; }
                check(output != null && error != null && output.Join(5000) && error.Join(5000), "both prepared workers finish on launch failure");
                check(caught is InvalidOperationException && !readOutput && !readError && !startup.AwaitLaunch(), "failure cannot authorize readers");
            }
        });
        test("Owned startup successful launch releases both prepared readers", delegate {
            var startup = new OwnedTaskStartup(); int reads = 0; Thread output = null, error = null;
            startup.Run(delegate { output = new Thread(delegate() { if (startup.AwaitLaunch()) Interlocked.Increment(ref reads); }); output.IsBackground = true; output.Start(); },
                delegate { error = new Thread(delegate() { if (startup.AwaitLaunch()) Interlocked.Increment(ref reads); }); error.IsBackground = true; error.Start(); },
                delegate { check(reads == 0, "readers cannot touch streams before launch succeeds"); return true; });
            check(output.Join(5000) && error.Join(5000) && reads == 2, "successful launch releases both readers");
        });
        test("Owned startup is single-use and cannot launch a second child", delegate {
            var startup = new OwnedTaskStartup(); int launches = 0;
            startup.Run(delegate { }, delegate { }, delegate { launches++; return true; });
            bool rejected = false;
            try { startup.Run(delegate { }, delegate { }, delegate { launches++; return true; }); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected && launches == 1 && startup.AwaitLaunch(), "second call rejected without changing first launch result");
        });
    }
}
