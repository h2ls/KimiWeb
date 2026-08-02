using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KimiWeb;

public partial class MainWindow : Window
{
    private const int MaxLogChars = 300_000;

    private readonly KimiService _service;
    private bool _syncingAutoStart;

    /// <summary>托盘菜单“退出”时置为 true，窗口才真正关闭；否则关闭按钮只是隐藏到托盘。</summary>
    public bool AllowClose { get; set; }

    public event Action<bool>? AutoStartToggled;

    public MainWindow(KimiService service)
    {
        InitializeComponent();
        _service = service;

        try { Icon = new BitmapImage(new Uri("pack://application:,,,/app.ico")); } catch { /* 图标缺失不影响功能 */ }

        _service.LogReceived += OnServiceLog;
        _service.StateChanged += () => Dispatcher.BeginInvoke(UpdateStatus);

        Loaded += (_, _) =>
        {
            SyncAutoStart(AutoStartHelper.IsEnabled());
            UpdateStatus();
        };
    }

    /// <summary>由 App 在托盘菜单勾选变化时同步勾选框，不触发事件回环。</summary>
    public void SyncAutoStart(bool enabled)
    {
        _syncingAutoStart = true;
        AutoStartBox.IsChecked = enabled;
        _syncingAutoStart = false;
    }

    private void OnServiceLog(string line)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // 限制日志长度，避免长时间运行无限增长
            if (LogBox.Text.Length > MaxLogChars)
                LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - MaxLogChars / 2);
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
            UrlText.Text = "地址：" + _service.CurrentUrl;
        });
    }

    private void UpdateStatus()
    {
        bool running = _service.IsRunning;
        StatusText.Text = running ? "服务状态：运行中" : "服务状态：已停止";
        StopButton.IsEnabled = running;
        UrlText.Text = "地址：" + _service.CurrentUrl;
    }

    private void OpenWeb_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _service.OpenInBrowser();
        }
        catch (Exception ex)
        {
            _service.Note($"打开浏览器失败：{ex.Message}");
        }
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        RestartButton.IsEnabled = false;
        try { await Task.Run(() => _service.Restart()); }
        finally { RestartButton.IsEnabled = true; }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        try { await Task.Run(() => _service.Stop()); }
        finally { StopButton.IsEnabled = true; }
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingAutoStart)
            return;
        AutoStartToggled?.Invoke(AutoStartBox.IsChecked == true);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            // 常驻托盘：关闭按钮只隐藏窗口
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
