using System;
using System.Collections.Generic;
using System.Reflection;
using PowerLockGuard;

internal static class CodexBoundsRegressionTests
{
    private static void Item(CodexTurnTracker tracker, string type, string id)
    { tracker.Observe("{\"type\":\"item." + type + "\",\"item\":{\"id\":\"" + id + "\"}}"); }
    private static string Id(int index, int length)
    { return index.ToString("D4") + new string('x', length - 4); }
    private static CodexTurnTracker Started()
    { var tracker = new CodexTurnTracker(); tracker.Observe("{\"type\":\"turn.started\"}"); return tracker; }
    private static void Complete(CodexTurnTracker tracker)
    { tracker.Observe("{\"type\":\"turn.completed\"}"); }
    private static int Count(CodexTurnTracker tracker)
    { return ((HashSet<string>)typeof(CodexTurnTracker).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker)).Count; }
    private static int Characters(CodexTurnTracker tracker)
    { return (int)typeof(CodexTurnTracker).GetField("pendingIdCharacters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker); }
    private static void Invalid(CodexTurnTracker tracker, Action<bool, string> check)
    {
        check(Count(tracker) == 0 && Characters(tracker) == 0, "invalidation releases retained IDs and budget");
        check(tracker.State(false, 0, false) == TaskState.Active, "retained running process still proves activity");
        Complete(tracker);
        check(tracker.State(true, 0, true) == TaskState.Unknown, "invalid protocol cannot recover successful completion");
    }
    private sealed class TrackerTasks : ITaskStatusSource
    {
        public CodexTurnTracker Tracker;
        public bool Exited;
        public TaskSnapshot Read()
        {
            TaskState state = Tracker.State(Exited, 0, true);
            return new TaskSnapshot { Registered = 1, Active = state == TaskState.Active ? 1 : 0,
                Unknown = state == TaskState.Unknown ? 1 : 0, Faulted = state == TaskState.Failed ? 1 : 0,
                Generation = Tracker.Revision + (Exited ? 1 : 0) };
        }
        public void OnResume() { }
    }

    public static void Run(Action<string, Action> test, Action<bool, string> check)
    {
        test("Codex bounds preserve normal lifecycle and process/drain semantics", delegate {
            var tracker = Started(); Item(tracker, "started", "item_1"); Item(tracker, "completed", "item_1"); Complete(tracker);
            check(tracker.State(false, 0, true) == TaskState.Active, "turn completion cannot override live handle");
            check(tracker.State(true, 0, false) == TaskState.Unknown, "exit requires drained output");
            check(tracker.State(true, 1, true) == TaskState.Failed, "nonzero exit cannot succeed");
            check(tracker.State(true, 0, true) == TaskState.Completed, "valid bounded success retained");
        });
        test("Codex bounds allow many sequential started/completed pairs", delegate {
            var tracker = Started();
            for (int i = 0; i < CodexTurnTracker.MaxPendingItems * 2; i++) {
                string id = Id(i, 32); Item(tracker, "started", id); Item(tracker, "completed", id);
            }
            Complete(tracker);
            check(Count(tracker) == 0 && Characters(tracker) == 0 && tracker.State(true, 0, true) == TaskState.Completed, "budget bounds simultaneous items, not total session events");
        });
        test("Codex pending count exactly at limit can complete normally", delegate {
            var tracker = Started();
            for (int i = 0; i < CodexTurnTracker.MaxPendingItems; i++) Item(tracker, "started", Id(i, 16));
            check(Count(tracker) == CodexTurnTracker.MaxPendingItems, "exact count accepted");
            for (int i = 0; i < CodexTurnTracker.MaxPendingItems; i++) Item(tracker, "completed", Id(i, 16));
            Complete(tracker); check(tracker.State(true, 0, true) == TaskState.Completed, "no false invalidation at count boundary");
        });
        test("Codex pending count exceeding limit invalidates before retention", delegate {
            var tracker = Started();
            for (int i = 0; i <= CodexTurnTracker.MaxPendingItems; i++) Item(tracker, "started", Id(i, 16));
            Invalid(tracker, check);
        });
        test("Codex item ID length exactly at limit is accepted", delegate {
            var tracker = Started(); string id = Id(0, CodexTurnTracker.MaxItemIdLength);
            Item(tracker, "started", id); check(Characters(tracker) == id.Length, "full length retained within budget");
            Item(tracker, "completed", id); Complete(tracker);
            check(tracker.State(true, 0, true) == TaskState.Completed, "boundary ID matches and completes");
        });
        test("Codex oversized started and completed IDs invalidate safely", delegate {
            foreach (string type in new[] { "started", "completed" }) {
                var tracker = Started(); Item(tracker, "started", "previous");
                Item(tracker, type, Id(0, CodexTurnTracker.MaxItemIdLength + 1)); Invalid(tracker, check);
            }
        });
        test("Codex cumulative ID storage exactly at limit can be reclaimed", delegate {
            var tracker = Started(); int items = CodexTurnTracker.MaxPendingIdCharacters / CodexTurnTracker.MaxItemIdLength;
            for (int i = 0; i < items; i++) Item(tracker, "started", Id(i, CodexTurnTracker.MaxItemIdLength));
            check(Characters(tracker) == CodexTurnTracker.MaxPendingIdCharacters && Count(tracker) == items, "exact cumulative boundary accepted independently of count");
            Item(tracker, "completed", Id(0, CodexTurnTracker.MaxItemIdLength));
            Item(tracker, "started", Id(items, CodexTurnTracker.MaxItemIdLength));
            check(Characters(tracker) == CodexTurnTracker.MaxPendingIdCharacters, "completion frees full ID budget for replacement");
            for (int i = 1; i <= items; i++) Item(tracker, "completed", Id(i, CodexTurnTracker.MaxItemIdLength));
            Complete(tracker); check(tracker.State(true, 0, true) == TaskState.Completed, "cumulative boundary session completes");
        });
        test("Codex cumulative storage exceeding limit by one invalidates", delegate {
            var tracker = Started(); int items = CodexTurnTracker.MaxPendingIdCharacters / CodexTurnTracker.MaxItemIdLength;
            for (int i = 0; i < items; i++) Item(tracker, "started", Id(i, CodexTurnTracker.MaxItemIdLength));
            Item(tracker, "started", "z"); Invalid(tracker, check);
        });
        test("Codex repeated pending IDs do not consume count or storage budget", delegate {
            var tracker = Started(); int length = CodexTurnTracker.MaxPendingIdCharacters / CodexTurnTracker.MaxPendingItems;
            for (int i = 0; i < CodexTurnTracker.MaxPendingItems; i++) Item(tracker, "started", Id(i, length));
            for (int i = 0; i < 20; i++) Item(tracker, "started", Id(0, length));
            check(Count(tracker) == CodexTurnTracker.MaxPendingItems && Characters(tracker) == CodexTurnTracker.MaxPendingIdCharacters, "duplicates accepted even at both exact limits");
            for (int i = 0; i < CodexTurnTracker.MaxPendingItems; i++) Item(tracker, "completed", Id(i, length));
            Complete(tracker); check(tracker.State(true, 0, true) == TaskState.Completed, "duplicates cannot fabricate extra pending work");
        });
        test("Codex unmatched or repeated completions never underflow storage budget", delegate {
            var tracker = Started(); Item(tracker, "started", "item_1");
            Item(tracker, "completed", "unmatched"); check(Characters(tracker) == 6, "unmatched ID does not free budget");
            Item(tracker, "completed", "item_1"); Item(tracker, "completed", "item_1");
            check(Characters(tracker) == 0 && Count(tracker) == 0, "matched ID removed only once");
            Complete(tracker); check(tracker.State(true, 0, true) == TaskState.Completed, "existing completion semantics preserved");
        });
        test("Codex invalid JSON and explicit error clear retained state permanently", delegate {
            foreach (string bad in new[] { "garbage", "{}", "{\"type\":\"error\"}", "{\"type\":null}" }) {
                var tracker = Started(); Item(tracker, "started", "item_1"); tracker.Observe(bad); Invalid(tracker, check);
                long revision = tracker.Revision;
                for (int i = 0; i < 20; i++) Item(tracker, "started", Id(i, 16));
                check(Count(tracker) == 0 && Characters(tracker) == 0 && tracker.Revision == revision, "invalid parser ignores later input without secondary retained state");
            }
        });
        test("Codex unknown event cannot invent completion and preserves forward compatibility", delegate {
            var tracker = new CodexTurnTracker(); tracker.Observe("{\"type\":\"future.event\"}");
            check(tracker.State(true, 0, true) == TaskState.Unknown, "unknown event alone has no completion authority");
            tracker.Observe("{\"type\":\"turn.completed\"}"); Invalid(tracker, check);
            tracker = Started(); tracker.Observe("{\"type\":\"future.event\"}"); Complete(tracker);
            check(tracker.State(true, 0, true) == TaskState.Completed, "unrelated future event does not break existing valid lifecycle");
        });
        test("Codex limit-invalid running session remains protected and exit cannot arm sleep", delegate {
            var tracker = Started(); Item(tracker, "started", Id(0, CodexTurnTracker.MaxItemIdLength + 1));
            var clock = new FakeClock(); var cpu = new WorkWatchdogEngine(clock, new ScriptedProcesses(), 4);
            var tasks = new TrackerTasks { Tracker = tracker }; var policy = new WorkloadPolicy(clock, tasks);
            var settings = new AppSettings { WatchdogEnabled = true, WatchdogEvidenceMode = "TaskAware", WatchdogGraceMinutes = 1 };
            cpu.Poll("AgentsAndBuilds", "", 1000, 3.5); clock.Advance(1); cpu.Poll("AgentsAndBuilds", "", 1000, 3.5);
            policy.Refresh(settings, cpu);
            check(policy.Protect && !policy.CoreEligible && policy.Observation == WorkObservation.ConfirmedActive, "running handle protects despite invalid protocol");
            tasks.Exited = true;
            foreach (string unknownPolicy in new[] { "HoldKnownWork", "ReleaseProtection" }) {
                settings.UnknownWorkPolicy = unknownPolicy; clock.Advance(120); policy.Refresh(settings, cpu);
                check(!policy.CoreEligible && policy.Observation == WorkObservation.Unknown, "invalid exited session never authorizes sleep for " + unknownPolicy);
            }
        });
        test("Codex failed turn and incomplete output never become successful completion", delegate {
            var tracker = Started(); tracker.Observe("{\"type\":\"turn.failed\"}");
            check(tracker.State(true, 0, true) == TaskState.Failed, "failed turn blocks successful exit");
            tracker.Observe("{\"type\":\"turn.started\"}"); Complete(tracker);
            check(tracker.State(true, 0, true) == TaskState.Unknown, "invalid overlapping restart cannot fabricate success");
            tracker = Started(); Item(tracker, "started", "pending"); Complete(tracker);
            check(tracker.State(true, 0, true) == TaskState.Unknown, "unfinished item blocks completed turn");
            Item(tracker, "completed", "pending");
            check(tracker.State(true, 0, false) == TaskState.Unknown && tracker.State(true, 0, true) == TaskState.Completed, "successful drained exit remains mandatory");
        });
    }
}
