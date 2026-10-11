using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using ZakoLauncher.Models;

namespace ZakoLauncher.Services;

public static partial class GmodLocator
{
    private const string GameFolderName = "GarrysMod";

    public static GameInstallation Locate(string? manualGamePath = null)
    {
        if (!string.IsNullOrWhiteSpace(manualGamePath) && Directory.Exists(manualGamePath))
        {
            var steamPath = FindSteamParent(manualGamePath) ?? GetSteamPaths().FirstOrDefault();
            return new GameInstallation(manualGamePath, steamPath, HasX64Build(manualGamePath));
        }

        foreach (var steamPath in GetSteamPaths())
        {
            foreach (var library in GetLibraries(steamPath))
            {
                var gamePath = Path.Combine(library, "steamapps", "common", GameFolderName);
                if (!Directory.Exists(gamePath))
                    continue;

                return new GameInstallation(gamePath, steamPath, HasX64Build(gamePath));
            }
        }

        return new GameInstallation(null, GetSteamPaths().FirstOrDefault(), false);
    }

    private static string? FindSteamParent(string gamePath)
    {
        var current = new DirectoryInfo(gamePath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "steam.exe"))) return current.FullName;
            current = current.Parent;
        }
        return null;
    }

    public static bool HasX64Build(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return false;

        // 1. 优先检查磁盘上的 64 位核心物理文件（最可靠判定：只要存在 win64 二进制，即代表已具备 x86-64）
        var physicalCandidates = new[]
        {
            Path.Combine(gamePath, "bin", "win64", "gmod.exe"),
            Path.Combine(gamePath, "bin", "win64", "client.dll"),
            Path.Combine(gamePath, "bin", "win64", "lua_shared.dll"),
            Path.Combine(gamePath, "garrysmod", "bin", "win64", "client.dll")
        };

        if (physicalCandidates.Any(File.Exists))
            return true;

        // 2. 若物理文件尚未下载完成，检查 Steam 清单中的分支设置
        return IsX64BranchSelected(gamePath);
    }

    public static string? FindAppManifestPath(string? gamePath)
    {
        // 尝试从游戏路径推导相对路径 (GarrysMod -> common -> steamapps -> appmanifest_4000.acf)
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            try
            {
                var direct = Path.GetFullPath(Path.Combine(gamePath, "..", "..", "appmanifest_4000.acf"));
                if (File.Exists(direct)) return direct;
            }
            catch { /* 路径非法时忽略 */ }
        }

        // 跨库搜索所有 Steam 库目录
        foreach (var steamPath in GetSteamPaths())
        {
            foreach (var library in GetLibraries(steamPath))
            {
                var candidate = Path.Combine(library, "steamapps", "appmanifest_4000.acf");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    public static bool IsX64BranchSelected(string gamePath)
    {
        try
        {
            var manifestPath = FindAppManifestPath(gamePath);
            if (manifestPath is null || !File.Exists(manifestPath)) return false;
            var manifest = File.ReadAllText(manifestPath);

            // 匹配 BetaKey 值，只要包含 x86-64 / chromium / prerelease / dev 均视为 64 位分支
            var match = Regex.Match(manifest, "\"BetaKey\"\\s*\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                if (value.Contains("x86-64", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("chromium", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("prerelease", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("dev", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> GetSteamPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRegistryPath(paths, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistryPath(paths, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        AddRegistryPath(paths, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFilesX86)) paths.Add(Path.Combine(programFilesX86, "Steam"));
        if (!string.IsNullOrEmpty(programFiles)) paths.Add(Path.Combine(programFiles, "Steam"));

        return paths.Where(Directory.Exists);
    }

    private static void AddRegistryPath(HashSet<string> paths, RegistryKey root, string keyPath, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                paths.Add(value.Replace('/', '\\'));
        }
        catch
        {
            // Registry access may be restricted; the default paths remain available.
        }
    }

    private static IEnumerable<string> GetLibraries(string steamPath)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return libraries;

        try
        {
            var content = File.ReadAllText(vdf);
            foreach (Match match in LibraryPathRegex().Matches(content))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path)) libraries.Add(path);
            }
        }
        catch
        {
            // A locked or malformed VDF should not stop scanning the main library.
        }

        return libraries;
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
