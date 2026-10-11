using System.Windows;
using System.Windows.Threading;
using ZakoLauncher.Services;

namespace ZakoLauncher;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SettingsService.Log($"[全局UI异常捕获] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}");
        e.Handled = true; // 拦截异常，避免进程 FailFast 终止
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            SettingsService.Log($"[未处理域异常] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        else
            SettingsService.Log($"[未处理域异常] {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        SettingsService.Log($"[未观察异步任务异常] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}");
        e.SetObserved(); // 标记为已观察，避免 .NET 任务调度器导致崩溃
    }
}

