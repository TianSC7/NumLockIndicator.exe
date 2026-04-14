using System.Windows;
using System.Threading;

namespace NumLockIndicator;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private MainWindow? _mainWindow;
    private TrayManager? _trayManager;
    private AppSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "NumLockIndicator_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }
        _ownsMutex = true;

        base.OnStartup(e);

        _settings = AppSettings.Load();

        _mainWindow = new MainWindow(_settings);
        _mainWindow.Show();

        _trayManager = new TrayManager(_mainWindow, _settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
