using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ZakoLauncher.Models;

namespace ZakoLauncher.Services;

public sealed record BranchSwitchResult(bool Success, string Message);

public static class SteamBranchSwitcher
{
    public static async Task<BranchSwitchResult> SwitchToX64Async(GameInstallation installation)
    {
        if (!installation.IsFound || installation.GamePath is null)
            return new(false, "尚未定位 Garry's Mod。");
        if (Process.GetProcessesByName("gmod").Length > 0)
            return new(false, "请先退出正在运行的 Garry's Mod，然后重试。");

        var manifestPath = GmodLocator.FindAppManifestPath(installation.GamePath);
        if (manifestPath is null || !File.Exists(manifestPath))
            return new(false, "未找到 appmanifest_4000.acf，无法自动切换分支。请确认游戏已通过 Steam 正常安装。");

        var steamExe = installation.SteamPath is null ? null : Path.Combine(installation.SteamPath, "steam.exe");
        if (steamExe is null || !File.Exists(steamExe))
            return new(false, "未找到 steam.exe，无法自动重启 Steam。");

        try
        {
            var steamProcesses = Process.GetProcessesByName("steam");
            if (steamProcesses.Length > 0)
            {
                Process.Start(new ProcessStartInfo(steamExe, "-shutdown") { UseShellExecute = true });
                var deadline = DateTime.UtcNow.AddSeconds(25);
                while (DateTime.UtcNow < deadline && Process.GetProcessesByName("steam").Length > 0)
                    await Task.Delay(500);
                if (Process.GetProcessesByName("steam").Length > 0)
                    return new(false, "Steam 未能自动退出，请手动关闭 Steam 后重试。");
            }

            var original = await File.ReadAllTextAsync(manifestPath);
            var updated = SetBetaKey(original, "x86-64");
            var backupPath = manifestPath + ".zako-backup";
            File.Copy(manifestPath, backupPath, true);
            await File.WriteAllTextAsync(manifestPath, updated, new UTF8Encoding(false));
            SettingsService.Log($"已备份并写入 Steam BetaKey=x86-64：{manifestPath}");

            Process.Start(new ProcessStartInfo(steamExe, "-silent") { UseShellExecute = true });
            return new(true, "已切换到 x86-64 分支并重新启动 Steam。请等待 Steam 下载更新完成。");
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, "没有权限修改 Steam 清单。请关闭 Steam，并以管理员身份运行启动器后重试。");
        }
        catch (Exception ex)
        {
            SettingsService.Log($"自动切换 x86-64 失败：{ex.Message}");
            return new(false, $"自动切换失败：{ex.Message}");
        }
    }

    internal static string SetBetaKey(string manifest, string betaKey)
    {
        var betaPattern = new Regex("\"BetaKey\"\\s*\"[^\"]*\"", RegexOptions.IgnoreCase);
        if (betaPattern.IsMatch(manifest))
            return betaPattern.Replace(manifest, $"\"BetaKey\"\t\t\"{betaKey}\"", 1);

        var userConfig = Regex.Match(manifest, "\"UserConfig\"\\s*\\{", RegexOptions.IgnoreCase);
        if (userConfig.Success)
        {
            var openBrace = manifest.IndexOf('{', userConfig.Index);
            var closeBrace = FindMatchingBrace(manifest, openBrace);
            if (closeBrace > openBrace)
                return manifest.Insert(closeBrace, $"\t\t\"BetaKey\"\t\t\"{betaKey}\"{Environment.NewLine}\t");
        }

        var rootClose = manifest.LastIndexOf('}');
        if (rootClose < 0) throw new InvalidDataException("Steam 清单格式无效。");
        var section = $"\t\"UserConfig\"{Environment.NewLine}\t{{{Environment.NewLine}\t\t\"BetaKey\"\t\t\"{betaKey}\"{Environment.NewLine}\t}}{Environment.NewLine}";
        return manifest.Insert(rootClose, section);
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        var depth = 0;
        var inString = false;
        for (var i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }
}
