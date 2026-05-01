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
    }
}
