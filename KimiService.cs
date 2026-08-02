using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KimiWeb;

/// <summary>
/// 管理 kimi.exe web 子进程：启动 / 停止 / 重启，捕获输出并转发为日志事件。
/// 启动参数：web --no-open --log-level info（--no-open 由应用自己控制浏览器打开）。
/// </summary>
public sealed class KimiService
{
    public const string ExePath = @"C:\Users\lee\.kimi-code\bin\kimi.exe";
    private const string Arguments = "web --no-open --log-level info";

    /// <summary>kimi web 默认监听地址（--port 默认 58627），日志中解析不到 URL 时使用。</summary>
    public const string FallbackUrl = "http://127.0.0.1:58627";

    private static readonly Regex AnsiRegex = new("\x1B\\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://(?:127\.0\.0\.1|localhost|\[::1\])[^\s\""'<>]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly object _gate = new();
    private Process? _process;

    /// <summary>服务输出（含应用自身提示信息），每行一条。</summary>
    public event Action<string>? LogReceived;

    /// <summary>服务运行状态发生变化（可能在任意线程触发）。</summary>
    public event Action? StateChanged;

    /// <summary>当前服务地址：从服务日志中解析，解析不到则为 <see cref="FallbackUrl"/>。</summary>
    public string CurrentUrl { get; private set; } = FallbackUrl;

    public bool IsRunning
    {
        get { lock (_gate) return _process is { HasExited: false }; }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
                return;

            _process?.Dispose();
            _process = null;

            if (!File.Exists(ExePath))
            {
                Note($"找不到 {ExePath}，请确认 Kimi Code 已安装");
                StateChanged?.Invoke();
                return;
            }

            var psi = new ProcessStartInfo(ExePath, Arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Environment["NO_COLOR"] = "1";

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => HandleLine(e.Data);
            p.ErrorDataReceived += (_, e) => HandleLine(e.Data);
            p.Exited += (_, _) =>
            {
                Note($"kimi web 进程已退出（退出码 {SafeExitCode(p)}）");
                StateChanged?.Invoke();
            };

            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _process = p;
                Note($"已启动：{ExePath} {Arguments}（PID {p.Id}）");
            }
            catch (Exception ex)
            {
                Note($"启动失败：{ex.Message}");
                p.Dispose();
            }
        }
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        Process? p;
        lock (_gate)
            p = _process;

        if (p is null)
            return;

        try
        {
            if (!p.HasExited)
            {
                Note("正在停止 kimi web 服务…");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(10_000);
            }
        }
        catch (Exception ex)
        {
            Note($"停止服务时出错：{ex.Message}");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_process, p))
                    _process = null;
            }
            p.Dispose();
            StateChanged?.Invoke();
        }
    }

    public void Restart()
    {
        Note("重启 kimi web 服务…");
        Stop();
        Start();
    }

    /// <summary>用默认浏览器打开 Kimi Web（携带 #token= 认证片段）。</summary>
    public void OpenInBrowser()
    {
        string url = CurrentUrl;
        if (!url.Contains("#token=", StringComparison.Ordinal))
        {
            // 日志里没解析到带 token 的地址时，从持久化 token 文件补上
            string? token = ReadServerToken();
            if (!string.IsNullOrEmpty(token))
                url = url.TrimEnd('/') + "/#token=" + token;
        }
        Note($"打开 {url}");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>读取 kimi web 的持久化 bearer token（home 目录下的 server.token）。</summary>
    private static string? ReadServerToken()
    {
        try
        {
            string? home = Environment.GetEnvironmentVariable("KIMI_CODE_HOME");
            if (string.IsNullOrEmpty(home))
                home = Path.GetDirectoryName(Path.GetDirectoryName(ExePath)); // bin\kimi.exe 的上一级
            if (string.IsNullOrEmpty(home))
                return null;
            string path = Path.Combine(home, "server.token");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>向日志窗口追加一条应用自身的提示信息。</summary>
    public void Note(string message) => LogReceived?.Invoke($"[KimiWeb] {message}");

    private void HandleLine(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        line = AnsiRegex.Replace(line, string.Empty).TrimEnd('\r');
        if (line.Length == 0)
            return;

        // 从输出中解析服务地址（可能带 token），供“打开 Kimi Web”使用
        Match m = UrlRegex.Match(line);
        if (m.Success)
        {
            string url = m.Value.TrimEnd('.', ',', ')', ';');
            // 优先保留带 token 的地址：后续日志行里的裸地址（如 server ready）不覆盖它
            if (url.Contains("#token=", StringComparison.Ordinal) ||
                !CurrentUrl.Contains("#token=", StringComparison.Ordinal))
                CurrentUrl = url;
        }

        LogReceived?.Invoke(line);
    }

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; } catch { return -1; }
    }
}
