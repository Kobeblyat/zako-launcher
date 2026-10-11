using System.Diagnostics;
using System.IO;
using System.Windows;
using ZakoLauncher.Models;

namespace ZakoLauncher.Services;

public static class GameLauncher
{
    public const string ServerAddress = "171.80.0.187";
    public const string CollectionUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3562943550";
    public const string WebsiteUrl = "https://zako.life";
    public const string SupportUrl = "https://talk.zako.life";

    public static async Task LaunchAsync(GameInstallation installation, LauncherSettings settings)
    {
        var arguments = BuildArguments(settings);
        var steamExe = installation.SteamPath is null ? null : Path.Combine(installation.SteamPath, "steam.exe");

        await Task.Run(() =>
        {
            if (steamExe is not null && File.Exists(steamExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamExe,
                    Arguments = $"-applaunch 4000 {arguments}",
                    UseShellExecute = true
                });
                return;
            }

            OpenUrl($"steam://rungameid/4000//{Uri.EscapeDataString(arguments)}");
        });
    }

    public static void Launch(GameInstallation installation, LauncherSettings settings)
    {
        _ = LaunchAsync(installation, settings);
    }

    public static void OpenCollection() => OpenUrl(CollectionUrl);
    public static void ValidateFiles() => OpenUrl("steam://validate/4000");

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SettingsService.Log($"打开 URL 异常 ({url}): {ex.Message}");
        }
    }

    private static string BuildArguments(LauncherSettings settings)
    {
        var args = new List<string>();

        // 显示模式参数
        if (settings.DisplayMode.Contains("全屏"))
        {
            args.Add("-fullscreen");
        }
        else if (settings.DisplayMode.Contains("窗口化") && !settings.DisplayMode.Contains("无边框"))
        {
            args.Add("-windowed");
        }
        else if (settings.DisplayMode.Contains("无边框"))
        {
            args.AddRange(["-windowed", "-noborder"]);
        }
        // 若为“默认（由游戏内决定）”，则不传入显示模式参数，由 Source 引擎读取游戏内配置

        // 游戏分辨率参数：
        // 1. “默认（由游戏内决定）”：不传入 -w 与 -h，使用 GMod 内置保存的分辨率（切屏最流畅，不影响系统桌面分辨率）
        // 2. “跟随屏幕原生分辨率”：读取物理显示器硬件分辨率（非 DPI 缩放虚拟像素），避免高分屏错位
        // 3. 具体分辨率（如 1920 × 1080 等）：强制传入对应宽高
        if (!string.IsNullOrWhiteSpace(settings.Resolution) && !settings.Resolution.Contains("默认") && !settings.Resolution.Contains("决定"))
        {
            if (settings.Resolution.Contains("跟随") || settings.Resolution.Contains("原生") || settings.Resolution.Contains("屏幕"))
            {
                var (physW, physH) = WindowAppearance.GetPhysicalScreenResolution();
                if (physW > 0 && physH > 0)
                    args.AddRange(["-w", physW.ToString(), "-h", physH.ToString()]);
            }
            else
            {
                var parts = settings.Resolution.Split(['×', 'x', '*', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                {
                    args.AddRange(["-w", width.ToString(), "-h", height.ToString()]);
                }
            }
        }

        if (settings.SkipIntro) args.Add("-novid");
        if (settings.EnableConsole) args.Add("-console");
        if (!args.Contains("-high") && (string.IsNullOrWhiteSpace(settings.CustomArguments) || !settings.CustomArguments.Contains("-high")))
        {
            args.Add("-high"); // 高 CPU 优先级，提升模组解压与加载速度
        }
        if (!string.IsNullOrWhiteSpace(settings.CustomArguments)) args.Add(settings.CustomArguments.Trim());
        
        // 直连服务器选项（勾选时直接连接 Zako 服，不勾选则进入 GMod 游戏主菜单）
        if (settings.DirectConnect)
        {
            args.AddRange(["+connect", ServerAddress]);
        }

        return string.Join(' ', args);
    }
}

