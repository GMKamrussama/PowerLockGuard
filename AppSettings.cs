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
        public bool WatchdogEnabled;
        public string WatchdogAction = "Sleep";
        public string WatchdogMode = "AgentsAndBuilds";
        public string WatchdogTargetProcess = "claude";
        public int WatchdogGraceMinutes = 3;
        public double WatchdogIdleThreshold = 3.5;
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

        private void Invalid(string key)
        {
            Warnings.Add("Invalid " + key + "; automatic guards disabled until explicitly enabled.");
            ChargerGuardEnabled = false; WatchdogEnabled = false;
        }
        public void Validate()
        {
            if (!ActionDispatcher.IsPowerAction(ChargerGuardAction)) { Invalid("ChargerGuardAction"); ChargerGuardAction = "Sleep"; }
            if (WatchdogAction != "Sleep" && WatchdogAction != "Shutdown" && WatchdogAction != "Hibernate") { Invalid("WatchdogAction"); WatchdogAction = "Sleep"; }
            if (WatchdogMode == "AutoDev") WatchdogMode = "AgentsAndBuilds";
            if (WatchdogMode != "AgentsAndBuilds" && WatchdogMode != "AIAgentsOnly" && WatchdogMode != "AllDevTools" && WatchdogMode != "SpecificProcess")
            { Invalid("WatchdogMode"); WatchdogMode = "AgentsAndBuilds"; }
            if (Array.IndexOf(new int[] { 1, 2, 3, 5, 10 }, WatchdogGraceMinutes) < 0) { Invalid("WatchdogGraceMinutes"); WatchdogGraceMinutes = 3; }
            if (double.IsNaN(WatchdogIdleThreshold) || double.IsInfinity(WatchdogIdleThreshold) || Array.IndexOf(new double[] { 1.5, 2.5, 3.5, 5, 8, 10 }, WatchdogIdleThreshold) < 0)
            { Invalid("WatchdogIdleThreshold"); WatchdogIdleThreshold = 3.5; }
            if (CountdownSeconds != 15 && CountdownSeconds != 30 && CountdownSeconds != 60) { Invalid("CountdownSeconds"); CountdownSeconds = 30; }
            WatchdogTargetProcess = (WatchdogTargetProcess ?? "").Trim();
            if (WatchdogMode == "SpecificProcess" && WatchdogTargetProcess.Length == 0) Invalid("WatchdogTargetProcess");
            if (AutoKillStuckAgentsOnSleep) Warnings.Add("Legacy auto-kill setting disabled: CPU usage does not prove a process is stuck.");
            AutoKillStuckAgentsOnSleep = false;
        }
        public static AppSettings Load() { return Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")); }
        public static AppSettings Load(string path)
        {
            AppSettings s = new AppSettings(path);
            bool invalid = false;
            try
            {
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("[") || line.StartsWith("#") || line.StartsWith(";")) continue;
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (parts.Length != 2) { s.Invalid("config line"); invalid = true; continue; }
                        string key = parts[0].Trim(), value = parts[1].Trim();
                        bool b; int n; double d;
                        switch (key)
                        {
                            case "IsEnabled": case "ChargerGuardEnabled": if (bool.TryParse(value, out b)) s.ChargerGuardEnabled = b; else invalid = true; break;
                            case "WatchdogEnabled": if (bool.TryParse(value, out b)) s.WatchdogEnabled = b; else invalid = true; break;
                            case "AutoKillStuckAgentsOnSleep": if (bool.TryParse(value, out b)) s.AutoKillStuckAgentsOnSleep = b; else invalid = true; break;
                            case "StartWithWindows": if (bool.TryParse(value, out b)) s.StartWithWindows = b; else invalid = true; break;
                            case "PlayAlertSound": if (bool.TryParse(value, out b)) s.PlayAlertSound = b; else invalid = true; break;
                            case "WatchdogGraceMinutes": if (int.TryParse(value, out n)) s.WatchdogGraceMinutes = n; else invalid = true; break;
                            case "CountdownSeconds": if (int.TryParse(value, out n)) s.CountdownSeconds = n; else invalid = true; break;
                            case "WatchdogIdleThreshold": if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.WatchdogIdleThreshold = d; else invalid = true; break;
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
            if (invalid) { s.Invalid("configuration read/parse"); s.StartWithWindows = false; }
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
                    sw.WriteLine("WatchdogEnabled=" + WatchdogEnabled);
                    sw.WriteLine("WatchdogAction=" + WatchdogAction);
                    sw.WriteLine("WatchdogMode=" + WatchdogMode);
                    sw.WriteLine("WatchdogTargetProcess=" + WatchdogTargetProcess.Replace("\r", "").Replace("\n", ""));
                    sw.WriteLine("WatchdogGraceMinutes=" + WatchdogGraceMinutes);
                    sw.WriteLine("WatchdogIdleThreshold=" + WatchdogIdleThreshold.ToString("0.0", CultureInfo.InvariantCulture));
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
