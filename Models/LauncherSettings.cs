namespace ZakoLauncher.Models;

public sealed class LauncherSettings
{
    public string? ManualGamePath { get; set; }
    public string DisplayMode { get; set; } = "无边框窗口";
    public string Resolution { get; set; } = "默认（由游戏内决定）";
    public bool SkipIntro { get; set; } = true;
    public bool EnableConsole { get; set; }
    public bool AutoOpenCollection { get; set; }
    public bool AutoMinimizeOnLaunch { get; set; } = true;
    public bool DirectConnect { get; set; } = true;
    public string CustomArguments { get; set; } = string.Empty;
}

