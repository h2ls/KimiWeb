using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace KimiWeb;

public partial class MainWindow : FluentWindow
{
    private static readonly SolidColorBrush RunningBrush = new(System.Windows.Media.Color.FromRgb(0x3F, 0xB9, 0x50));
    private static readonly SolidColorBrush StoppedBrush = new(System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49));

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
        StatusText.Text = running ? "服务运行中" : "服务已停止";
        StatusDot.Fill = running ? RunningBrush : StoppedBrush;
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
        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Title = "确认退出",
            Content = "将关闭 kimi web 服务并退出 Kimi Web，是否继续？",
            PrimaryButtonText = "关闭并退出",
            CloseButtonText = "取消",
            Owner = this,
        };

        if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        StopButton.IsEnabled = false;
        ((App)System.Windows.Application.Current).ExitApp();
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
