using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PowerLockGuard
{
    public sealed class AppSettings
    {
        public bool ChargerGuardEnabled = true;
        public string ChargerGuardAction = "Sleep";
        public int ChargerDebounceMilliseconds = 500;
        public bool WatchdogEnabled;
        public string WatchdogAction = "Sleep";
        public string WatchdogMode = "AgentsAndBuilds";
        public string WatchdogTargetProcess = "claude";
        public int WatchdogGraceMinutes = 3;
        public double WatchdogIdleThreshold = 3.5;
        public string WatchdogEvidenceMode = "Heuristic";
        public string UnknownWorkPolicy = "HoldKnownWork";
        public bool KeepAwakeEnabled = true;
        public bool AutoKillStuckAgentsOnSleep; // Legacy key, deliberately never enabled in v1.1.
        public bool StartWithWindows;
        public bool PlayAlertSound = true;
        public int CountdownSeconds = 30;
        public readonly List<string> Warnings = new List<string>();
        public string LastError { get; private set; }
        public bool Ephemeral { get; set; }
        private readonly string path;
        public AppSettings() : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")) { }
        public AppSettings(string path) { this.path = path; }

        private void Invalid(string key, bool charger)
        {
            string message = "Invalid " + key + "; " + (charger ? "Charger Guard" : "Work Watchdog") + " disabled until explicitly enabled.";
            if (!Warnings.Contains(message)) Warnings.Add(message);
            if (charger) ChargerGuardEnabled = false; else WatchdogEnabled = false;
        }
        public void Validate()
        {
            if (!ActionDispatcher.IsPowerAction(ChargerGuardAction)) { Invalid("ChargerGuardAction", true); ChargerGuardAction = "Sleep"; }
            if (ChargerDebounceMilliseconds < 100 || ChargerDebounceMilliseconds > 10000) { Invalid("ChargerDebounceMilliseconds", true); ChargerDebounceMilliseconds = 500; }
            if (WatchdogAction != "Sleep" && WatchdogAction != "Shutdown" && WatchdogAction != "Hibernate") { Invalid("WatchdogAction", false); WatchdogAction = "Sleep"; }
            if (WatchdogMode == "AutoDev") WatchdogMode = "AgentsAndBuilds";
            if (WatchdogMode != "AgentsAndBuilds" && WatchdogMode != "AIAgentsOnly" && WatchdogMode != "AllDevTools" && WatchdogMode != "SpecificProcess")
            { Invalid("WatchdogMode", false); WatchdogMode = "AgentsAndBuilds"; }
            if (WatchdogEvidenceMode != "Heuristic" && WatchdogEvidenceMode != "TaskAware") { Invalid("WatchdogEvidenceMode", false); WatchdogEvidenceMode = "TaskAware"; }
            if (UnknownWorkPolicy != "HoldKnownWork" && UnknownWorkPolicy != "ReleaseProtection") { Invalid("UnknownWorkPolicy", false); UnknownWorkPolicy = "HoldKnownWork"; }
            if (Array.IndexOf(new int[] { 1, 2, 3, 5, 10 }, WatchdogGraceMinutes) < 0) { Invalid("WatchdogGraceMinutes", false); WatchdogGraceMinutes = 3; }
            if (double.IsNaN(WatchdogIdleThreshold) || double.IsInfinity(WatchdogIdleThreshold) || Array.IndexOf(new double[] { 1.5, 2.5, 3.5, 5, 8, 10 }, WatchdogIdleThreshold) < 0)
            { Invalid("WatchdogIdleThreshold", false); WatchdogIdleThreshold = 3.5; }
            if (CountdownSeconds != 15 && CountdownSeconds != 30 && CountdownSeconds != 60) { Invalid("CountdownSeconds", false); CountdownSeconds = 30; }
            WatchdogTargetProcess = (WatchdogTargetProcess ?? "").Trim();
            if (WatchdogMode == "SpecificProcess" && WatchdogTargetProcess.Length == 0) Invalid("WatchdogTargetProcess", false);
            if (AutoKillStuckAgentsOnSleep) Warnings.Add("Legacy auto-kill setting disabled: CPU usage does not prove a process is stuck.");
            AutoKillStuckAgentsOnSleep = false;
        }
        public static AppSettings Load() { return Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")); }
        public static AppSettings Load(string path)
        {
            AppSettings s = new AppSettings(path);
            bool invalid = false;
            bool invalidCharger = false, invalidWatchdog = false;
            try
            {
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("[") || line.StartsWith("#") || line.StartsWith(";")) continue;
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (parts.Length != 2) { invalid = true; continue; }
                        string key = parts[0].Trim(), value = parts[1].Trim();
                        bool b; int n; double d;
                        switch (key)
                        {
                            case "IsEnabled": case "ChargerGuardEnabled": if (bool.TryParse(value, out b)) s.ChargerGuardEnabled = b; else invalidCharger = true; break;
                            case "ChargerDebounceMilliseconds": if (int.TryParse(value, out n)) s.ChargerDebounceMilliseconds = n; else invalidCharger = true; break;
                            case "WatchdogEnabled": if (bool.TryParse(value, out b)) s.WatchdogEnabled = b; else invalidWatchdog = true; break;
                            case "KeepAwakeEnabled": if (bool.TryParse(value, out b)) s.KeepAwakeEnabled = b; else invalidWatchdog = true; break;
                            case "WatchdogEvidenceMode": s.WatchdogEvidenceMode = value; break;
                            case "UnknownWorkPolicy": s.UnknownWorkPolicy = value; break;
                            case "AutoKillStuckAgentsOnSleep": if (bool.TryParse(value, out b)) s.AutoKillStuckAgentsOnSleep = b; else s.Warnings.Add("Invalid legacy auto-kill ignored; automatic termination remains disabled."); break;
                            case "StartWithWindows": if (bool.TryParse(value, out b)) s.StartWithWindows = b; else { s.StartWithWindows = false; s.Warnings.Add("Invalid startup setting; startup disabled."); } break;
                            case "PlayAlertSound": if (bool.TryParse(value, out b)) s.PlayAlertSound = b; else { s.PlayAlertSound = false; s.Warnings.Add("Invalid sound setting; sound disabled."); } break;
                            case "WatchdogGraceMinutes": if (int.TryParse(value, out n)) s.WatchdogGraceMinutes = n; else invalidWatchdog = true; break;
                            case "CountdownSeconds": if (int.TryParse(value, out n)) s.CountdownSeconds = n; else invalidWatchdog = true; break;
                            case "WatchdogIdleThreshold": if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.WatchdogIdleThreshold = d; else invalidWatchdog = true; break;
                            case "ChargerGuardAction": s.ChargerGuardAction = value; break;
                            case "WatchdogAction": s.WatchdogAction = value; break;
                            case "WatchdogMode": s.WatchdogMode = value; break;
                            case "WatchdogTargetProcess": s.WatchdogTargetProcess = value; break;
                        }
                    }
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception ex) { invalid = true; s.LastError = ex.Message; }
            s.Validate();
            if (invalidCharger) s.Invalid("charger configuration parse", true);
            if (invalidWatchdog) s.Invalid("watchdog configuration parse", false);
            if (invalid) { s.Invalid("configuration read/parse", true); s.Invalid("configuration read/parse", false); s.StartWithWindows = false; }
            return s;
        }
        public bool Save()
        {
            LastError = null;
            if (Ephemeral) return true;
            Validate();
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (StreamWriter sw = new StreamWriter(temp, false, Encoding.UTF8))
                {
                    sw.WriteLine("[PowerLockGuard]");
                    sw.WriteLine("ChargerGuardEnabled=" + ChargerGuardEnabled);
                    sw.WriteLine("ChargerGuardAction=" + ChargerGuardAction);
                    sw.WriteLine("ChargerDebounceMilliseconds=" + ChargerDebounceMilliseconds);
                    sw.WriteLine("WatchdogEnabled=" + WatchdogEnabled);
                    sw.WriteLine("WatchdogAction=" + WatchdogAction);
                    sw.WriteLine("WatchdogMode=" + WatchdogMode);
                    sw.WriteLine("WatchdogTargetProcess=" + WatchdogTargetProcess.Replace("\r", "").Replace("\n", ""));
                    sw.WriteLine("WatchdogGraceMinutes=" + WatchdogGraceMinutes);
                    sw.WriteLine("WatchdogIdleThreshold=" + WatchdogIdleThreshold.ToString("0.0", CultureInfo.InvariantCulture));
                    sw.WriteLine("WatchdogEvidenceMode=" + WatchdogEvidenceMode);
                    sw.WriteLine("UnknownWorkPolicy=" + UnknownWorkPolicy);
                    sw.WriteLine("KeepAwakeEnabled=" + KeepAwakeEnabled);
                    sw.WriteLine("AutoKillStuckAgentsOnSleep=False");
                    sw.WriteLine("StartWithWindows=" + StartWithWindows);
                    sw.WriteLine("PlayAlertSound=" + PlayAlertSound);
                    sw.WriteLine("CountdownSeconds=" + CountdownSeconds);
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception ex) { LastError = ex.GetType().Name + ": " + ex.Message; return false; }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
