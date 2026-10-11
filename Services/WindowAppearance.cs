using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ZakoLauncher.Services;

public static class WindowAppearance
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int Round = 2;

    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_THICKFRAME = 0x00040000;

    public static void ApplyRoundedCorners(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            // 补全 WS_CAPTION 等 Win32 窗口样式，使得 Windows DWM 能正确将无边框窗口最小化到任务栏，杜绝屏幕左下角残留黑色小方框
            var style = GetWindowLong(handle, GWL_STYLE);
            SetWindowLong(handle, GWL_STYLE, style | WS_CAPTION | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_THICKFRAME);

            var corner = Round;
            DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref corner, sizeof(int));
            var dark = 1;
            DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkMode, ref dark, sizeof(int));
        }
        catch
        {
            // 兼容不支持 DWM 属性的旧版 Windows 系统
        }
    }

    public static (int Width, int Height) GetPhysicalScreenResolution()
    {
        try
        {
            var w = GetSystemMetrics(0); // SM_CXSCREEN
            var h = GetSystemMetrics(1); // SM_CYSCREEN
            if (w > 0 && h > 0) return (w, h);
        }
        catch { }

        // 回退默认 1080p
        return (1920, 1080);
    }

    public static void OptimizeBackgroundResources(bool enteringBackground)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var currentProc = System.Diagnostics.Process.GetCurrentProcess();
            if (enteringBackground)
            {
                // 1. 降低 CPU 调度优先级至低于正常，将 100% 算力让渡给游戏
                try { currentProc.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; } catch { }

                // 2. 深度回收托管堆垃圾
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);

                // 3. 将物理内存工作集（Working Set）归还给 Windows 系统内核，内存瞬间降至几兆
                try
                {
                    var handle = currentProc.Handle;
                    EmptyWorkingSet(handle);
                    SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));
                }
                catch { }

                SettingsService.Log("启动器已进入后台低功耗模式（CPU 占用归零，物理内存已释放归还系统）");
            }
            else
            {
                // 恢复前台优先级
                try { currentProc.PriorityClass = System.Diagnostics.ProcessPriorityClass.Normal; } catch { }
            }
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);
}

