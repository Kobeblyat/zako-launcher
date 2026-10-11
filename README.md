# Zako 服启动器

Windows WPF 启动器，会自动扫描 Steam 库中的 Garry's Mod、检测 x86-64 文件，并通过 Steam 启动参数连接 `171.80.0.187`。集成 **GMOD视频播放修复（GModPatchTool 20260723，CEF 137）**，一键修复游戏内视频黑屏/无声与 HTML 白屏。

## 构建

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

发布文件位于 `bin\Release\net9.0-windows\win-x64\publish\ZakoLauncher.exe`，自带运行时与 GMOD视频修复工具（已嵌入），启用单文件压缩后约 **70MB**，**单文件分发**。

## 功能

- 自动读取 Steam 注册表路径和 `libraryfolders.vdf` 中的多磁盘游戏库
- 同时检查 `UserConfig/BetaKey=x86-64` 与 `bin\win64` 组件，避免残留文件误判
- x86-64 缺失时显示应用内通知
- 一键打开创意工坊合集
- 使用 `-applaunch 4000 +connect 171.80.0.187` 启动并进入服务器
- 官网、客服、合集快捷入口
- 活动 / 公告 / 资讯切换及官网玩法信息
- 游戏目录重新定位、显示模式、分辨率和自定义启动参数
- 持久化设置与本地运行日志
- 16:9 自适应窗口与 Per-Monitor V2 高 DPI 支持
- 内置公告阅读器，不离开启动器即可查看正文
- 右下角快捷菜单：设置、桌面快捷方式、检查更新、Steam 文件修复
- 非 x86-64 时可一键备份 Steam 清单、切换分支并重启 Steam，并以富弹窗详细介绍 **x86-64 五大好处**：突破 4GB、性能更稳、CEF 视频完美修复、官方推荐、一键自动切换
- 🎬 **GMOD视频播放修复**：集成 **GModPatchTool 20260723版**（源自 D:\gmodpatch\gmodpatchtool_0723 ），CEF 升级至 137.0.19（Chromium 137），开启 H.264/AAC、Widevine、WebGL、GPU 加速；工具以**嵌入资源**方式打进启动器，运行时自动解压到 `%LOCALAPPDATA%\ZakoLauncher\Tools\`，**发布仅一个 EXE**；入口位于主界面“视频播放修复”按钮、快捷菜单“GMOD视频播放修复”、设置→基本卡片与帮助页
- 换版本弹窗与 `x64` 公告已重构为富文本，推荐顺序：先切换 x86-64 → 再执行视频修复
- 使用 fatcat 图标作为窗口与 EXE 图标
- EXE 图标包含 16–256px 多尺寸透明圆角版本
- 不透明无留白窗口框架，避免透明窗口黑边
- 使用 Windows DWM 原生圆角裁剪窗口四角
