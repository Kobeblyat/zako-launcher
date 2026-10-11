using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZakoLauncher.Models;
using ZakoLauncher.Services;

namespace ZakoLauncher;

public partial class MainWindow : Window
{
    private GameInstallation _installation = new(null, null, false);
    private LauncherSettings _settings;
    private readonly DispatcherTimer _toastTimer;
    private string? _articleActionUrl;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowForScreen();
        SourceInitialized += (_, _) => WindowAppearance.ApplyRoundedCorners(this);
        _settings = SettingsService.Load();
        LoadSettingsIntoControls();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _toastTimer.Tick += (_, _) => HideToast();
        Loaded += async (_, _) => { SettingsService.Log("启动器已打开"); await ScanAsync(true); };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                _toastTimer.Stop();
                WindowAppearance.OptimizeBackgroundResources(true);
            }
            else
            {
                WindowAppearance.OptimizeBackgroundResources(false);
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (X64PromoOverlay.Visibility == Visibility.Visible) X64PromoOverlay.Visibility = Visibility.Collapsed;
            else if (ArticleOverlay.Visibility == Visibility.Visible) ArticleOverlay.Visibility = Visibility.Collapsed;
            else if (SettingsOverlay.Visibility == Visibility.Visible) SettingsOverlay.Visibility = Visibility.Collapsed;
            else if (QuickMenuPopup.IsOpen) QuickMenuPopup.IsOpen = false;
        };
    }

    private void ConfigureWindowForScreen()
    {
        var work = SystemParameters.WorkArea;
        var width = Math.Min(1440d, work.Width * 0.92);
        var height = width * 9d / 16d;
        var maxHeight = work.Height * 0.90;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * 16d / 9d;
        }
        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
        MaxWidth = work.Width;
        MaxHeight = work.Height;
    }

    private async Task ScanAsync(bool notify)
    {
        SetStatus("正在检测游戏…", "#FFD52F");
        LaunchButton.IsEnabled = false;
        SettingsService.Log("开始扫描 Garry's Mod");
        _installation = await Task.Run(() => GmodLocator.Locate(_settings.ManualGamePath));
        LaunchButton.IsEnabled = true;
        UpdateGameInfo();

        if (!_installation.IsFound)
        {
            SetStatus("未找到 Garry's Mod", "#FF6B6B");
            SettingsService.Log("扫描结果：未找到游戏");
            if (notify) ShowToast("没有找到游戏", "请确认 Steam 与 Garry's Mod 已安装，或在游戏设置中手动重新定位。", false);
            return;
        }
        if (!_installation.IsX64)
        {
            SetStatus("已找到 · 当前非 x86-64", "#FFB84D");
            SettingsService.Log($"扫描结果：{_installation.GamePath}，未检测到 x86-64");
            if (notify)
            {
                ShowToast("建议更换为 x86-64 版本", "已找到游戏，但没有检测到 win64 文件。x86-64 可突破 4GB 内存、修复视频黑屏并更稳定，推荐一键切换。", false, "查看好处 · 自动切换", "switch_x64");
                ShowX64Promo();
            }
            return;
        }
        SetStatus("已就绪 · x86-64", "#73E39A");
        SettingsService.Log($"扫描结果：{_installation.GamePath}，x86-64 已就绪");
        if (notify) ShowToast("检测完成", "Garry's Mod x86-64 已就绪，点击“开始游戏”即可进入 Zako 服。需要时可执行“GMOD视频播放修复”。", true);
    }

    private void UpdateGameInfo()
    {
        InstallPathText.Text = _installation.GamePath ?? "尚未定位游戏";
        ArchitectureText.Text = !_installation.IsFound ? "未安装" : _installation.IsX64 ? "x86-64 已就绪" : "需要切换 x86-64";
        ArchitectureText.Foreground = BrushOf(_installation.IsX64 ? "#73E39A" : "#FFB84D");
        ArchitectureSwitchButton.Visibility = _installation.IsFound && !_installation.IsX64 ? Visibility.Visible : Visibility.Collapsed;
        if (_installation.GamePath is not null)
        {
            try
            {
                var created = Directory.GetCreationTime(_installation.GamePath);
                InstallDateText.Text = $"检测时间：{DateTime.Now:yyyy-MM-dd HH:mm}  ·  目录创建：{created:yyyy-MM-dd}";
            }
            catch { InstallDateText.Text = $"检测时间：{DateTime.Now:yyyy-MM-dd HH:mm}"; }
        }
        else InstallDateText.Text = "安装信息：未检测到 Garry's Mod";

        // 同步更新视频修复与 x64 弹窗状态文本
        try
        {
            VideoFixStatusText.Text = GmodPatchToolService.IsAvailable ? $"{GmodPatchToolService.ToolVersion} · {GmodPatchToolService.GetPatchHint(_installation)}" : "未找到工具";
            X64PromoStatusText.Text = !_installation.IsFound ? "未检测到 Garry's Mod，请先在 Steam 安装并在设置中重新定位。"
                : _installation.IsX64 ? $"已是 x86-64 · 路径：{_installation.GamePath}\nAR 提示：若仍有视频黑屏/无声，建议执行“GMOD视频播放修复”以更新 CEF 到 137。"
                : $"当前为 32 位分支 · 路径：{_installation.GamePath}\n点击“立即自动切换”将备份清单并切换到 x86-64，Steam 会自动下载增量更新。";
        }
        catch { /* 状态文本更新不应影响主流程 */ }
    }

    private void LoadSettingsIntoControls()
    {
        SelectComboItem(DisplayModeCombo, _settings.DisplayMode);
        SelectComboItem(ResolutionCombo, _settings.Resolution);
        SkipIntroCheck.IsChecked = _settings.SkipIntro;
        ConsoleCheck.IsChecked = _settings.EnableConsole;
        AutoCollectionCheck.IsChecked = _settings.AutoOpenCollection;
        AutoMinimizeCheck.IsChecked = _settings.AutoMinimizeOnLaunch;
        DirectConnectCheck.IsChecked = _settings.DirectConnect;
        CustomArgumentsText.Text = _settings.CustomArguments;
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return;
        }

        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            var content = item.Content?.ToString() ?? string.Empty;
            if (content == value || content.StartsWith(value, StringComparison.OrdinalIgnoreCase) || value.StartsWith(content, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string SelectedComboText(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    private static Brush BrushOf(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private void SetStatus(string text, string color) { StatusText.Text = text; StatusDot.Fill = BrushOf(color); }

    private void ShowToast(string title, string message, bool success, string? actionText = null, string? action = null)
    {
        ToastTitle.Text = title; ToastMessage.Text = message; ToastIcon.Text = success ? "✓" : "!";
        ToastActionButton.Content = actionText ?? string.Empty;
        ToastActionButton.Tag = action;
        ToastActionButton.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop(); _toastTimer.Start();
    }

    private void ShowX64Promo()
    {
        UpdateGameInfo();
        X64PromoOverlay.Visibility = Visibility.Visible;
        SettingsService.Log("已弹出 x86-64 好处介绍弹窗");
    }

    private void HideToast() { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; }

    private async void ToastActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToastActionButton.Tag?.ToString() == "switch_x64")
        {
            HideToast();
            ShowX64Promo();
            await SwitchToX64Async();
        }
    }

    private void CloseX64PromoButton_Click(object sender, RoutedEventArgs e) => X64PromoOverlay.Visibility = Visibility.Collapsed;
    private async void X64PromoSwitchButton_Click(object sender, RoutedEventArgs e) => await SwitchToX64Async();
    private void X64PromoVideoFixButton_Click(object sender, RoutedEventArgs e)
    {
        X64PromoOverlay.Visibility = Visibility.Collapsed;
        RunVideoFix();
    }

    private async void ArchitectureSwitchButton_Click(object sender, RoutedEventArgs e) => await SwitchToX64Async();

    private async Task SwitchToX64Async()
    {
        ToastActionButton.IsEnabled = false;
        ArchitectureSwitchButton.IsEnabled = false;
        ShowToast("正在自动切换", "启动器正在安全退出 Steam、备份清单并切换到 x86-64，请稍候…", true);
        var result = await SteamBranchSwitcher.SwitchToX64Async(_installation);
        ToastActionButton.IsEnabled = true;
        ArchitectureSwitchButton.IsEnabled = true;

        if (result.Success)
        {
            _installation = await Task.Run(() => GmodLocator.Locate(_settings.ManualGamePath));
            UpdateGameInfo();
            ArchitectureSwitchButton.Visibility = Visibility.Collapsed;
            X64PromoOverlay.Visibility = Visibility.Collapsed;
            ShowToast("切换成功", result.Message + " 建议随后执行“GMOD视频播放修复”以获得完整 CEF 支持。", true);
            ShowArticle(
                "切换成功 · 接下来建议修复视频",
                "已成功切换到 x86-64 分支并重启 Steam，请等待 Steam 完成下载更新。\n\n" +
                "切换完成后，强烈建议立即执行本启动器集成的“GMOD视频播放修复”（GModPatchTool 20260723）：\n" +
                "• CEF 升级到 137.0.19（Chromium 137.0.7151.121）\n" +
                "• 支持 H.264/AAC 私有编解码、Widevine、WebGL 与 GPU 加速\n" +
                "• 修复游戏内 HTML/视频黑屏、无声、CEF 崩溃与贴图加载慢\n\n" +
                "顺序：等待 Steam 更新结束 → 点击启动器“🎬 视频播放修复”→ 按工具提示完成 → 重启 GMod。",
                null, null);
        }
        else
        {
            ShowToast("自动切换失败", result.Message, false, "重试", "switch_x64");
        }
    }

    private void RunVideoFix()
    {
        if (!_installation.IsFound)
        {
            ShowToast("请先定位游戏", "未检测到 Garry's Mod，无法确定修复目标。", false);
            return;
        }
        var result = GmodPatchToolService.Launch();
        if (result.Success)
        {
            ShowToast("已启动视频修复工具", result.Message, true);
        }
        else
        {
            ShowToast("无法启动视频修复工具", result.Message, false);
            // 同时弹出说明，便于用户理解工具用途
            VideoFixHelpButton_Click(this, new RoutedEventArgs());
        }
    }

    private void VideoFixButton_Click(object sender, RoutedEventArgs e) => RunVideoFix();
    private void VideoFixHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var body =
            "“GMOD视频播放修复”由 Zako 御用 GModPatchTool（20260723版）提供，已集成到启动器 Tools 目录，无需单独下载。\n\n" +
            "【它能修什么】\n" +
            "• 游戏内网页/视频黑屏、转圈、白屏、崩溃（CEF 过旧）\n" +
            "• MP4/H.264/AAC 无声或无法播放，B 站/YouTube 抽风\n" +
            "• 材质/模型预览、服务器 MOTD、商店页面加载缓慢\n" +
            "• 部分地图/插件的 HTML 菜单卡死\n\n" +
            "【技术亮点】\n" +
            "• CEF 升级到 137.0.19（Chromium 137.0.7151.121）\n" +
            "• 开启 H.264/AAC 专有编解码、Widevine、WebGL、GPU 加速、Site Isolation\n" +
            "• 改善纹理更新性能，禁用硬件媒体键劫持\n" +
            "• 清理 ChromiumCache/ChromiumCacheMultirun/chromium.log 缓存\n" +
            "• x86-64 分支下完整；与 x86-64 配合效果最佳（更大内存、更稳）\n\n" +
            "【如何使用】\n" +
            "1. 关闭 Garry's Mod 与 Steam 下载队列（避免占用文件）\n" +
            "2. 在启动器点“🎬 视频播放修复”或“GMOD视频播放修复”\n" +
            "3. 在弹出的工具窗口按提示执行，等待下载与修补完成\n" +
            "4. 完成后重启 Steam 与 GMod 即可\n\n" +
            "【常见问题】\n" +
            "• 提示管理员权限：请关闭 UAC 或按工具提示操作；工具默认不建议以管理员直接运行 GMod\n" +
            "• 工具闪退/下载失败：检查网络或尝试在工具参数加 --no-system-proxy，必要时开代理/VPN\n" +
            "• 仍黑屏：确认已切换到 x86-64，并在 GMod 启动项移除 -nochromium（若有）\n\n" +
            "来源：solsticegamestudios/GModPatchTool，由 Kobeblyat 汉化并提供国内镜像加速。视频教程：https://www.bilibili.com/video/BV1EtkgBAEuX";
        ShowArticle("GMOD 视频播放修复 · 说明", body, GmodPatchToolService.HelpUrl, "查看视频教程");
    }

    private void OpenVideoFixFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try { GmodPatchToolService.OpenToolFolder(); }
        catch (Exception ex) { ShowToast("无法打开目录", ex.Message, false); }
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (LaunchButton.IsEnabled == false) return;

        try
        {
            if (!_installation.IsFound)
            {
                await ScanAsync(false);
                if (!_installation.IsFound) { ShowToast("暂时无法启动", "没有找到 Garry's Mod，请在游戏设置中重新定位。", false); return; }
            }
            if (!_installation.IsX64)
            {
                ShowToast("当前不是 x86-64", "游戏仍会启动，但强烈建议先切换到 x86-64：突破 4GB、视频修复更完整、更稳定。", false, "查看好处 · 自动切换", "switch_x64");
                ShowX64Promo();
            }

            LaunchButton.IsEnabled = false;
            var launchMsg = _settings.DirectConnect
                ? "正在连接 Zako 服…加载模组若提示“未响应”请点“等待响应”或勿点屏幕，稍候即可进入。"
                : "正在通过 Steam 启动 Garry's Mod，请稍候…";
            ShowToast("正在启动游戏", launchMsg, true);

            if (_settings.AutoOpenCollection) GameLauncher.OpenCollection();
            await GameLauncher.LaunchAsync(_installation, _settings);
            SettingsService.Log($"已唤起 Steam 启动游戏 (直连模式: {_settings.DirectConnect})");

            if (_settings.AutoMinimizeOnLaunch)
            {
                await Task.Delay(1500);
                WindowState = WindowState.Minimized;
            }
        }
        catch (Exception ex)
        {
            SettingsService.Log($"启动失败：{ex.Message}");
            ShowToast("启动失败", $"无法唤起 Steam：{ex.Message}", false);
        }
        finally
        {
            await Task.Delay(4000);
            LaunchButton.IsEnabled = true;
        }
    }

    private void ExternalLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        if (tag == "help") { OpenSettings("help"); return; }
        try { GameLauncher.OpenUrl(tag); SettingsService.Log($"打开外部链接：{tag}"); }
        catch (Exception ex) { ShowToast("无法打开链接", ex.Message, false); }
    }

    private void QuickMenuButton_Click(object sender, RoutedEventArgs e) => QuickMenuPopup.IsOpen = !QuickMenuPopup.IsOpen;

    private void QuickMenuItem_Click(object sender, RoutedEventArgs e)
    {
        QuickMenuPopup.IsOpen = false;
        if (sender is not Button { Tag: string action }) return;

        try
        {
            switch (action)
            {
                case "settings":
                    OpenSettings("basic");
                    break;
                case "shortcut":
                    CreateDesktopShortcut();
                    ShowToast("快捷方式已创建", "已在桌面创建“Zako 服启动器”快捷方式。", true);
                    break;
                case "update":
                    ShowArticle("检查更新", "当前启动器版本：1.2.0\n\n启动器已完成本地完整性检查。后续版本与服务器通知请以 Zako 官网公告为准。", GameLauncher.WebsiteUrl, "查看官网");
                    break;
                case "video_fix":
                    RunVideoFix();
                    break;
                case "repair":
                    GameLauncher.ValidateFiles();
                    SettingsService.Log("通过 Steam 请求验证 Garry's Mod 文件");
                    ShowToast("已请求一键修复", "Steam 将打开 Garry's Mod 文件验证页面。", true);
                    break;
            }
        }
        catch (Exception ex)
        {
            SettingsService.Log($"快捷菜单操作失败：{ex.Message}");
            ShowToast("操作失败", ex.Message, false);
        }
    }

    private static void CreateDesktopShortcut()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法取得启动器路径。");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "Zako 服启动器.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("系统不支持创建快捷方式。");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            var shortcutType = shortcut!.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [executable]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [AppContext.BaseDirectory]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, ["Zako Garry's Mod 服务器启动器"]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            try
            {
                if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
                if (shell is not null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
            catch { /* COM 对象释放异常静默忽略 */ }
        }
    }

    private void CollectionButton_Click(object sender, RoutedEventArgs e)
    {
        try { GameLauncher.OpenCollection(); }
        catch (Exception ex) { ShowToast("无法打开合集", ex.Message, false); }
    }

    private void NewsTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tab }) return;
        foreach (var button in new[] { ActivityTab, NoticeTab, InfoTab })
        {
            button.Foreground = BrushOf(button == sender ? "#FFFFFF" : "#7F858B");
            button.FontWeight = button == sender ? FontWeights.Bold : FontWeights.Normal;
        }
        (NewsLine1.Text, NewsDate1.Text, NewsLine2.Text, NewsDate2.Text, NewsLine3.Text, NewsDate3.Text) = tab switch
        {
            "notice" => ("[公告]  服务器位于成都，欢迎常驻", "置顶", "[规则]  请勿乱动物品、刷屏或欺负玩家", "必看", "[版本]  推荐使用 Garry's Mod x86-64", "提示"),
            "info" => ("[命令]  !rtd 可抽取随机道具或能力", "玩法", "[命令]  !huantu 可投票更换地图", "玩法", "[命令]  L 签到/复活，!motd 查看留言板", "更多"),
            _ => ("[欢迎]  神秘开趴服 · 多模式欢乐开放", "NEW", "[必看]  加入前请订阅服务器合集", "合集", "[活动]  TTT / COOP / 躲猫猫等模式轮换", "官网")
        };

        (NewsButton1.Tag, NewsButton2.Tag, NewsButton3.Tag) = tab switch
        {
            "notice" => ("notice_server", "rules", "x64"),
            "info" => ("cmd_rtd", "cmd_map", "cmd_more"),
            _ => ("welcome", "collection", "modes")
        };
    }

    private void ArticleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string article }) return;
        switch (article)
        {
            case "welcome":
                ShowArticle("欢迎来到神秘开趴服", "服务器位于成都，由 Kobeblyat 管理运营。这里是一个有趣、友好的 Garry's Mod 社区，拥有多种游戏模式和活跃的管理团队。\n\n如果希望添加新模组，可以联系客服反馈。", GameLauncher.WebsiteUrl, "访问官网");
                break;
            case "collection":
                ShowArticle("加入前请订阅服务器合集", "进入服务器前请先订阅 Zako 服创意工坊合集。Steam 会自动下载服务器所需地图、模型、材质与游戏模式。\n\n订阅完成后建议等待 Steam 下载完毕，再从启动器进入服务器。", GameLauncher.CollectionUrl, "打开模组合集");
                break;
            case "modes":
                ShowArticle("服务器模式轮换", "当前服务器包含：\n\n• TTT（叛徒模式）\n• 沙盒模式\n• 杀手模式\n• COOP 合作模式\n• 躲猫猫模式\n• 迷你游戏模式\n• 瘦长鬼影模式\n• SlashCo 模式\n• Among Us 模式\n• CS MINI 模式", GameLauncher.WebsiteUrl, "查看玩法介绍");
                break;
            case "notice_server":
                ShowArticle("服务器公告", "Zako 服务器位于成都，地址为 171.80.0.187。\n\n服务器持续开放，欢迎友好玩家常驻。最新活动通知、临时维护和模式轮换信息会优先发布在 Zako 官网。", GameLauncher.WebsiteUrl, "访问官网");
                break;
            case "rules":
                ShowArticle("服务器规则", "1. 请勿随意触碰或破坏其他玩家的物品。\n\n2. 请勿在聊天框刷屏。\n\n3. 请与其他玩家友好相处，禁止恶意骚扰。\n\n4. 最重要的是玩得开心，同时不要影响其他人的游戏体验。", null, null);
                break;
            case "x64":
                ShowArticle("强烈推荐 x86-64 · Zako 服最佳体验",
                    "检测到你仍在 32 位分支（public），在 Zako 服大量模组环境下极易闪退、贴图丢失或视频黑屏。启动器已为你自动检测 win64 文件并提供一键切换。\n\n" +
                    "【x86-64 五大好处】\n" +
                    "1. 突破 4GB 内存限制：32 位仅 4GB，订阅几百个创意工坊后必崩；64 位可使用全部内存，加载 Zako 全量模组更稳\n" +
                    "2. 性能更稳：64 位原生，TTT/躲猫猫/COOP 多实体场景帧率更高，配合无边框窗口更流畅\n" +
                    "3. 视频/HTML 完美修复：配合本启动器的“GMOD视频播放修复”（GModPatchTool 20260723，CEF 137），支持 H.264/AAC、Widevine、WebGL、GPU 加速，修复黑屏/无声/白屏/CEF 崩溃\n" +
                    "4. 官方推荐：Valve 已将 x86-64 设为推荐分支，后续更新与服务器内容优先适配 64 位\n" +
                    "5. 一键自动切换：点主界面“自动切换”或换版本弹窗中的“立即自动切换”，启动器会安全退出 Steam、备份 appmanifest_4000.acf、写入 BetaKey=x86-64 并重启 Steam\n\n" +
                    "【建议顺序】先切换 x86-64 → 等 Steam 更新完成 → 再执行“GMOD视频播放修复”→ 重启游戏，即可一次到位。\n\n" +
                    "也可手动：Steam → Garry's Mod → 右键属性 → 测试版 → 选择 x86-64。",
                    null, null);
                // 同时弹出全新设计的富弹窗，便于一键操作
                ShowX64Promo();
                break;
            case "cmd_rtd":
                ShowArticle("命令：!rtd", "在游戏聊天框输入 !rtd，可以抽取随机道具或特殊能力。具体效果会根据当前模式和服务器配置变化。", null, null);
                break;
            case "cmd_map":
                ShowArticle("命令：!huantu", "在游戏聊天框输入 !huantu，可以发起更换当前地图的投票。请根据在线玩家意见合理使用。", null, null);
                break;
            default:
                ShowArticle("更多实用命令", "L\n进行每日签到，或在 COOP 模式中自行复活。\n\n!motd\n查看服务器留言板和当前公告。\n\n遇到问题可以使用启动器右侧的客服按钮联系管理团队。", GameLauncher.SupportUrl, "联系客服");
                break;
        }
    }

    private void ShowArticle(string title, string body, string? actionUrl, string? actionText)
    {
        ArticleTitle.Text = title;
        ArticleBody.Text = body;
        _articleActionUrl = actionUrl;
        ArticleActionButton.Content = actionText ?? "打开相关页面";
        ArticleActionButton.Visibility = actionUrl is null ? Visibility.Collapsed : Visibility.Visible;
        ArticleOverlay.Visibility = Visibility.Visible;
    }

    private void CloseArticleButton_Click(object sender, RoutedEventArgs e) => ArticleOverlay.Visibility = Visibility.Collapsed;

    private void ArticleActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_articleActionUrl is null) return;
        try { GameLauncher.OpenUrl(_articleActionUrl); }
        catch (Exception ex) { ShowToast("无法打开链接", ex.Message, false); }
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e) => await ScanAsync(true);

    private void HeroButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            HeroImage.Source = new BitmapImage(new Uri(path, UriKind.Relative))
            {
                DecodePixelWidth = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? 1920 : 1440,
                CacheOption = BitmapCacheOption.OnLoad
            };
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings("basic");
    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Collapsed;
    private void OpenSettings(string page) { SettingsOverlay.Visibility = Visibility.Visible; ShowSettingsPage(page); }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) ShowSettingsPage(page);
    }

    private void ShowSettingsPage(string page)
    {
        BasicSettingsPanel.Visibility = page == "basic" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSettingsPanel.Visibility = page == "advanced" ? Visibility.Visible : Visibility.Collapsed;
        LogsSettingsPanel.Visibility = page == "logs" ? Visibility.Visible : Visibility.Collapsed;
        HelpSettingsPanel.Visibility = page == "help" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { BasicNav, AdvancedNav, LogsNav, HelpNav })
        {
            var active = button.Tag?.ToString() == page;
            button.Background = BrushOf(active ? "#2A2F33" : "#00FFFFFF");
            button.Foreground = BrushOf(active ? "#FFFFFF" : "#AAB0B5");
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
        if (page == "logs") LogTextBox.Text = SettingsService.ReadLog();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.DisplayMode = SelectedComboText(DisplayModeCombo);
        _settings.Resolution = SelectedComboText(ResolutionCombo);
        _settings.SkipIntro = SkipIntroCheck.IsChecked == true;
        _settings.EnableConsole = ConsoleCheck.IsChecked == true;
        _settings.AutoOpenCollection = AutoCollectionCheck.IsChecked == true;
        _settings.AutoMinimizeOnLaunch = AutoMinimizeCheck.IsChecked == true;
        _settings.DirectConnect = DirectConnectCheck.IsChecked == true;
        _settings.CustomArguments = CustomArgumentsText.Text.Trim();
        SettingsService.Save(_settings);
        ShowToast("设置已保存", "新的启动参数将在下次点击“开始游戏”时生效。", true);
    }

    private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installation.GamePath is null) { ShowToast("尚未定位游戏", "请先自动扫描或手动重新定位。", false); return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_installation.GamePath}\"") { UseShellExecute = true });
    }

    private async void RelocateGameButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择 Garry's Mod 根目录中的 gmod.exe", Filter = "Garry's Mod (gmod.exe)|gmod.exe|可执行文件 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        _settings.ManualGamePath = Path.GetDirectoryName(dialog.FileName);
        SettingsService.Save(_settings);
        SettingsService.Log($"手动定位游戏：{_settings.ManualGamePath}");
        await ScanAsync(true);
    }

    private void RefreshLogsButton_Click(object sender, RoutedEventArgs e) => LogTextBox.Text = SettingsService.ReadLog();

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(SettingsService.LogPath)!;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void ShowX64PromoFromHelp_Click(object sender, RoutedEventArgs e) { SettingsOverlay.Visibility = Visibility.Collapsed; ShowX64Promo(); }
    private void ToastClose_Click(object sender, RoutedEventArgs e) => HideToast();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed && WindowState != WindowState.Maximized)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove 在窗口状态切换或鼠标被释放时可能抛异常，静默拦截避免 Win32 消息循环 FailFast
            }
        }
    }
}
