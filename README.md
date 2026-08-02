# KimiWeb

Kimi Web 的 Windows 托盘控制台：一个基于 WPF 的桌面小程序，用于一键启动、管理和访问 [Kimi Code](https://www.kimi.com/) 的本地 Web 服务（`kimi.exe web`）。

## 功能特性

- **一键管理服务**：启动、重启、停止 `kimi.exe web --no-open --log-level info` 子进程，停止时连同整棵进程树一起终止
- **实时日志窗口**：捕获服务 stdout/stderr 输出（自动剥离 ANSI 颜色码），在服务台中滚动显示
- **一键打开浏览器**：从服务日志中解析带 `#token=` 的认证地址；解析不到时回退读取 `server.token` 持久化文件拼接认证链接
- **系统托盘常驻**：最小化到托盘运行，双击图标弹出主窗口，右键菜单提供全部常用操作
- **开机自启动**：通过写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 注册表项实现，无需管理员权限
- **单实例运行**：通过命名 Mutex 保证只运行一个托盘实例
- **状态指示**：窗口底部与托盘提示实时显示服务运行状态与访问地址

## 运行截图

![Kimi Web 控制台](kimi%20web.png)

## 系统要求

- Windows 7 及以上（x64）
- **必须先安装 Kimi Code** 才能运行本程序。在 PowerShell 中执行以下命令安装：

  ```powershell
  # Windows (PowerShell)
  irm https://code.kimi.com/kimi-code/install.ps1 | iex
  ```

  安装后默认路径为 `C:\Users\<用户名>\.kimi-code\bin\kimi.exe`。
  当前版本的可执行文件路径硬编码在 `KimiService.cs` 的 `ExePath` 常量中，如路径不同请修改后重新编译

## 构建与发布

项目基于 **.NET 11（net11.0-windows7.0）**，使用 WPF + Windows Forms（托盘图标），UI 库为 [WPF-UI](https://github.com/lepoco/wpfui) 4.3。

```powershell
# 开发调试
dotnet build

# 发布单文件自包含 exe（win-x64，ReadyToRun）
dotnet publish -c Release
```

发布后产物为单个 `KimiWeb.exe`，无需安装 .NET 运行时即可运行。

## 使用说明

1. 运行 `KimiWeb.exe`，程序自动启动 kimi web 服务并驻留系统托盘
2. **双击托盘图标**打开主窗口，查看服务日志与运行状态
3. 点击主窗口的 **Kimi Web** 按钮（或托盘菜单"打开 Kimi Web"）在默认浏览器中打开带认证的 Web 界面
4. **重启服务** / **关闭服务**按钮控制子进程生命周期
5. 勾选 **开机自启动** 后，登录 Windows 时自动运行并启动服务
6. 托盘菜单选择 **退出** 会先停止服务再退出程序

## 项目结构

| 文件 | 说明 |
| --- | --- |
| `App.xaml.cs` | 应用入口：单实例 Mutex、托盘图标与菜单、服务生命周期协调 |
| `MainWindow.xaml(.cs)` | 主窗口：日志显示、操作按钮、状态栏 |
| `KimiService.cs` | kimi web 子进程管理：启动/停止/重启、输出捕获、URL 与 token 解析 |
| `AutoStartHelper.cs` | 注册表开机自启动的读写 |
| `IconHelper.cs` | 应用图标加载 |
| `KimiWeb.csproj` | 项目文件：net11.0-windows、WPF-UI、单文件发布配置 |

## 许可证

见 [LICENSE.txt](LICENSE.txt)。
