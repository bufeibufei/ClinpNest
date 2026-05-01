using ClipNest.Data;
using ClipNest.Models;
using ClipNest.Services;
using System.Windows;

namespace ClipNest;

public partial class App
{
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "DispatcherUnhandledException");
            System.Windows.MessageBox.Show($"ClipNest 启动或运行时发生错误，日志已写入：\n{AppLogger.LogPath}\n\n{args.Exception.Message}", "ClipNest 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLogger.Error(ex, "UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            AppLogger.Info("Startup begin");
            var database = new AppDatabase();
            await database.InitializeAsync();

            var clipboardRepository = new ClipboardRepository(database);
            var settingsRepository = new SettingsRepository(database);
            var historyService = new ClipboardHistoryService(
                clipboardRepository,
                new SensitiveContentService(),
                new SourceAppService());
            var monitorService = new ClipboardMonitorService(historyService);
            var hotkeyService = new HotkeyService();
            var pasteService = new PasteService(clipboardRepository);

            var savedHotkey = HotkeySettings.Parse(await settingsRepository.GetAsync("quick_panel_hotkey"));

            _trayService = new TrayService();
            _mainWindow = new MainWindow(
                clipboardRepository,
                settingsRepository,
                historyService,
                monitorService,
                hotkeyService,
                pasteService,
                _trayService,
                savedHotkey);

            _mainWindow.Show();
            AppLogger.Info("Startup finished");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Startup failed");
            System.Windows.MessageBox.Show($"ClipNest 启动失败，日志已写入：\n{AppLogger.LogPath}\n\n{ex.Message}", "ClipNest 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
