using System.Windows;
using System.Windows.Threading;
using R2Explorer.Models;
using R2Explorer.Services;

namespace R2Explorer;

/// <summary>
/// 应用入口：加载设置、初始化托盘、创建主窗口。
/// </summary>
public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    private TrayService? _tray;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Settings = SettingsService.Load();
        SetTheme(Settings.Theme);

        _mainWindow = new MainWindow();
        _tray = new TrayService(ShowMainWindow, ExitFromTray);
        _mainWindow.Show();
    }

    /// <summary>运行时切换主题：替换 MergedDictionaries 索引 0 的主题字典。</summary>
    public static void SetTheme(string name)
    {
        var themeName = string.Equals(name, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        var merged = Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
        {
            merged.RemoveAt(0);
        }
        merged.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative),
        });
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null)
            return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    /// <summary>通过托盘图标退出应用（绕过“关闭最小化到托盘”）。</summary>
    private void ExitFromTray()
    {
        _mainWindow?.AllowClose();
        SettingsService.Save(Settings);
        _tray?.Dispose();
        Shutdown();
    }

    /// <summary>显示托盘气泡通知。</summary>
    public void Notify(string title, string message)
        => _tray?.ShowBalloon(title, message);

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"程序发生未处理的错误：\n{e.Exception.Message}",
            "R2 Explorer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        SettingsService.Save(Settings);
        base.OnExit(e);
    }
}
