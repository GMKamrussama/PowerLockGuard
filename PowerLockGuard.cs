using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace PowerLockGuard
{
    public static class Program
    {
        private static System.Threading.Mutex singleInstanceMutex;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDPIAware();
                }
            }
            catch { }

            bool createdNew;
            singleInstanceMutex = new System.Threading.Mutex(true, "Global\\PowerLockGuard_SingleInstanceMutex", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("PowerLockGuard is already running in the background.\nCheck your system tray near the clock.",
                    "PowerLockGuard Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    #region Configuration & Settings
    public class AppSettings
    {
        // Charger Guard
        public bool ChargerGuardEnabled = true;
        public string ChargerGuardAction = "Sleep"; // "Sleep", "Shutdown", "Hibernate", "Lock"

        // Work Watchdog (Auto-Sleep / Shutdown on Dev & Agent Completion)
        public bool WatchdogEnabled = false;
        public string WatchdogAction = "Sleep"; // "Sleep", "Shutdown", "Hibernate"
        public string WatchdogMode = "AutoDev"; // "AutoDev", "SpecificProcess", "SelectedPID"
        public string WatchdogTargetProcess = "claude"; // Process name for SpecificProcess
        public int WatchdogGraceMinutes = 3; // Consecutive idle minutes before action (1, 2, 3, 5, 10)

        // General
        public bool StartWithWindows = false;
        public bool PlayAlertSound = true;
        public int CountdownSeconds = 30; // 15, 30, 60

        private static string ConfigPath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "config.ini");
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(ConfigPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("[PowerLockGuard]");
                    sw.WriteLine("ChargerGuardEnabled=" + ChargerGuardEnabled);
                    sw.WriteLine("ChargerGuardAction=" + ChargerGuardAction);
                    sw.WriteLine("WatchdogEnabled=" + WatchdogEnabled);
                    sw.WriteLine("WatchdogAction=" + WatchdogAction);
                    sw.WriteLine("WatchdogMode=" + WatchdogMode);
                    sw.WriteLine("WatchdogTargetProcess=" + WatchdogTargetProcess);
                    sw.WriteLine("WatchdogGraceMinutes=" + WatchdogGraceMinutes);
                    sw.WriteLine("StartWithWindows=" + StartWithWindows);
                    sw.WriteLine("PlayAlertSound=" + PlayAlertSound);
                    sw.WriteLine("CountdownSeconds=" + CountdownSeconds);
                }
            }
            catch { }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath);
                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("[") || line.StartsWith("#")) continue;

                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string val = parts[1].Trim();

                            // Backward compatibility with v1
                            if (key == "IsEnabled") bool.TryParse(val, out s.ChargerGuardEnabled);
                            else if (key == "ChargerGuardEnabled") bool.TryParse(val, out s.ChargerGuardEnabled);
                            else if (key == "ChargerGuardAction") s.ChargerGuardAction = val;
                            else if (key == "WatchdogEnabled") bool.TryParse(val, out s.WatchdogEnabled);
                            else if (key == "WatchdogAction") s.WatchdogAction = val;
                            else if (key == "WatchdogMode") s.WatchdogMode = val;
                            else if (key == "WatchdogTargetProcess") s.WatchdogTargetProcess = val;
                            else if (key == "WatchdogGraceMinutes") int.TryParse(val, out s.WatchdogGraceMinutes);
                            else if (key == "StartWithWindows") bool.TryParse(val, out s.StartWithWindows);
                            else if (key == "PlayAlertSound") bool.TryParse(val, out s.PlayAlertSound);
                            else if (key == "CountdownSeconds") int.TryParse(val, out s.CountdownSeconds);
                        }
                    }
                }
            }
            catch { }
            return s;
        }
    }
    #endregion

    #region Power Actions Native Helper
    public static class PowerManager
    {
        [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        public static void ExecuteAction(string actionName)
        {
            try
            {
                switch (actionName)
                {
                    case "Shutdown":
                        Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0 /f")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        break;
                    case "Hibernate":
                        SetSuspendState(true, true, false);
                        break;
                    case "Lock":
                        LockWorkStation();
                        break;
                    case "Sleep":
                    default:
                        // Native Windows Standby S3
                        SetSuspendState(false, true, false);
                        break;
                }
            }
            catch { }
        }
    }
    #endregion

    #region Process & Task Watchdog Engine
    public class WorkWatchdogEngine
    {
        // Known Dev & AI Agent process signatures
        public static readonly string[] KnownDevProcessNames = new string[]
        {
            // AI Agents & CLI Tools
            "claude", "aider", "codex", "copilot", "gemini", "antigravity", "cline", "roo-cline",
            // Popular AI & Dev IDEs
            "cursor", "code", "windsurf", "devenv", "idea64", "pycharm64", "webstorm64", "rider64", "sublime_text",
            // Runtimes, Compilers & Build Tools
            "node", "python", "pythonw", "cargo", "rustc", "dotnet", "javac", "java", "gradlew", "tsc", "git",
            // Terminals
            "windowsterminal", "powershell", "pwsh", "wsl", "cmd"
        };

        private Dictionary<int, TimeSpan> lastCpuTimes = new Dictionary<int, TimeSpan>();
        private DateTime lastSampleTime = DateTime.UtcNow;

        public double LastSampledCpuPercent { get; private set; }
        public int MonitoredProcessCount { get; private set; }
        public string MonitoredProcessSummary { get; private set; }
        public bool HasActiveWork { get; private set; }
        public int ConsecutiveIdleSeconds { get; private set; }

        public WorkWatchdogEngine()
        {
            MonitoredProcessSummary = "Initializing...";
            HasActiveWork = false;
            ConsecutiveIdleSeconds = 0;
        }

        public void ResetIdleTimer()
        {
            ConsecutiveIdleSeconds = 0;
        }

        public void Poll(string mode, string targetCustomProcess, int tickIntervalMs)
        {
            List<Process> targetProcesses = new List<Process>();

            try
            {
                if (mode == "AutoDev")
                {
                    // Scan all running processes for known dev agents & tools
                    Process[] all = Process.GetProcesses();
                    foreach (Process p in all)
                    {
                        try
                        {
                            string pName = p.ProcessName.ToLowerInvariant();
                            foreach (string devPattern in KnownDevProcessNames)
                            {
                                if (pName == devPattern || pName.Contains(devPattern))
                                {
                                    targetProcesses.Add(p);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
                else if (mode == "SpecificProcess")
                {
                    string target = (targetCustomProcess ?? "").Trim().ToLowerInvariant();
                    if (target.EndsWith(".exe")) target = target.Substring(0, target.Length - 4);

                    if (!string.IsNullOrEmpty(target))
                    {
                        Process[] all = Process.GetProcesses();
                        foreach (Process p in all)
                        {
                            try
                            {
                                if (p.ProcessName.ToLowerInvariant().Contains(target))
                                {
                                    targetProcesses.Add(p);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            MonitoredProcessCount = targetProcesses.Count;

            DateTime now = DateTime.UtcNow;
            double elapsedSeconds = (now - lastSampleTime).TotalSeconds;
            if (elapsedSeconds <= 0.1) elapsedSeconds = tickIntervalMs / 1000.0;
            lastSampleTime = now;

            if (targetProcesses.Count == 0)
            {
                // No monitored processes are running
                LastSampledCpuPercent = 0.0;
                MonitoredProcessSummary = (mode == "AutoDev") ? "No dev tools/agents currently active" : "Target process not running";
                HasActiveWork = false;
                ConsecutiveIdleSeconds += (int)Math.Max(1, Math.Round(elapsedSeconds));
                lastCpuTimes.Clear();
                return;
            }

            // Calculate CPU utilization across target processes
            TimeSpan totalCpuDelta = TimeSpan.Zero;
            Dictionary<int, TimeSpan> currentCpuTimes = new Dictionary<int, TimeSpan>();
            List<string> activeNames = new List<string>();

            foreach (Process p in targetProcesses)
            {
                try
                {
                    TimeSpan curCpu = p.TotalProcessorTime;
                    currentCpuTimes[p.Id] = curCpu;

                    if (lastCpuTimes.ContainsKey(p.Id))
                    {
                        TimeSpan delta = curCpu - lastCpuTimes[p.Id];
                        if (delta > TimeSpan.Zero)
                        {
                            totalCpuDelta += delta;
                        }
                    }

                    if (!activeNames.Contains(p.ProcessName))
                    {
                        activeNames.Add(p.ProcessName);
                    }
                }
                catch { }
            }

            lastCpuTimes = currentCpuTimes;

            // Compute CPU% normalized by processor count
            int procCount = Math.Max(1, Environment.ProcessorCount);
            double cpuPercent = (totalCpuDelta.TotalSeconds / (elapsedSeconds * procCount)) * 100.0;
            if (cpuPercent > 100.0) cpuPercent = 100.0;
            if (cpuPercent < 0.0) cpuPercent = 0.0;
            LastSampledCpuPercent = cpuPercent;

            // Summary text
            if (activeNames.Count > 0)
            {
                string names = string.Join(", ", activeNames.ToArray());
                if (names.Length > 45) names = names.Substring(0, 42) + "...";
                MonitoredProcessSummary = string.Format("{0} tasks ({1})", activeNames.Count, names);
            }
            else
            {
                MonitoredProcessSummary = targetProcesses.Count + " processes";
            }

            // Work is active if CPU > 1.8% (compilation, agent coding, active script)
            // If CPU <= 1.8%, consider idle
            if (cpuPercent >= 1.8)
            {
                HasActiveWork = true;
                ConsecutiveIdleSeconds = 0; // Reset idle timer since active work is happening
            }
            else
            {
                HasActiveWork = false;
                ConsecutiveIdleSeconds += (int)Math.Max(1, Math.Round(elapsedSeconds));
            }

            // Dispose process objects
            foreach (Process p in targetProcesses)
            {
                try { p.Dispose(); } catch { }
            }
        }
    }
    #endregion

    #region Safety Countdown Dialog
    public class SafetyCountdownForm : Form
    {
        private int remainingSeconds;
        private string targetAction;
        private Timer countdownTimer;
        private Label lblHeader;
        private Label lblAction;
        private Label lblTimer;
        private ProgressBar prgBar;
        private Button btnCancel;
        private Button btnExecuteNow;
        private bool playSound;

        public bool WasCancelled { get; private set; }

        public SafetyCountdownForm(string action, int seconds, bool sound)
        {
            this.targetAction = action;
            this.remainingSeconds = seconds;
            this.playSound = sound;
            this.WasCancelled = false;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "PowerLockGuard - Auto " + targetAction + " Pending";
            this.Size = new Size(500, 290);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(15, 23, 42); // Dark Slate
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10f, FontStyle.Regular);

            // Warning Icon & Header
            lblHeader = new Label();
            lblHeader.Text = "⏰ ALL MONITORED TASKS FINISHED!";
            lblHeader.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblHeader.ForeColor = Color.FromArgb(245, 158, 11); // Amber
            lblHeader.Location = new Point(25, 20);
            lblHeader.Size = new Size(450, 30);
            this.Controls.Add(lblHeader);

            lblAction = new Label();
            lblAction.Text = string.Format("All developer agents & IDE work went idle. Your PC will {0} shortly.", targetAction.ToUpper());
            lblAction.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblAction.ForeColor = Color.FromArgb(203, 213, 225);
            lblAction.Location = new Point(27, 52);
            lblAction.Size = new Size(445, 25);
            this.Controls.Add(lblAction);

            // Big Timer Display
            lblTimer = new Label();
            lblTimer.Text = string.Format("{0} will trigger in: {1}s", targetAction, remainingSeconds);
            lblTimer.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            lblTimer.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            lblTimer.Location = new Point(25, 85);
            lblTimer.Size = new Size(450, 40);
            lblTimer.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblTimer);

            // Progress Bar
            prgBar = new ProgressBar();
            prgBar.Location = new Point(30, 135);
            prgBar.Size = new Size(430, 16);
            prgBar.Maximum = remainingSeconds;
            prgBar.Value = remainingSeconds;
            this.Controls.Add(prgBar);

            // Cancel Button (Large, prominent)
            btnCancel = new Button();
            btnCancel.Text = "❌ CANCEL (বাতিল করুন - I'm still working)";
            btnCancel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            btnCancel.BackColor = Color.FromArgb(239, 68, 68); // Red
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Location = new Point(30, 175);
            btnCancel.Size = new Size(270, 48);
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.Click += (s, e) =>
            {
                WasCancelled = true;
                countdownTimer.Stop();
                this.Close();
            };
            this.Controls.Add(btnCancel);

            // Execute Now Button
            btnExecuteNow = new Button();
            btnExecuteNow.Text = "⚡ " + targetAction + " Now";
            btnExecuteNow.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            btnExecuteNow.BackColor = Color.FromArgb(51, 65, 85); // Slate
            btnExecuteNow.ForeColor = Color.White;
            btnExecuteNow.FlatStyle = FlatStyle.Flat;
            btnExecuteNow.FlatAppearance.BorderSize = 0;
            btnExecuteNow.Location = new Point(310, 175);
            btnExecuteNow.Size = new Size(150, 48);
            btnExecuteNow.Cursor = Cursors.Hand;
            btnExecuteNow.Click += (s, e) =>
            {
                WasCancelled = false;
                countdownTimer.Stop();
                this.Close();
            };
            this.Controls.Add(btnExecuteNow);

            // Keyboard shortcut (Esc or Space cancels)
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Space)
                {
                    WasCancelled = true;
                    countdownTimer.Stop();
                    this.Close();
                }
            };

            // Timer
            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();

            PlayBeep();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            remainingSeconds--;

            if (remainingSeconds <= 0)
            {
                countdownTimer.Stop();
                WasCancelled = false;
                this.Close();
                return;
            }

            lblTimer.Text = string.Format("{0} will trigger in: {1}s", targetAction, remainingSeconds);
            if (remainingSeconds <= prgBar.Maximum && remainingSeconds >= 0)
            {
                prgBar.Value = remainingSeconds;
            }

            if (remainingSeconds <= 5 || remainingSeconds == 10 || remainingSeconds == 20)
            {
                PlayBeep();
            }
        }

        private void PlayBeep()
        {
            if (!playSound) return;
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch { }
        }
    }
    #endregion

    #region Main Form with Fluent Tabbed UI
    public class MainForm : Form
    {
        private AppSettings settings;
        private WorkWatchdogEngine watchdogEngine;

        private Timer powerMonitorTimer;
        private Timer watchdogTimer;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        // Custom Tab Controls
        private Panel pnlHeader;
        private Panel pnlTabNav;
        private Panel pnlTabContainer;
        private Button btnTabCharger;
        private Button btnTabWatchdog;
        private Button btnTabSettings;

        // --- Tab 1: Charger Guard Controls ---
        private Panel tabCharger;
        private Button btnToggleCharger;
        private Label lblChargerStatus;
        private Label lblChargerDescription;
        private ComboBox cmbChargerAction;
        private Label lblChargerActionPrompt;

        // --- Tab 2: Work Watchdog Controls ---
        private Panel tabWatchdog;
        private Button btnToggleWatchdog;
        private ComboBox cmbWatchdogAction;
        private ComboBox cmbWatchdogMode;
        private ComboBox cmbWatchdogGrace;
        private TextBox txtCustomProcess;
        private Label lblWatchdogActivity;
        private Label lblWatchdogCpu;
        private Label lblWatchdogTimer;
        private ProgressBar prgActivity;
        private Button btnRefreshProcesses;

        // --- Tab 3: Settings & Logs Controls ---
        private Panel tabSettings;
        private CheckBox chkStartup;
        private CheckBox chkSound;
        private ComboBox cmbCountdownSeconds;
        private ListBox lstActivityLog;
        private Button btnClearLog;
        private Button btnTestCountdown;
        private Button btnTestSleep;

        // State variables
        private bool lastWasOnline = true;
        private bool isInitialized = false;
        private bool isCountdownActive = false;

        public MainForm()
        {
            settings = AppSettings.Load();
            watchdogEngine = new WorkWatchdogEngine();

            InitializeUI();
            InitializeTray();
            UpdateStartupShortcut(settings.StartWithWindows);

            lastWasOnline = IsPowerPlugged();
            isInitialized = true;

            // Power monitor timer (every 500ms)
            powerMonitorTimer = new Timer();
            powerMonitorTimer.Interval = 500;
            powerMonitorTimer.Tick += PowerMonitorTimer_Tick;
            powerMonitorTimer.Start();

            // Watchdog evaluation timer (every 1000ms)
            watchdogTimer = new Timer();
            watchdogTimer.Interval = 1000;
            watchdogTimer.Tick += WatchdogTimer_Tick;
            watchdogTimer.Start();

            LogActivity("PowerLockGuard v2.0 started successfully.");
            if (settings.ChargerGuardEnabled) LogActivity("Charger Unplug Guard is ACTIVE.");
            if (settings.WatchdogEnabled) LogActivity("Work Watchdog is ACTIVE.");
        }

        private bool IsPowerPlugged()
        {
            return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
        }

        private void InitializeUI()
        {
            this.Text = "PowerLockGuard v2.0 - Dev Work & Charger Guard";
            this.Size = new Size(540, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(248, 250, 252); // Modern light slate background
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            // Load app icon if present
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                try { this.Icon = new Icon(icoPath); } catch { }
            }

            // Header Banner
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 70;
            pnlHeader.BackColor = Color.FromArgb(15, 23, 42); // Dark slate #0f172a
            this.Controls.Add(pnlHeader);

            Label lblTitle = new Label();
            lblTitle.Text = "PowerLockGuard";
            lblTitle.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(18, 12);
            lblTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblTitle);

            Label lblBadge = new Label();
            lblBadge.Text = "v2.0 PRO";
            lblBadge.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            lblBadge.ForeColor = Color.FromArgb(16, 185, 129);
            lblBadge.BackColor = Color.FromArgb(30, 41, 59);
            lblBadge.Location = new Point(190, 16);
            lblBadge.Size = new Size(58, 20);
            lblBadge.TextAlign = ContentAlignment.MiddleCenter;
            pnlHeader.Controls.Add(lblBadge);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "AI Agent & IDE Work Watchdog + Instant Charger Unplug Guard";
            lblSubtitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblSubtitle.Location = new Point(20, 40);
            lblSubtitle.AutoSize = true;
            pnlHeader.Controls.Add(lblSubtitle);

            // Navigation Bar (Tabs)
            pnlTabNav = new Panel();
            pnlTabNav.Dock = DockStyle.Top;
            pnlTabNav.Height = 42;
            pnlTabNav.BackColor = Color.FromArgb(241, 245, 249);
            this.Controls.Add(pnlTabNav);

            btnTabCharger = CreateTabButton("🛡️ Charger Guard", 0);
            btnTabWatchdog = CreateTabButton("🤖 Work Watchdog", 1);
            btnTabSettings = CreateTabButton("⚙️ Settings & Logs", 2);

            btnTabCharger.Click += (s, e) => SwitchTab(0);
            btnTabWatchdog.Click += (s, e) => SwitchTab(1);
            btnTabSettings.Click += (s, e) => SwitchTab(2);

            pnlTabNav.Controls.Add(btnTabCharger);
            pnlTabNav.Controls.Add(btnTabWatchdog);
            pnlTabNav.Controls.Add(btnTabSettings);

            // Tab Container
            pnlTabContainer = new Panel();
            pnlTabContainer.Dock = DockStyle.Fill;
            this.Controls.Add(pnlTabContainer);

            // Initialize 3 tab panels
            BuildTabCharger();
            BuildTabWatchdog();
            BuildTabSettings();

            // Bring Tab 2 (Work Watchdog) or Tab 1 to front
            SwitchTab(1); // Default to Work Watchdog to highlight new features!

            this.FormClosing += MainForm_FormClosing;
        }

        private Button CreateTabButton(string text, int index)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(175, 40);
            btn.Location = new Point(index * 176, 1);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Color.FromArgb(71, 85, 105);
            return btn;
        }

        private void SwitchTab(int tabIndex)
        {
            tabCharger.Visible = (tabIndex == 0);
            tabWatchdog.Visible = (tabIndex == 1);
            tabSettings.Visible = (tabIndex == 2);

            btnTabCharger.BackColor = (tabIndex == 0) ? Color.White : Color.Transparent;
            btnTabCharger.ForeColor = (tabIndex == 0) ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);

            btnTabWatchdog.BackColor = (tabIndex == 1) ? Color.White : Color.Transparent;
            btnTabWatchdog.ForeColor = (tabIndex == 1) ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);

            btnTabSettings.BackColor = (tabIndex == 2) ? Color.White : Color.Transparent;
            btnTabSettings.ForeColor = (tabIndex == 2) ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);
        }

        #region Tab 1: Charger Guard Builder
        private void BuildTabCharger()
        {
            tabCharger = new Panel();
            tabCharger.Dock = DockStyle.Fill;
            pnlTabContainer.Controls.Add(tabCharger);

            // Big Toggle Button
            btnToggleCharger = new Button();
            btnToggleCharger.Location = new Point(25, 20);
            btnToggleCharger.Size = new Size(475, 58);
            btnToggleCharger.FlatStyle = FlatStyle.Flat;
            btnToggleCharger.FlatAppearance.BorderSize = 0;
            btnToggleCharger.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            btnToggleCharger.Cursor = Cursors.Hand;
            btnToggleCharger.Click += BtnToggleCharger_Click;
            tabCharger.Controls.Add(btnToggleCharger);
            UpdateChargerToggleUI();

            // Status Card
            GroupBox grpStatus = new GroupBox();
            grpStatus.Text = " Charger & Power Status ";
            grpStatus.Location = new Point(25, 95);
            grpStatus.Size = new Size(475, 110);
            grpStatus.ForeColor = Color.FromArgb(71, 85, 105);
            tabCharger.Controls.Add(grpStatus);

            lblChargerStatus = new Label();
            lblChargerStatus.Location = new Point(18, 25);
            lblChargerStatus.Size = new Size(440, 25);
            lblChargerStatus.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            grpStatus.Controls.Add(lblChargerStatus);

            lblChargerDescription = new Label();
            lblChargerDescription.Location = new Point(18, 55);
            lblChargerDescription.Size = new Size(440, 45);
            lblChargerDescription.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            lblChargerDescription.ForeColor = Color.FromArgb(100, 116, 139);
            grpStatus.Controls.Add(lblChargerDescription);

            // Action Selection Card
            GroupBox grpAction = new GroupBox();
            grpAction.Text = " Action When Charger Unplugged ";
            grpAction.Location = new Point(25, 220);
            grpAction.Size = new Size(475, 80);
            grpAction.ForeColor = Color.FromArgb(71, 85, 105);
            tabCharger.Controls.Add(grpAction);

            lblChargerActionPrompt = new Label();
            lblChargerActionPrompt.Text = "Trigger this action on unplug:";
            lblChargerActionPrompt.Location = new Point(18, 30);
            lblChargerActionPrompt.Size = new Size(190, 25);
            grpAction.Controls.Add(lblChargerActionPrompt);

            cmbChargerAction = new ComboBox();
            cmbChargerAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbChargerAction.Items.AddRange(new object[] {
                "Sleep (Standby S3 - Recommended)",
                "Shut Down PC",
                "Hibernate PC",
                "Lock Workstation"
            });
            cmbChargerAction.Location = new Point(215, 26);
            cmbChargerAction.Size = new Size(240, 28);
            SetComboSelectedAction(cmbChargerAction, settings.ChargerGuardAction);
            cmbChargerAction.SelectedIndexChanged += (s, e) =>
            {
                settings.ChargerGuardAction = GetActionFromCombo(cmbChargerAction);
                settings.Save();
                LogActivity("Charger unplug action changed to: " + settings.ChargerGuardAction);
            };
            grpAction.Controls.Add(cmbChargerAction);

            Label lblNote = new Label();
            lblNote.Text = "💡 Tip: Sleep mode keeps all open IDE files and RAM intact. Zero battery drain.";
            lblNote.Location = new Point(28, 315);
            lblNote.Size = new Size(470, 30);
            lblNote.ForeColor = Color.FromArgb(100, 116, 139);
            lblNote.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            tabCharger.Controls.Add(lblNote);
        }
        #endregion

        #region Tab 2: Work Watchdog Builder
        private void BuildTabWatchdog()
        {
            tabWatchdog = new Panel();
            tabWatchdog.Dock = DockStyle.Fill;
            pnlTabContainer.Controls.Add(tabWatchdog);

            // Master Watchdog Toggle Button
            btnToggleWatchdog = new Button();
            btnToggleWatchdog.Location = new Point(25, 15);
            btnToggleWatchdog.Size = new Size(475, 54);
            btnToggleWatchdog.FlatStyle = FlatStyle.Flat;
            btnToggleWatchdog.FlatAppearance.BorderSize = 0;
            btnToggleWatchdog.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            btnToggleWatchdog.Cursor = Cursors.Hand;
            btnToggleWatchdog.Click += BtnToggleWatchdog_Click;
            tabWatchdog.Controls.Add(btnToggleWatchdog);
            UpdateWatchdogToggleUI();

            // Configuration Group
            GroupBox grpConfig = new GroupBox();
            grpConfig.Text = " Watchdog Monitoring Settings ";
            grpConfig.Location = new Point(25, 80);
            grpConfig.Size = new Size(475, 155);
            grpConfig.ForeColor = Color.FromArgb(71, 85, 105);
            tabWatchdog.Controls.Add(grpConfig);

            // Row 1: Action when work finishes
            Label lblAct = new Label();
            lblAct.Text = "When work finishes:";
            lblAct.Location = new Point(15, 26);
            lblAct.Size = new Size(150, 22);
            grpConfig.Controls.Add(lblAct);

            cmbWatchdogAction = new ComboBox();
            cmbWatchdogAction.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbWatchdogAction.Items.AddRange(new object[] {
                "Sleep (Standby S3 - Recommended)",
                "Shut Down PC",
                "Hibernate PC"
            });
            cmbWatchdogAction.Location = new Point(175, 23);
            cmbWatchdogAction.Size = new Size(280, 28);
            SetComboSelectedAction(cmbWatchdogAction, settings.WatchdogAction);
            cmbWatchdogAction.SelectedIndexChanged += (s, e) =>
            {
                settings.WatchdogAction = GetActionFromCombo(cmbWatchdogAction);
                settings.Save();
                LogActivity("Watchdog action changed to: " + settings.WatchdogAction);
            };
            grpConfig.Controls.Add(cmbWatchdogAction);

            // Row 2: Target Tasks / Agents
            Label lblTarget = new Label();
            lblTarget.Text = "Monitored tasks:";
            lblTarget.Location = new Point(15, 60);
            lblTarget.Size = new Size(150, 22);
            grpConfig.Controls.Add(lblTarget);

            cmbWatchdogMode = new ComboBox();
            cmbWatchdogMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbWatchdogMode.Items.AddRange(new object[] {
                "Auto-Detect AI Agents & IDEs (Claude, Cursor, VS Code, etc.)",
                "Specific Process Name (e.g. claude, python, npm)"
            });
            cmbWatchdogMode.Location = new Point(175, 57);
            cmbWatchdogMode.Size = new Size(280, 28);
            cmbWatchdogMode.SelectedIndex = (settings.WatchdogMode == "SpecificProcess") ? 1 : 0;
            cmbWatchdogMode.SelectedIndexChanged += (s, e) =>
            {
                settings.WatchdogMode = (cmbWatchdogMode.SelectedIndex == 1) ? "SpecificProcess" : "AutoDev";
                txtCustomProcess.Visible = (settings.WatchdogMode == "SpecificProcess");
                settings.Save();
                LogActivity("Watchdog mode set to: " + settings.WatchdogMode);
            };
            grpConfig.Controls.Add(cmbWatchdogMode);

            // Row 2.5: Custom process textbox
            txtCustomProcess = new TextBox();
            txtCustomProcess.Location = new Point(175, 88);
            txtCustomProcess.Size = new Size(280, 25);
            txtCustomProcess.Text = settings.WatchdogTargetProcess;
            txtCustomProcess.Visible = (settings.WatchdogMode == "SpecificProcess");
            txtCustomProcess.TextChanged += (s, e) =>
            {
                settings.WatchdogTargetProcess = txtCustomProcess.Text.Trim();
                settings.Save();
            };
            grpConfig.Controls.Add(txtCustomProcess);

            // Row 3: Grace Period
            Label lblGrace = new Label();
            lblGrace.Text = "Inactivity buffer:";
            lblGrace.Location = new Point(15, 120);
            lblGrace.Size = new Size(150, 22);
            grpConfig.Controls.Add(lblGrace);

            cmbWatchdogGrace = new ComboBox();
            cmbWatchdogGrace.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbWatchdogGrace.Items.AddRange(new object[] {
                "1 Minute",
                "2 Minutes",
                "3 Minutes (Recommended)",
                "5 Minutes",
                "10 Minutes"
            });
            cmbWatchdogGrace.Location = new Point(175, 117);
            cmbWatchdogGrace.Size = new Size(280, 28);
            SelectGraceCombo(cmbWatchdogGrace, settings.WatchdogGraceMinutes);
            cmbWatchdogGrace.SelectedIndexChanged += (s, e) =>
            {
                settings.WatchdogGraceMinutes = GetGraceMinutesFromCombo(cmbWatchdogGrace);
                settings.Save();
                LogActivity("Inactivity buffer set to: " + settings.WatchdogGraceMinutes + " minutes");
            };
            grpConfig.Controls.Add(cmbWatchdogGrace);

            // Live Telemetry & Status Group
            GroupBox grpTelemetry = new GroupBox();
            grpTelemetry.Text = " Live Work & Task Telemetry ";
            grpTelemetry.Location = new Point(25, 245);
            grpTelemetry.Size = new Size(475, 115);
            grpTelemetry.ForeColor = Color.FromArgb(71, 85, 105);
            tabWatchdog.Controls.Add(grpTelemetry);

            lblWatchdogActivity = new Label();
            lblWatchdogActivity.Text = "Status: Initializing...";
            lblWatchdogActivity.Location = new Point(15, 24);
            lblWatchdogActivity.Size = new Size(330, 22);
            lblWatchdogActivity.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grpTelemetry.Controls.Add(lblWatchdogActivity);

            lblWatchdogCpu = new Label();
            lblWatchdogCpu.Text = "CPU: 0.0%";
            lblWatchdogCpu.Location = new Point(360, 24);
            lblWatchdogCpu.Size = new Size(95, 22);
            lblWatchdogCpu.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblWatchdogCpu.TextAlign = ContentAlignment.TopRight;
            grpTelemetry.Controls.Add(lblWatchdogCpu);

            prgActivity = new ProgressBar();
            prgActivity.Location = new Point(18, 50);
            prgActivity.Size = new Size(438, 12);
            prgActivity.Maximum = 100;
            grpTelemetry.Controls.Add(prgActivity);

            lblWatchdogTimer = new Label();
            lblWatchdogTimer.Text = "Waiting for tasks to start...";
            lblWatchdogTimer.Location = new Point(15, 72);
            lblWatchdogTimer.Size = new Size(350, 32);
            lblWatchdogTimer.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblWatchdogTimer.ForeColor = Color.FromArgb(100, 116, 139);
            grpTelemetry.Controls.Add(lblWatchdogTimer);

            btnRefreshProcesses = new Button();
            btnRefreshProcesses.Text = "🔄 Scan";
            btnRefreshProcesses.Location = new Point(375, 72);
            btnRefreshProcesses.Size = new Size(80, 28);
            btnRefreshProcesses.FlatStyle = FlatStyle.Flat;
            btnRefreshProcesses.BackColor = Color.FromArgb(241, 245, 249);
            btnRefreshProcesses.Click += (s, e) =>
            {
                watchdogEngine.Poll(settings.WatchdogMode, settings.WatchdogTargetProcess, 1000);
                UpdateTelemetryUI();
            };
            grpTelemetry.Controls.Add(btnRefreshProcesses);
        }
        #endregion

        #region Tab 3: Settings & Logs Builder
        private void BuildTabSettings()
        {
            tabSettings = new Panel();
            tabSettings.Dock = DockStyle.Fill;
            pnlTabContainer.Controls.Add(tabSettings);

            // General options
            chkStartup = new CheckBox();
            chkStartup.Text = "Start PowerLockGuard automatically with Windows";
            chkStartup.Location = new Point(25, 15);
            chkStartup.AutoSize = true;
            chkStartup.Checked = settings.StartWithWindows;
            chkStartup.CheckedChanged += (s, e) =>
            {
                settings.StartWithWindows = chkStartup.Checked;
                settings.Save();
                UpdateStartupShortcut(settings.StartWithWindows);
                LogActivity("Startup with Windows set to: " + settings.StartWithWindows);
            };
            tabSettings.Controls.Add(chkStartup);

            chkSound = new CheckBox();
            chkSound.Text = "Play warning sound alert before Sleep / Shutdown";
            chkSound.Location = new Point(25, 42);
            chkSound.AutoSize = true;
            chkSound.Checked = settings.PlayAlertSound;
            chkSound.CheckedChanged += (s, e) =>
            {
                settings.PlayAlertSound = chkSound.Checked;
                settings.Save();
            };
            tabSettings.Controls.Add(chkSound);

            Label lblCountPrompt = new Label();
            lblCountPrompt.Text = "Safety Countdown Duration:";
            lblCountPrompt.Location = new Point(25, 73);
            lblCountPrompt.Size = new Size(180, 22);
            tabSettings.Controls.Add(lblCountPrompt);

            cmbCountdownSeconds = new ComboBox();
            cmbCountdownSeconds.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCountdownSeconds.Items.AddRange(new object[] { "15 Seconds", "30 Seconds", "60 Seconds" });
            cmbCountdownSeconds.Location = new Point(210, 70);
            cmbCountdownSeconds.Size = new Size(130, 26);
            if (settings.CountdownSeconds == 15) cmbCountdownSeconds.SelectedIndex = 0;
            else if (settings.CountdownSeconds == 60) cmbCountdownSeconds.SelectedIndex = 2;
            else cmbCountdownSeconds.SelectedIndex = 1;

            cmbCountdownSeconds.SelectedIndexChanged += (s, e) =>
            {
                if (cmbCountdownSeconds.SelectedIndex == 0) settings.CountdownSeconds = 15;
                else if (cmbCountdownSeconds.SelectedIndex == 2) settings.CountdownSeconds = 60;
                else settings.CountdownSeconds = 30;
                settings.Save();
            };
            tabSettings.Controls.Add(cmbCountdownSeconds);

            // Action Test buttons
            btnTestCountdown = new Button();
            btnTestCountdown.Text = "🔔 Test Warning Dialog";
            btnTestCountdown.Location = new Point(355, 68);
            btnTestCountdown.Size = new Size(145, 30);
            btnTestCountdown.FlatStyle = FlatStyle.Flat;
            btnTestCountdown.BackColor = Color.FromArgb(241, 245, 249);
            btnTestCountdown.Cursor = Cursors.Hand;
            btnTestCountdown.Click += (s, e) => TriggerCountdownAndAction(true);
            tabSettings.Controls.Add(btnTestCountdown);

            // Activity Log Box
            GroupBox grpLog = new GroupBox();
            grpLog.Text = " Activity & Event History ";
            grpLog.Location = new Point(25, 110);
            grpLog.Size = new Size(475, 240);
            grpLog.ForeColor = Color.FromArgb(71, 85, 105);
            tabSettings.Controls.Add(grpLog);

            lstActivityLog = new ListBox();
            lstActivityLog.Location = new Point(15, 25);
            lstActivityLog.Size = new Size(445, 160);
            lstActivityLog.Font = new Font("Consolas", 8.25f, FontStyle.Regular);
            lstActivityLog.BackColor = Color.FromArgb(248, 250, 252);
            grpLog.Controls.Add(lstActivityLog);

            btnClearLog = new Button();
            btnClearLog.Text = "Clear History";
            btnClearLog.Location = new Point(15, 195);
            btnClearLog.Size = new Size(100, 28);
            btnClearLog.FlatStyle = FlatStyle.Flat;
            btnClearLog.Click += (s, e) => lstActivityLog.Items.Clear();
            grpLog.Controls.Add(btnClearLog);

            btnTestSleep = new Button();
            btnTestSleep.Text = "💤 Sleep PC Now";
            btnTestSleep.Location = new Point(340, 195);
            btnTestSleep.Size = new Size(120, 28);
            btnTestSleep.FlatStyle = FlatStyle.Flat;
            btnTestSleep.BackColor = Color.FromArgb(226, 232, 240);
            btnTestSleep.Click += (s, e) =>
            {
                LogActivity("Manual Test Sleep triggered.");
                PowerManager.ExecuteAction("Sleep");
            };
            grpLog.Controls.Add(btnTestSleep);
        }
        #endregion

        #region UI Update & Event Handlers
        private void UpdateChargerToggleUI()
        {
            if (settings.ChargerGuardEnabled)
            {
                btnToggleCharger.Text = "🛡️ CHARGER GUARD IS ON\n(Click to Pause / Turn OFF)";
                btnToggleCharger.BackColor = Color.FromArgb(16, 185, 129); // Emerald Green
                btnToggleCharger.ForeColor = Color.White;
            }
            else
            {
                btnToggleCharger.Text = "⏸️ CHARGER GUARD IS OFF\n(Click to Turn ON Protection)";
                btnToggleCharger.BackColor = Color.FromArgb(239, 68, 68); // Red
                btnToggleCharger.ForeColor = Color.White;
            }
            UpdateTrayTooltip();
        }

        private void BtnToggleCharger_Click(object sender, EventArgs e)
        {
            settings.ChargerGuardEnabled = !settings.ChargerGuardEnabled;
            settings.Save();
            UpdateChargerToggleUI();
            LogActivity(settings.ChargerGuardEnabled ? "Charger Guard turned ON." : "Charger Guard turned OFF.");
        }

        private void UpdateWatchdogToggleUI()
        {
            if (settings.WatchdogEnabled)
            {
                btnToggleWatchdog.Text = "🤖 WORK WATCHDOG IS ACTIVE\n(Monitoring Tasks — Will " + settings.WatchdogAction + " when done)";
                btnToggleWatchdog.BackColor = Color.FromArgb(37, 99, 235); // Royal Blue
                btnToggleWatchdog.ForeColor = Color.White;
            }
            else
            {
                btnToggleWatchdog.Text = "▶️ ACTIVATE WORK WATCHDOG\n(Auto-Sleep / Shutdown when AI Agents & IDE finish)";
                btnToggleWatchdog.BackColor = Color.FromArgb(100, 116, 139); // Slate Gray
                btnToggleWatchdog.ForeColor = Color.White;
            }
            UpdateTrayTooltip();
        }

        private void BtnToggleWatchdog_Click(object sender, EventArgs e)
        {
            settings.WatchdogEnabled = !settings.WatchdogEnabled;
            settings.Save();
            UpdateWatchdogToggleUI();
            watchdogEngine.ResetIdleTimer();
            LogActivity(settings.WatchdogEnabled ? "Work Watchdog ACTIVATED." : "Work Watchdog STOPPED.");
        }

        private void PowerMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!isInitialized) return;

            bool isOnline = IsPowerPlugged();

            // Update UI status text
            if (isOnline)
            {
                lblChargerStatus.Text = "⚡ Charger: CONNECTED (Charging / Plugged in)";
                lblChargerStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            else
            {
                lblChargerStatus.Text = "🔋 Charger: UNPLUGGED (Running on Battery)";
                lblChargerStatus.ForeColor = Color.FromArgb(220, 38, 38);
            }

            if (settings.ChargerGuardEnabled)
            {
                lblChargerDescription.Text = isOnline
                    ? string.Format("Protection ACTIVE: Unplugging charger will instantly trigger {0}.", settings.ChargerGuardAction.ToUpper())
                    : "Running on battery. Plug in charger to reactivate unplug protection.";
            }
            else
            {
                lblChargerDescription.Text = "Protection is PAUSED. You can unplug safely without triggering action.";
            }

            // TRIGGER: Instant action when charger is unplugged
            if (settings.ChargerGuardEnabled)
            {
                if (lastWasOnline && !isOnline)
                {
                    lastWasOnline = false;
                    LogActivity(string.Format("Charger unplugged! Triggering {0} instantly.", settings.ChargerGuardAction));
                    PowerManager.ExecuteAction(settings.ChargerGuardAction);
                    return;
                }
            }

            lastWasOnline = isOnline;
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            if (!isInitialized) return;

            // Poll watchdog engine
            watchdogEngine.Poll(settings.WatchdogMode, settings.WatchdogTargetProcess, watchdogTimer.Interval);
            UpdateTelemetryUI();

            // Check if Watchdog is enabled and grace period has expired
            if (settings.WatchdogEnabled && !isCountdownActive)
            {
                int graceSeconds = settings.WatchdogGraceMinutes * 60;

                // If tasks are idle consecutively for the grace duration:
                if (watchdogEngine.ConsecutiveIdleSeconds >= graceSeconds)
                {
                    LogActivity(string.Format("Tasks have been idle for {0}m. Starting countdown to {1}.",
                        settings.WatchdogGraceMinutes, settings.WatchdogAction));
                    TriggerCountdownAndAction(false);
                }
            }
        }

        private void UpdateTelemetryUI()
        {
            double cpu = watchdogEngine.LastSampledCpuPercent;
            lblWatchdogCpu.Text = string.Format("CPU: {0:F1}%", cpu);

            int prgVal = (int)Math.Min(100, Math.Max(0, cpu));
            prgActivity.Value = prgVal;

            if (watchdogEngine.HasActiveWork)
            {
                lblWatchdogActivity.Text = "🟢 WORKING: " + watchdogEngine.MonitoredProcessSummary;
                lblWatchdogActivity.ForeColor = Color.FromArgb(5, 150, 105); // Green

                lblWatchdogTimer.Text = "Active task execution detected. Watchdog is waiting for work to complete.";
            }
            else
            {
                lblWatchdogActivity.Text = "🟡 IDLE: " + watchdogEngine.MonitoredProcessSummary;
                lblWatchdogActivity.ForeColor = Color.FromArgb(217, 119, 6); // Amber

                int graceSeconds = settings.WatchdogGraceMinutes * 60;
                int remaining = Math.Max(0, graceSeconds - watchdogEngine.ConsecutiveIdleSeconds);
                TimeSpan tIdle = TimeSpan.FromSeconds(watchdogEngine.ConsecutiveIdleSeconds);
                TimeSpan tRem = TimeSpan.FromSeconds(remaining);

                if (settings.WatchdogEnabled)
                {
                    lblWatchdogTimer.Text = string.Format("Idle for: {0:mm\\:ss} / {1:mm\\:ss} buffer. {2} in {3:mm\\:ss}.",
                        tIdle, TimeSpan.FromSeconds(graceSeconds), settings.WatchdogAction, tRem);
                }
                else
                {
                    lblWatchdogTimer.Text = string.Format("Tasks currently idle ({0:mm\\:ss}). Turn ON Watchdog to auto-sleep.", tIdle);
                }
            }
        }

        private void TriggerCountdownAndAction(bool isTest)
        {
            isCountdownActive = true;
            string targetAction = isTest ? "Sleep (Test)" : settings.WatchdogAction;
            int seconds = settings.CountdownSeconds;

            SafetyCountdownForm dlg = new SafetyCountdownForm(targetAction, seconds, settings.PlayAlertSound);
            dlg.ShowDialog(this);

            if (dlg.WasCancelled)
            {
                LogActivity("Countdown CANCELLED by user. PC kept awake.");
                watchdogEngine.ResetIdleTimer();
            }
            else
            {
                if (isTest)
                {
                    LogActivity("Test countdown finished successfully (no real power action taken).");
                    MessageBox.Show("Test countdown completed successfully! In actual mode, your PC would " + settings.WatchdogAction + " now.",
                        "Test Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogActivity(string.Format("Executing {0} now. Goodbye!", settings.WatchdogAction));
                    // Auto turn off watchdog so it doesn't re-trigger immediately upon wakeup
                    settings.WatchdogEnabled = false;
                    settings.Save();
                    UpdateWatchdogToggleUI();

                    PowerManager.ExecuteAction(settings.WatchdogAction);
                }
            }

            isCountdownActive = false;
        }

        private void LogActivity(string message)
        {
            try
            {
                string entry = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);
                if (lstActivityLog != null)
                {
                    lstActivityLog.Items.Add(entry);
                    lstActivityLog.TopIndex = lstActivityLog.Items.Count - 1;
                }
            }
            catch { }
        }
        #endregion

        #region Helpers & System Tray
        private void InitializeTray()
        {
            trayMenu = new ContextMenuStrip();

            ToolStripMenuItem mnuHeader = new ToolStripMenuItem("PowerLockGuard v2.0");
            mnuHeader.Enabled = false;
            trayMenu.Items.Add(mnuHeader);
            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem mnuToggleCharger = new ToolStripMenuItem("Toggle Charger Guard", null, (s, e) =>
            {
                settings.ChargerGuardEnabled = !settings.ChargerGuardEnabled;
                settings.Save();
                UpdateChargerToggleUI();
            });
            trayMenu.Items.Add(mnuToggleCharger);

            ToolStripMenuItem mnuToggleWatchdog = new ToolStripMenuItem("Toggle Work Watchdog", null, (s, e) =>
            {
                settings.WatchdogEnabled = !settings.WatchdogEnabled;
                settings.Save();
                UpdateWatchdogToggleUI();
            });
            trayMenu.Items.Add(mnuToggleWatchdog);

            trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem mnuSleep = new ToolStripMenuItem("Sleep PC Now", null, (s, e) => PowerManager.ExecuteAction("Sleep"));
            trayMenu.Items.Add(mnuSleep);

            ToolStripMenuItem mnuShow = new ToolStripMenuItem("Open PowerLockGuard", null, (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            });
            trayMenu.Items.Add(mnuShow);

            trayMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem mnuExit = new ToolStripMenuItem("Exit", null, (s, e) =>
            {
                trayIcon.Visible = false;
                Application.Exit();
            });
            trayMenu.Items.Add(mnuExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "PowerLockGuard v2.0";

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                try { trayIcon.Icon = new Icon(icoPath); } catch { trayIcon.Icon = SystemIcons.Shield; }
            }
            else
            {
                trayIcon.Icon = SystemIcons.Shield;
            }

            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            };

            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            if (trayIcon == null) return;
            string txt = string.Format("PowerLockGuard | Charger: {0} | Watchdog: {1}",
                settings.ChargerGuardEnabled ? "ON" : "OFF",
                settings.WatchdogEnabled ? "ON" : "OFF");
            if (txt.Length > 63) txt = txt.Substring(0, 63);
            trayIcon.Text = txt;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "PowerLockGuard v2.0",
                    "Running quietly in system tray. Charger Guard & Work Watchdog remain active!", ToolTipIcon.Info);
            }
        }

        private void SetComboSelectedAction(ComboBox cmb, string action)
        {
            if (action == "Shutdown") cmb.SelectedIndex = 1;
            else if (action == "Hibernate") cmb.SelectedIndex = 2;
            else if (action == "Lock" && cmb.Items.Count > 3) cmb.SelectedIndex = 3;
            else cmb.SelectedIndex = 0;
        }

        private string GetActionFromCombo(ComboBox cmb)
        {
            int idx = cmb.SelectedIndex;
            if (idx == 1) return "Shutdown";
            if (idx == 2) return "Hibernate";
            if (idx == 3) return "Lock";
            return "Sleep";
        }

        private void SelectGraceCombo(ComboBox cmb, int minutes)
        {
            if (minutes == 1) cmb.SelectedIndex = 0;
            else if (minutes == 2) cmb.SelectedIndex = 1;
            else if (minutes == 5) cmb.SelectedIndex = 3;
            else if (minutes == 10) cmb.SelectedIndex = 4;
            else cmb.SelectedIndex = 2; // Default 3 min
        }

        private int GetGraceMinutesFromCombo(ComboBox cmb)
        {
            int idx = cmb.SelectedIndex;
            if (idx == 0) return 1;
            if (idx == 1) return 2;
            if (idx == 3) return 5;
            if (idx == 4) return 10;
            return 3;
        }

        private void UpdateStartupShortcut(bool enable)
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, "PowerLockGuard.lnk");

                if (!enable && File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
                else if (enable)
                {
                    string exePath = Application.ExecutablePath;
                    string vbs = string.Format(
                        "Set oWS = WScript.CreateObject(\"WScript.Shell\")\r\n" +
                        "sLinkFile = \"{0}\"\r\n" +
                        "Set oLink = oWS.CreateShortcut(sLinkFile)\r\n" +
                        "oLink.TargetPath = \"{1}\"\r\n" +
                        "oLink.WorkingDirectory = \"{2}\"\r\n" +
                        "oLink.Description = \"PowerLockGuard\"\r\n" +
                        "oLink.Save\r\n",
                        shortcutPath.Replace("\\", "\\\\"),
                        exePath.Replace("\\", "\\\\"),
                        Path.GetDirectoryName(exePath).Replace("\\", "\\\\")
                    );
                    string tempVbs = Path.Combine(Path.GetTempPath(), "create_shortcut.vbs");
                    File.WriteAllText(tempVbs, vbs);
                    Process.Start("wscript.exe", "\"" + tempVbs + "\"");
                }
            }
            catch { }
        }
        #endregion
    }
    #endregion
}
