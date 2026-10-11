namespace ZakoLauncher.Models;

public sealed record GameInstallation(string? GamePath, string? SteamPath, bool IsX64)
{
    public bool IsFound => !string.IsNullOrWhiteSpace(GamePath);
}
