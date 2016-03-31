using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace PowerConfigChanger
{
    public partial class MainWindow : Window
    {
        private static readonly List<string> _shortcutFolder = new List<string>();
        private System.Windows.Forms.NotifyIcon _notifyIcon = null;
        private static readonly Regex RegexGuid = new Regex(@"(?<id>[a-z0-9]{8}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{12})(.*)(?<name>\(.*\))(\s?)(?<current>\*?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static ReadOnlyCollection<PowerConfigType> _initPowerConfig = null; //起動時の状態を退避（起動後に増えたものはホットキー割当の関係から無視する）

        [DllImport("user32.dll")]
        extern static int RegisterHotKey(IntPtr HWnd, int ID, int MOD_KEY, int KEY);
        [DllImport("user32.dll")]
        extern static int UnregisterHotKey(IntPtr HWnd, int ID);

        //https://msdn.microsoft.com/ja-jp/library/windows/desktop/ms646279
        const int MOD_ALT = 0x0001;
        const int MOD_CONTROL = 0x0002;
        const int MOD_SHIFT = 0x0004;
        const int MOD_WIN = 0x0008;
        const int WM_HOTKEY = 0x0312;

        public MainWindow()
        {
            InitializeComponent();

            mainWindow.Title = AppConst.ProgramName + " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.AllowsTransparency = true;
            mainWindow.ShowInTaskbar = false;

            _initPowerConfig = new ReadOnlyCollection<PowerConfigType>(GetPowerConfigInitialDataFromMachine());
            CreateNotifyIcon();

            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += (o, e) => { Visibility = Visibility.Collapsed; };
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            //SourceInitialized後でないとHandleがとれない
            HwndSource source = GetHwndSource();
            source.AddHook(new HwndSourceHook(WndProc));
            foreach (var item in _initPowerConfig)
            {
                RegisterHotKey(source.Handle, item.NO, MOD_SHIFT + MOD_CONTROL, (int)System.Windows.Forms.Keys.D0 + item.NO);
            }
        }

        private HwndSource GetHwndSource()
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            return source;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var config = _initPowerConfig.SingleOrDefault(d => d.NO == (int)wParam);
                if (config != null)
                {
                    SetPowerConfig(config);
                }
            }

            return IntPtr.Zero;
        }

        private void CreateNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = mainWindow.Title;
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/App.ico")).Stream;
            _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = GetContextMenuStrip();
            _notifyIcon.BalloonTipClicked += (o, e) => { _notifyIcon.Visible = true; };
        }

        private System.Windows.Forms.ContextMenuStrip GetContextMenuStrip()
        {
            var contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            foreach (var item in _initPowerConfig)
            {
                var config = new System.Windows.Forms.ToolStripMenuItem();
                config.Text = item.Name;
                config.Click += (o, e) => { SetPowerConfig(item); };
                contextMenuStrip.Items.Add(config);
            }
            contextMenuStrip.Items.Add("-");
            var exitItem = new System.Windows.Forms.ToolStripMenuItem();
            exitItem.Text = "終了";
            exitItem.Click += (o, e) => { ExitApplication(); };
            contextMenuStrip.Items.Add(exitItem);
            return contextMenuStrip;
        }

        private void ShowBalloonTip(string text)
        {
            _notifyIcon.BalloonTipTitle = AppConst.ProgramName;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.ShowBalloonTip(2000);
        }

        private void ExitApplication()
        {
            _notifyIcon.Dispose();
            foreach (var item in _initPowerConfig)
            {
                UnregisterHotKey(GetHwndSource().Handle, item.NO);
            }
            Application.Current.Shutdown();
        }

        private static Process GetPowercfgProcessInstance()
        {
            var p = new Process();
            p.StartInfo.FileName = @"powercfg.exe";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            return p;
        }

        private List<PowerConfigType> GetPowerConfigInitialDataFromMachine()
        {
            var data = new List<PowerConfigType>();

            Process p = GetPowercfgProcessInstance();
            p.StartInfo.Arguments = "-L";
            p.Start();
            var temp1 = p.StandardOutput.ReadToEnd();
            var temp2 = temp1.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in temp2)
            {
                var m = RegexGuid.Match(item);
                if (m.Success)
                {
                    data.Add(new PowerConfigType(data.Count + 1
                        , m.Groups["id"].Value
                        , m.Groups["name"].Value.Replace("(", "").Replace(")", "")
                        , m.Groups["current"].Value.Contains("*"))
                        );
                }
            }
            return data;
        }

        private void SetPowerConfig(PowerConfigType type)
        {
            var p = GetPowercfgProcessInstance();
            p.StartInfo.Arguments = "-SETACTIVE " + type.ID;
            p.Start();

            ShowBalloonTip(type.Name);
        }
    }
}
