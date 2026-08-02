using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace KimiWeb;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\KimiWebTrayApp";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _autoStartItem;
    private Forms.ToolStripMenuItem? _stopItem;
    private MainWindow? _mainWindow;
    private bool _syncingAutoStart;

    /// <summary>kimi web 子进程管理服务，全局共享一个实例。</summary>
    public KimiService Service { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：托盘程序只允许一个
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }
        _ownsMutex = true;

        _mainWindow = new MainWindow(Service);
        _mainWindow.AutoStartToggled += enabled => SetAutoStart(enabled);

        BuildTrayIcon();

        Service.StateChanged += () => Dispatcher.BeginInvoke(UpdateTrayState);

        // 应用启动即拉起 kimi web 服务，窗口保持隐藏（双击托盘图标弹出）
        Service.Start();
        _trayIcon?.ShowBalloonTip(3000, "Kimi Web", "服务已在后台运行，双击图标打开主窗口", Forms.ToolTipIcon.Info);
    }

    private void BuildTrayIcon()
    {
        _autoStartItem = new Forms.ToolStripMenuItem("开机自启动") { CheckOnClick = true };
        _autoStartItem.CheckedChanged += (_, _) =>
        {
            if (!_syncingAutoStart)
                SetAutoStart(_autoStartItem.Checked);
        };

        _stopItem = new Forms.ToolStripMenuItem("关闭服务", null, (_, _) => Task.Run(() => Service.Stop()));

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add("打开 Kimi Web", null, (_, _) => OpenKimiWeb());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("重启服务", null, (_, _) => Task.Run(() => Service.Restart()));
        menu.Items.Add(_stopItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = IconHelper.LoadAppIcon(),
            Text = "Kimi Web",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        SyncAutoStartUi(AutoStartHelper.IsEnabled());
        UpdateTrayState();
    }

    /// <summary>勾选框 / 托盘菜单共用的开机自启动设置入口。</summary>
    public void SetAutoStart(bool enabled)
    {
        try
        {
            AutoStartHelper.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            Service.Note($"设置开机自启动失败：{ex.Message}");
        }
        SyncAutoStartUi(enabled);
    }

    private void SyncAutoStartUi(bool enabled)
    {
        _syncingAutoStart = true;
        try
        {
            if (_autoStartItem is not null)
                _autoStartItem.Checked = enabled;
            _mainWindow?.SyncAutoStart(enabled);
        }
        finally
        {
            _syncingAutoStart = false;
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenKimiWeb()
    {
        try
        {
            Service.OpenInBrowser();
        }
        catch (Exception ex)
        {
            Service.Note($"打开浏览器失败：{ex.Message}");
        }
    }

    private void UpdateTrayState()
    {
        bool running = Service.IsRunning;
        if (_trayIcon is not null)
            _trayIcon.Text = running ? "Kimi Web（服务运行中）" : "Kimi Web（服务已停止）";
        if (_stopItem is not null)
            _stopItem.Enabled = running;
    }

    private void ExitApp()
    {
        if (_mainWindow is not null)
            _mainWindow.AllowClose = true;
        try { Service.Stop(); } catch { /* 退出时忽略停止异常 */ }
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
