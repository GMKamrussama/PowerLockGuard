using System;
using System.IO;
using System.Text;

namespace PowerLockGuard
{
    public sealed class DemoPowerSource : IPowerStatusSource
    { public ChargerState Read() { return ChargerState.Unknown; } }

    public sealed class DemoProcessSource : IProcessSource
    {
        private readonly IClock clock;
        private double epoch;
        public DemoProcessSource(IClock clock) { this.clock = clock; Restart(); }
        public void Restart() { epoch = clock.Seconds; }
        public ProcessSample Read(string mode, string target)
        {
            ProcessSample sample = new ProcessSample();
            if (!WorkWatchdogEngine.Matches("codex", mode, target)) return sample;
            sample.Processes.Add(new ProcessObservation { Identity = new ProcessIdentity(4242, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Name = "codex", CpuTime = TimeSpan.FromSeconds(Math.Min(8, Math.Max(0, clock.Seconds - epoch)) * 2), MemoryMb = 80 });
            return sample;
        }
    }

    public static class SimulationRunner
    {
        public static int Run(string scenario)
        { return Run(scenario, Path.Combine(Environment.CurrentDirectory, "artifacts", "simulation.log")); }

        public static int Run(string scenario, string output)
        {
            if (scenario != "all" && scenario != "watchdog" && scenario != "charger") return 2;
            StringBuilder log = new StringBuilder("PowerLockGuard v1.1 SIMULATION\nSynthetic inputs; no production action adapter is constructed.\n");
            RecordingActionExecutor executor = new RecordingActionExecutor();
            ActionDispatcher dispatcher = new ActionDispatcher(executor);
            FakeClock clock = new FakeClock();
            if (scenario == "all" || scenario == "watchdog")
            {
                WorkWatchdogEngine engine = new WorkWatchdogEngine(clock, new DemoProcessSource(clock), 4);
                for (int second = 0; second <= 75; second++)
                {
                    clock.Seconds = second;
                    engine.Poll("AgentsAndBuilds", "", 1000, 3.5);
                    if (second == 5) log.AppendLine("00:05 High CPU is useful activity; automatic termination remains disabled.");
                    if (second == 9) log.AppendLine("00:09 CPU inactive; task completion remains unverified.");
                    if (engine.CanAct(60))
                    {
                        log.AppendLine(second + "s " + dispatcher.Request("Sleep", ActionOrigin.Watchdog, null));
                        engine.RestartMonitoring();
                    }
                }
            }
            if (scenario == "all" || scenario == "charger")
            {
                ChargerGuard guard = new ChargerGuard(clock);
                guard.Observe(ChargerState.Online, true);
                if (guard.Observe(ChargerState.Unknown, true)) return 1;
                clock.Advance(1);
                if (guard.Observe(ChargerState.Offline, true)) return 1;
                log.AppendLine("Unknown charger reading suppressed; no confirmed transition.");
                guard.Observe(ChargerState.Online, true);
                guard.Observe(ChargerState.Offline, true);
                clock.Advance(0.5);
                if (!guard.Observe(ChargerState.Offline, true)) return 1;
                log.AppendLine("Confirmed unplug " + dispatcher.Request("Sleep", ActionOrigin.Charger, null));
            }
            ActionResult blocked = dispatcher.Request("Terminate", ActionOrigin.Watchdog, new ProcessIdentity(4242, DateTime.UtcNow));
            log.AppendLine("Automatic termination " + blocked.Status);
            log.AppendLine("Recorded power requests: " + executor.Requests.Count);
            log.AppendLine("Native action adapter constructed: false");
            log.AppendLine("Native actions invoked: 0");
            try
            {
                string full = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, log.ToString(), Encoding.UTF8);
                Console.Write(log.ToString());
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
    }
}
