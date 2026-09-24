using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;

internal static class Program
{
    private const string DshUrl = "http://127.0.0.1:3080";
    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string LogDirectory = Path.Combine(BaseDirectory, "logs");
    private static readonly string LogPath = Path.Combine(LogDirectory, "dsh-tray.log");
    private static readonly string OutputPath = Path.Combine(LogDirectory, "dsh-web-output.log");
    private static readonly string NodePath = FindNodePath();
    private static readonly string DshBinPath = FindDshBinPath();
    private static readonly string FishIconPath = Path.Combine(BaseDirectory, "dsh-fish.ico");

    private static NotifyIcon trayIcon;
    private static ContextMenuStrip menu;
    private static ToolStripMenuItem statusItem;
    private static ToolStripMenuItem startItem;
    private static ToolStripMenuItem restartItem;
    private static System.Windows.Forms.Timer statusTimer;
    private static Process ownedDsh;
    private static Mutex singleInstanceMutex;
    private static bool ownsDsh;
    private static string lastError = "";

    [STAThread]
    private static void Main()
    {
        bool createdNew;
        singleInstanceMutex = new Mutex(true, "Local\\TuoguanDSH-Web-Tray", out createdNew);
        if (!createdNew) return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory(LogDirectory);

        menu = new ContextMenuStrip();
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = false;
        menu.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        menu.Renderer = new ToolStripProfessionalRenderer(new TrayColorTable());

        statusItem = new ToolStripMenuItem();
        statusItem.Enabled = false;
        statusItem.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        statusItem.ForeColor = Color.FromArgb(30, 64, 175);
        statusItem.Image = SystemIcons.Information.ToBitmap();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());

        startItem = new ToolStripMenuItem("\u542f\u52a8 DSH \u670d\u52a1");
        startItem.Image = GetFishBitmap();
        startItem.ToolTipText = "\u5728\u540e\u53f0\u542f\u52a8 dsh web\uff0c\u4e0d\u663e\u793a CMD \u7a97\u53e3";
        startItem.Click += delegate { StartDshFromShortcut(false); };
        menu.Items.Add(startItem);

        var openItem = new ToolStripMenuItem("\u6253\u5f00 DSH \u7f51\u9875");
        openItem.Image = SystemIcons.Information.ToBitmap();
        openItem.ToolTipText = "\u5728\u6d4f\u89c8\u5668\u6253\u5f00 http://127.0.0.1:3080";
        openItem.Click += delegate { OpenDshWeb(); };
        menu.Items.Add(openItem);

        restartItem = new ToolStripMenuItem("\u91cd\u542f\u6258\u76d8\u7ba1\u7406\u7684 DSH");
        restartItem.Image = SystemIcons.Warning.ToBitmap();
        restartItem.ToolTipText = "\u53ea\u91cd\u542f\u7531\u672c\u6258\u76d8\u542f\u52a8\u7684 DSH\uff1b\u4e0d\u4f1a\u5f71\u54cd\u5176\u4ed6\u5de5\u4f5c\u6d41";
        restartItem.Click += delegate { RestartDsh(); };
        menu.Items.Add(restartItem);

        menu.Items.Add(new ToolStripSeparator());

        var logItem = new ToolStripMenuItem("\u67e5\u770b\u8fd0\u884c\u65e5\u5fd7");
        logItem.Image = SystemIcons.Question.ToBitmap();
        logItem.ToolTipText = "\u6253\u5f00 DSH \u6258\u76d8\u7a0b\u5e8f\u548c DSH \u8f93\u51fa\u65e5\u5fd7";
        logItem.Click += delegate { OpenLog(); };
        menu.Items.Add(logItem);

        var exitItem = new ToolStripMenuItem("\u5b8c\u5168\u9000\u51fa\u6258\u76d8\uff08\u505c\u6b62\u672c\u6258\u76d8\u542f\u52a8\u7684 DSH\uff09");
        exitItem.Image = SystemIcons.Error.ToBitmap();
        exitItem.ForeColor = Color.FromArgb(185, 28, 28);
        exitItem.ToolTipText = "\u5173\u95ed\u6258\u76d8\u56fe\u6807\uff0c\u53ea\u505c\u6b62\u672c\u6258\u76d8\u81ea\u5df1\u542f\u52a8\u7684 DSH";
        exitItem.Click += delegate { ExitApplication(); };
        menu.Items.Add(exitItem);

        trayIcon = new NotifyIcon();
        trayIcon.Icon = File.Exists(FishIconPath) ? new Icon(FishIconPath) : SystemIcons.Application;
        trayIcon.Text = "DSH \u6258\u76d8\uff1a\u6b63\u5728\u542f\u52a8";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += delegate { OpenDshWeb(); };

        // The user starts this EXE explicitly by clicking the .lnk shortcut.
        // No scheduled task, service, or startup-folder entry is used.
        statusTimer = new System.Windows.Forms.Timer();
        statusTimer.Interval = 2000;
        statusTimer.Tick += delegate { RefreshStatus(); };

