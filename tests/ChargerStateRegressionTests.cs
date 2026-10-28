using System;
using System.Reflection;
using PowerLockGuard;

internal static class ChargerStateRegressionTests
{
    public static void Run(Action<string, Action> test, Action<bool, string> check)
    {
        test("Charger valid Online to Offline preserves configurable debounce", delegate {
            var c = new FakeClock(); var g = new ChargerGuard(c);
            check(!g.Observe(ChargerState.Online, true, 1200), "online arms only");
            check(!g.Observe(ChargerState.Offline, true, 1200), "first offline starts debounce");
            c.Advance(1); check(!g.Observe(ChargerState.Offline, true, 1200), "custom delay honored");
            c.Advance(.25); check(g.Observe(ChargerState.Offline, true, 1200), "confirmed disconnect");
        });
        test("Charger Offline to Online never requests an action", delegate {
            var c = new FakeClock(); var g = new ChargerGuard(c);
            check(!g.Observe(ChargerState.Offline, true), "battery startup is not unplug");
            c.Advance(30); check(!g.Observe(ChargerState.Offline, true), "battery remains safe");
            check(!g.Observe(ChargerState.Online, true), "reconnect arms without action");
        });
        foreach (ChargerState state in new[] { ChargerState.Unknown, (ChargerState)(-1), (ChargerState)99 })
        {
            ChargerState invalid = state;
            test("Charger rejects telemetry " + ((int)invalid), delegate {
                var c = new FakeClock(); var g = new ChargerGuard(c);
                g.Observe(ChargerState.Online, true);
                check(!g.Observe(invalid, true), "invalid cannot start disconnect");
                c.Advance(60); check(!g.Observe(invalid, true), "invalid cannot finish disconnect");
                check(!g.Observe(ChargerState.Offline, true), "fresh online required");
                c.Advance(60); check(!g.Observe(ChargerState.Offline, true), "offline cannot re-arm");
            });
        }
        test("Charger invalid observation cancels a pending transition", delegate {
            foreach (ChargerState invalid in new[] { ChargerState.Unknown, (ChargerState)(-1), (ChargerState)99 }) {
                var c = new FakeClock(); var g = new ChargerGuard(c);
                g.Observe(ChargerState.Online, true); g.Observe(ChargerState.Offline, true);
                c.Advance(.25); check(!g.Observe(invalid, true), "pending invalid is safe");
                c.Advance(60); check(!g.Observe(ChargerState.Offline, true), "old debounce discarded");
                c.Advance(60); check(!g.Observe(ChargerState.Offline, true), "still disarmed");
            }
        });
        test("Charger recovers only with fresh Online and full debounce", delegate {
            var c = new FakeClock(); var g = new ChargerGuard(c);
            g.Observe(ChargerState.Online, true); g.Observe(ChargerState.Offline, true);
            c.Advance(1); g.Observe((ChargerState)99, true);
            check(!g.Observe(ChargerState.Online, true), "fresh online arms");
            check(!g.Observe(ChargerState.Offline, true), "fresh debounce starts");
            c.Advance(.25); check(!g.Observe(ChargerState.Offline, true), "old elapsed time not reused");
            c.Advance(.25); check(g.Observe(ChargerState.Offline, true), "full new debounce permits action");
        });
        test("Charger repeated events dispatch once until reconnect", delegate {
            var c = new FakeClock(); var g = new ChargerGuard(c);
            for (int cycle = 0; cycle < 2; cycle++) {
                check(!g.Observe(ChargerState.Online, true), "reconnect only arms");
                g.Observe(ChargerState.Offline, true); c.Advance(.5);
                check(g.Observe(ChargerState.Offline, true), "one decision per disconnect");
                for (int i = 0; i < 5; i++) { c.Advance(1); check(!g.Observe(ChargerState.Offline, true), "no duplicate"); }
                check(!g.Observe((ChargerState)99, true), "invalid cannot re-arm after action");
            }
        });
        test("Charger debounce boundaries include minimum default and maximum", delegate {
            foreach (int delay in new[] { 100, 500, 10000 }) {
                var c = new FakeClock(); var g = new ChargerGuard(c);
                g.Observe(ChargerState.Online, true, delay); g.Observe(ChargerState.Offline, true, delay);
                c.Advance(delay / 1000.0 - .000001);
                check(!g.Observe(ChargerState.Offline, true, delay), "before boundary");
                c.Advance(.000002); check(g.Observe(ChargerState.Offline, true, delay), "after boundary");
                check(!g.Observe(ChargerState.Offline, true, delay), "one shot at boundary");
                var exactClock = new FakeClock(); var exactGuard = new ChargerGuard(exactClock);
                exactGuard.Observe(ChargerState.Online, true, delay); exactGuard.Observe(ChargerState.Offline, true, delay);
                exactClock.Advance(delay / 1000.0); check(exactGuard.Observe(ChargerState.Offline, true, delay), "exact boundary");
            }
        });
        test("UI invalid charger telemetry never reaches recording action backend", delegate {
            foreach (ChargerState invalid in new[] { ChargerState.Unknown, (ChargerState)(-1), (ChargerState)99 }) {
                var c = new FakeClock(); var power = new ScriptedPower(); var r = new RecordingActionExecutor();
                using (var form = new MainForm(true, c, new ScriptedProcesses(), power, r)) {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var settings = (AppSettings)typeof(MainForm).GetField("settings", flags).GetValue(form);
                    settings.WatchdogEnabled = false; settings.ChargerGuardEnabled = true;
                    settings.ChargerDebounceMilliseconds = 500; settings.ChargerGuardAction = "Sleep";
                    var tick = typeof(MainForm).GetMethod("PowerMonitorTimer_Tick", flags);
                    power.State = ChargerState.Online; tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    power.State = invalid; tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    c.Advance(60); tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    power.State = ChargerState.Offline; tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    c.Advance(60); tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    check(r.Requests.Count == 0, "invalid telemetry and unrearmed offline cannot dispatch");
                    power.State = ChargerState.Online; tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    power.State = ChargerState.Offline; tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    c.Advance(.5); tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    tick.Invoke(form, new object[] { null, EventArgs.Empty });
                    check(r.Requests.Count == 1 && r.Requests[0].Origin == ActionOrigin.Charger && r.Requests[0].Action == "Sleep", "independent one-shot recovery uses recording executor only");
                }
            }
        });
    }
}
