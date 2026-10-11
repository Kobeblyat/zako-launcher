using System.IO;
using System.Text.Json;
using ZakoLauncher.Models;

namespace ZakoLauncher.Services;

public static class SettingsService
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZakoLauncher");
    private static readonly string SettingsPath = Path.Combine(DataFolder, "settings.json");
    public static readonly string LogPath = Path.Combine(DataFolder, "launcher.log");

    public static LauncherSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath)) ?? new LauncherSettings()
                : new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        Log("设置已保存");
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent the launcher from working.
        }
    }

    public static string ReadLog()
    {
        try { return File.Exists(LogPath) ? File.ReadAllText(LogPath) : "暂时没有日志。"; }
        catch (Exception ex) { return $"读取日志失败：{ex.Message}"; }
    }
}
