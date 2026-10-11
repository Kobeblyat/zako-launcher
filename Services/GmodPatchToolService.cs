using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ZakoLauncher.Services;

/// <summary>
/// 封装 GMOD 视频播放修复工具（GModPatchTool）的定位与启动。
/// 工具已作为嵌入资源打包进启动器（ZakoLauncher.Tools.GModPatchTool.exe），
/// 运行时自动解压到 %LOCALAPPDATA%\ZakoLauncher\Tools\ 后启动，因此发布仅需一个 EXE。
/// 若嵌入资源缺失，则回退到部署目录 Tools\ 或开发机原始路径。
/// </summary>
public static class GmodPatchToolService
{
    private const string EmbeddedResourceName = "ZakoLauncher.Tools.GModPatchTool.exe";
    private const string BundledFileNameAscii = "GModPatchTool.exe";
    private const string DevMachineOriginalPath = @"D:\gmodpatch\gmodpatchtool_0723\target\release\GMOD修补工具（20260723版）.exe";

    private static readonly string RuntimeToolDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZakoLauncher", "Tools");

    public static readonly string ToolDisplayName = "GMOD 视频播放修复（GModPatchTool）";
    public static readonly string ToolVersion = "20260723";
    public const string HelpUrl = "https://www.bilibili.com/video/BV1EtkgBAEuX";
    public const string ForumUrl = "https://solsticegamestudios.com/fixmedia/";

    /// <summary>
    /// 从嵌入资源解压工具到本地数据目录（带大小校验，避免重复写入），返回解压后的完整路径。
    /// </summary>
    private static string? EnsureExtracted()
    {
        try
        {
            var targetPath = Path.Combine(RuntimeToolDir, BundledFileNameAscii);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);
            if (stream is null) return null;

            var embeddedLength = stream.Length;
            if (File.Exists(targetPath))
            {
                var existing = new FileInfo(targetPath);
                if (existing.Length == embeddedLength) return targetPath;
            }

            Directory.CreateDirectory(RuntimeToolDir);
            using (var fs = File.Create(targetPath))
            {
                stream.CopyTo(fs);
            }

            SettingsService.Log($"已解压视频修复工具到 {targetPath}（{embeddedLength} 字节）");
            return targetPath;
        }
        catch (Exception ex)
        {
            SettingsService.Log($"解压视频修复工具失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 按优先级返回可用工具路径：嵌入资源解压 → 部署目录 Tools → 开发机 D 盘 → 当前目录兜底
    /// </summary>
    public static string? FindToolPath()
    {
        var extracted = EnsureExtracted();
        if (extracted is not null && File.Exists(extracted)) return extracted;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", BundledFileNameAscii),
            Path.Combine(AppContext.BaseDirectory, BundledFileNameAscii),
            Path.Combine(Environment.CurrentDirectory, "Tools", BundledFileNameAscii),
            Path.Combine(Environment.CurrentDirectory, BundledFileNameAscii),
            DevMachineOriginalPath,
        };

        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    public static bool IsAvailable => FindToolPath() != null;

    public static string GetStatusText()
    {
        var path = FindToolPath();
        if (path == null) return "未找到视频修复工具，请重新安装启动器";
        var fi = new FileInfo(path);
        return $"已就绪 · {ToolVersion} · {fi.Length / 1024 / 1024} MB";
    }

    public sealed record LaunchResult(bool Success, string Message, string? ToolPath);

    /// <summary>
    /// 启动修复工具。默认不带参数走内置 GUI；若需要静默可传入自定义参数。
    /// </summary>
    public static LaunchResult Launch(string? extraArgs = null, bool runAsAdmin = false)
    {
        var toolPath = FindToolPath();
        if (toolPath == null)
        {
            var msg = "未找到 GMOD 视频修复工具（嵌入资源缺失）。\n\n" +
                      $"已尝试：\n• 嵌入资源 {EmbeddedResourceName}\n" +
                      $"• {Path.Combine(AppContext.BaseDirectory, "Tools", BundledFileNameAscii)}\n" +
                      $"• {DevMachineOriginalPath}\n\n" +
                      "请确认启动器为完整发布版本，或手动将工具放入 Tools 目录。";
            SettingsService.Log($"视频修复工具启动失败：{msg}");
            return new(false, msg, null);
        }

        // 若游戏正在运行，提醒但仍允许用户继续（PatchTool 自身也会检测）
        if (Process.GetProcessesByName("gmod").Length > 0)
        {
            SettingsService.Log("视频修复工具启动时检测到 Garry's Mod 正在运行");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = extraArgs ?? string.Empty,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(toolPath) ?? AppContext.BaseDirectory,
                Verb = runAsAdmin ? "runas" : null,
            };

            Process.Start(psi);
            SettingsService.Log($"已启动视频修复工具：{toolPath} {extraArgs}");
            return new(true, $"已启动 {ToolDisplayName}。请按工具提示完成 CEF 视频补丁。", toolPath);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 用户在 UAC 取消
            return new(false, "已取消管理员授权。", toolPath);
        }
        catch (Exception ex)
        {
            SettingsService.Log($"视频修复工具启动失败：{ex.Message}");
            return new(false, $"启动失败：{ex.Message}", toolPath);
        }
    }

    /// <summary>
    /// 打开工具所在目录（便于查看日志/缓存）
    /// </summary>
    public static void OpenToolFolder()
    {
        var toolPath = FindToolPath();
        var folder = toolPath != null ? Path.GetDirectoryName(toolPath)! : RuntimeToolDir;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    /// <summary>
    /// 快速检查：当前 GMod 是否已打上视频补丁（粗略基于哈希与 CEF 版本）
    /// 这里仅作 UI 提示，不阻塞用户。
    /// </summary>
    public static string GetPatchHint(Models.GameInstallation installation)
    {
        if (!installation.IsFound) return "请先定位 Garry's Mod";
        if (!installation.IsX64) return "建议先切换到 x86-64，再执行视频修复（补丁对 x86-64 更完整）";
        return "建议执行一次视频修复，可修复游戏内 HTML/视频黑屏、无声、CEF 崩溃等问题";
    }
}