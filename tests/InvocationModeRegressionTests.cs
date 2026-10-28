using System;
using System.Threading;
using PowerLockGuard;

internal static class InvocationModeRegressionTests
{
    private static string MutexFor(string[] args)
    { return Program.GetInstanceMutexName(Program.IsSimulationInvocation(args)); }

    public static void Run(Action<string, Action> test, Action<bool, string> check)
    {
        test("Invocation normal live GUI uses the global live mutex", delegate {
            check(!Program.IsSimulationInvocation(null) && !Program.IsSimulationInvocation(new string[0]), "normal invocation live");
            check(MutexFor(new string[0]) == "Global\\PowerLockGuard_SingleInstanceMutex", "existing global name retained");
        });
        test("Invocation top-level dry-run uses the isolated simulation mutex", delegate {
            check(Program.IsSimulationInvocation(new[] { "--dry-run" }), "top-level dry-run only");
            check(MutexFor(new[] { "--dry-run" }) == "Local\\PowerLockGuard_SimulationMutex", "existing simulation name retained");
        });
        test("Invocation watch-command never consumes child dry-run as an app option", delegate {
            var args = new[] { "--watch-command", "child.exe", "--dry-run", "--simulate", "--capture-screenshots" };
            check(!Program.IsSimulationInvocation(args) && MutexFor(args) == MutexFor(new string[0]), "all child tokens remain live");
            check(args[2] == "--dry-run" && args.Length == 5, "mode selection leaves forwarding arguments untouched");
        });
        test("Invocation watch-codex never consumes child dry-run as an app option", delegate {
            var args = new[] { "--watch-codex", "codex.exe", "--dry-run" };
            check(!Program.IsSimulationInvocation(args) && MutexFor(args) == MutexFor(new string[0]), "owned Codex uses live mutex");
        });
        test("Invocation second live mode cannot create a different isolated mutex", delegate {
            string prefix = "Local\\PowerLockGuard_Verification_" + Guid.NewGuid().ToString("N") + "_";
            // Unique test namespace: never open/hold the actual product's live mutex.
            string name = prefix + MutexFor(new string[0]).Replace('\\', '_');
            bool firstCreated;
            using (var first = new Mutex(true, name, out firstCreated)) {
                try {
                    check(firstCreated, "isolated initial instance created");
                    foreach (string mode in new[] { "--watch-command", "--watch-codex" }) {
                        string secondName = prefix + MutexFor(new[] { mode, "child.exe", "--dry-run" }).Replace('\\', '_');
                        bool secondCreated = true; Exception error = null;
                        var thread = new Thread(delegate() {
                            try { using (var second = new Mutex(true, secondName, out secondCreated)) { if (secondCreated) second.ReleaseMutex(); } }
                            catch (Exception ex) { error = ex; }
                        });
                        thread.IsBackground = true; thread.Start();
                        check(thread.Join(5000), "isolated second invocation completed promptly");
                        check(error == null && !secondCreated, "second live path rejected by existing mutex for " + mode);
                    }
                }
                finally { first.ReleaseMutex(); }
            }
        });
        test("Invocation headless and screenshot options are not child-selected GUI dry-run", delegate {
            check(!Program.IsSimulationInvocation(new[] { "--capture-screenshots" }), "screenshot path still handled separately before mutex");
            check(!Program.IsSimulationInvocation(new[] { "--simulate", "all" }), "headless path still returns before GUI");
            check(!Program.IsSimulationInvocation(new[] { "--watch-command", "child.exe" }), "normal command remains live");
        });
        test("Invocation invalid top-level options remain rejected without GUI or launch", delegate {
            int previous = Environment.ExitCode;
            try {
                foreach (string[] args in new[] { new[] { "--typo", "--dry-run" }, new[] { "--dry-run", "extra" }, new[] { "--watch-command" }, new[] { "--watch-codex" } }) {
                    Environment.ExitCode = 0; Program.Main(args);
                    check(Environment.ExitCode == 2, "invalid invocation rejected before live startup");
                }
            }
            finally { Environment.ExitCode = previous; }
        });
    }
}