        StartDshFromShortcut(true);
        statusTimer.Start();
        trayIcon.ShowBalloonTip(3000, "DSH \u6258\u76d8\u5df2\u542f\u52a8", "DSH \u5df2\u5728\u540e\u53f0\u8fd0\u884c\u3002\u53f3\u952e\u56fe\u6807\u53ef\u67e5\u770b\u72b6\u6001\u6216\u5b8c\u5168\u9000\u51fa\u3002", ToolTipIcon.Info);
        Application.Run();

        statusTimer.Stop();
        StopOwnedDsh();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        singleInstanceMutex.ReleaseMutex();
        singleInstanceMutex.Dispose();
    }

    // Invoked when the user double-clicks the manual .lnk, or explicitly clicks the menu command.
    private static void StartDshFromShortcut(bool launchTriggered)
    {
        try
        {
            DisposeExitedOwnedProcess();
            if (!IsPortOpen())
            {
                StartOwnedDsh();
                if (!launchTriggered)
                {
                    trayIcon.ShowBalloonTip(2500, "DSH \u6b63\u5728\u542f\u52a8", "DSH \u6b63\u7531\u6258\u76d8\u5728\u540e\u53f0\u542f\u52a8\u3002", ToolTipIcon.Info);
                }
            }
            else if (!ownsDsh && !launchTriggered)
            {
                trayIcon.ShowBalloonTip(2500, "DSH \u5df2\u5728\u8fd0\u884c", "\u68c0\u6d4b\u5230 3080 \u7aef\u53e3\u5df2\u6709 DSH\uff0c\u672a\u5bf9\u5b83\u505a\u4efb\u4f55\u6539\u52a8\u3002", ToolTipIcon.Info);
            }
            lastError = "";
        }
        catch (Exception exception)
        {
            lastError = exception.Message;
            Log("Startup error: " + lastError);
            trayIcon.ShowBalloonTip(5000, "DSH \u542f\u52a8\u5931\u8d25", lastError, ToolTipIcon.Error);
        }
        RefreshStatus();
    }

    private static void StartOwnedDsh()
    {
        if (IsPortOpen()) return;
        if (ownedDsh != null && !ownedDsh.HasExited) return;
        if (!File.Exists(NodePath)) throw new FileNotFoundException("node.exe was not found", NodePath);
        if (!File.Exists(DshBinPath)) throw new FileNotFoundException("DSH entry file was not found", DshBinPath);

        var startInfo = new ProcessStartInfo();
        startInfo.FileName = NodePath;
        startInfo.Arguments = "\"" + DshBinPath + "\" web";
        startInfo.WorkingDirectory = BaseDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        ownedDsh = new Process();
        ownedDsh.StartInfo = startInfo;
        ownedDsh.EnableRaisingEvents = true;
        ownedDsh.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args) { if (!String.IsNullOrEmpty(args.Data)) AppendOutput(args.Data); };
        ownedDsh.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args) { if (!String.IsNullOrEmpty(args.Data)) AppendOutput(args.Data); };
        ownedDsh.Start();
        ownedDsh.BeginOutputReadLine();
        ownedDsh.BeginErrorReadLine();
        ownsDsh = true;
        Log("Started tray-owned DSH directly with node.exe. PID=" + ownedDsh.Id);
    }

    private static void StopOwnedDsh()
    {
        if (!ownsDsh || ownedDsh == null) return;
        try
        {
            if (!ownedDsh.HasExited)
            {
                ownedDsh.Kill();
                ownedDsh.WaitForExit(5000);
                Log("Stopped tray-owned DSH. PID=" + ownedDsh.Id);
            }
        }
        catch (Exception exception)
        {
            Log("Stop error: " + exception.Message);
        }
        finally
        {
            ownedDsh.Dispose();
            ownedDsh = null;
            ownsDsh = false;
        }
    }

    private static void RestartDsh()
    {
        if (!ownsDsh)
        {
            MessageBox.Show("\u5f53\u524d\u6ca1\u6709\u7531\u672c\u6258\u76d8\u542f\u52a8\u7684 DSH\u3002\n\n\u5982\u679c 3080 \u7aef\u53e3\u5df2\u7ecf\u88ab\u5176\u4ed6 DSH \u5360\u7528\uff0c\u4e3a\u4e86\u4e0d\u5f71\u54cd\u5176\u4ed6\u5de5\u4f5c\u6d41\uff0c\u672c\u6258\u76d8\u4e0d\u4f1a\u505c\u6b62\u6216\u91cd\u542f\u5b83\u3002", "DSH \u6258\u76d8", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        StopOwnedDsh();
        Thread.Sleep(600);
        StartDshFromShortcut(false);
    }

    private static void DisposeExitedOwnedProcess()
    {
        if (ownedDsh != null && ownedDsh.HasExited)
        {
            Log("Tray-owned DSH exited. Code=" + ownedDsh.ExitCode);
            ownedDsh.Dispose();
            ownedDsh = null;
            ownsDsh = false;
        }
    }

    private static void RefreshStatus()
    {
        DisposeExitedOwnedProcess();
        if (IsPortOpen())
        {
            statusItem.Text = ownsDsh ? "\u72b6\u6001\uff1aDSH \u6b63\u5728\u8fd0\u884c\uff08\u7531\u672c\u6258\u76d8\u7ba1\u7406\uff09" : "\u72b6\u6001\uff1a\u68c0\u6d4b\u5230\u5176\u4ed6\u5b9e\u4f8b\u6b63\u5728\u8fd0\u884c";
            statusItem.Image = ownsDsh ? GetFishBitmap() : SystemIcons.Information.ToBitmap();
            statusItem.ForeColor = ownsDsh ? Color.FromArgb(22, 101, 52) : Color.FromArgb(30, 64, 175);
            startItem.Enabled = !ownsDsh;
            restartItem.Enabled = ownsDsh;
            trayIcon.Text = ownsDsh ? "DSH \u6258\u76d8\uff1a\u6b63\u5728\u8fd0\u884c" : "DSH \u6258\u76d8\uff1a\u68c0\u6d4b\u5230\u5176\u4ed6\u5b9e\u4f8b";
        }
        else if (!String.IsNullOrEmpty(lastError))
        {
            statusItem.Text = "\u72b6\u6001\uff1a\u542f\u52a8\u5931\u8d25\uff08\u53ef\u67e5\u770b\u8fd0\u884c\u65e5\u5fd7\uff09";
            statusItem.Image = SystemIcons.Error.ToBitmap();
            statusItem.ForeColor = Color.FromArgb(185, 28, 28);
            startItem.Enabled = true;
            restartItem.Enabled = false;
            trayIcon.Text = "DSH \u6258\u76d8\uff1a\u542f\u52a8\u5931\u8d25";
        }
        else
        {
            statusItem.Text = "\u72b6\u6001\uff1aDSH \u672a\u542f\u52a8";
            statusItem.Image = SystemIcons.Warning.ToBitmap();
            statusItem.ForeColor = Color.FromArgb(146, 64, 14);
            startItem.Enabled = true;
            restartItem.Enabled = false;
            trayIcon.Text = "DSH \u6258\u76d8\uff1aDSH \u672a\u542f\u52a8";
        }
    }

    private static bool IsPortOpen()
    {
        try
        {
            using (var client = new TcpClient())
            {
                var result = client.BeginConnect("127.0.0.1", 3080, null, null);
                if (!result.AsyncWaitHandle.WaitOne(300)) return false;
                client.EndConnect(result);
                return true;
            }
        }
        catch { return false; }
    }

    private static string FindNodePath()
    {
        var candidates = new List<string>();
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!String.IsNullOrEmpty(programFiles)) candidates.Add(Path.Combine(programFiles, "nodejs", "node.exe"));
        if (!String.IsNullOrEmpty(programFilesX86)) candidates.Add(Path.Combine(programFilesX86, "nodejs", "node.exe"));

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string directory in pathValue.Split(Path.PathSeparator))
        {
            if (!String.IsNullOrWhiteSpace(directory)) candidates.Add(Path.Combine(directory.Trim(), "node.exe"));
        }

        foreach (string candidate in candidates)
        {
            try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); }
            catch { }
        }
        return Path.Combine(programFiles, "nodejs", "node.exe");
    }

    private static string FindDshBinPath()
    {
        var candidates = new List<string>();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!String.IsNullOrEmpty(appData))
        {
            candidates.Add(Path.Combine(appData, "npm", "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js"));
        }

        string prefix = Environment.GetEnvironmentVariable("npm_config_prefix") ?? "";
        if (!String.IsNullOrEmpty(prefix))
        {
            candidates.Add(Path.Combine(prefix, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js"));
        }

        foreach (string candidate in candidates)
        {
            try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); }
            catch { }
        }
        return candidates.Count > 0 ? candidates[0] : "";
    }

    private static Bitmap GetFishBitmap()
    {
        try
        {
            if (File.Exists(FishIconPath))
            {
                using (var icon = new Icon(FishIconPath, new Size(16, 16)))
                {
                    return icon.ToBitmap();
                }
            }
        }
        catch { }
        return SystemIcons.Application.ToBitmap();
    }

    private static void OpenDshWeb()
    {
        Process.Start(new ProcessStartInfo(DshUrl) { UseShellExecute = true });
    }

    private static void OpenLog()
    {
        if (!File.Exists(LogPath)) File.WriteAllText(LogPath, "DSH tray is ready." + Environment.NewLine);
        Process.Start(new ProcessStartInfo("notepad.exe", "\"" + LogPath + "\"") { UseShellExecute = true });
    }

    private static void ExitApplication()
    {
        statusTimer.Stop();
        StopOwnedDsh();
        trayIcon.Visible = false;
        Application.Exit();
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine);
        }
        catch { }
    }

    private static void AppendOutput(string line)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(OutputPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + line + Environment.NewLine);
        }
        catch { }
    }

    private sealed class TrayColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.White; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(239, 246, 255); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(239, 246, 255); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(219, 234, 254); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(147, 197, 253); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(248, 250, 252); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(248, 250, 252); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(248, 250, 252); } }
        public override Color SeparatorDark { get { return Color.FromArgb(226, 232, 240); } }
        public override Color SeparatorLight { get { return Color.White; } }
    }
}
