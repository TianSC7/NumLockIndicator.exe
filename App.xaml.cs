using System.Windows;
using System.Threading;

namespace NumLockIndicator;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private IndicatorWindow? _numLockWindow;
    private IndicatorWindow? _capsLockWindow;
    private TrayManager? _trayManager;
    private AppSettings? _settings;
    private SnapManager? _snapManager;
    private MiddleButtonFilter? _middleButtonFilter;

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

        _numLockWindow = new IndicatorWindow(_settings, IndicatorType.NumLock);
        _numLockWindow.Show();

        _capsLockWindow = new IndicatorWindow(_settings, IndicatorType.CapsLock);
        _capsLockWindow.Show();

        _snapManager = new SnapManager(_numLockWindow, _capsLockWindow);

        _middleButtonFilter = new MiddleButtonFilter(_settings.MiddleButtonFilterThresholdMs);
        _middleButtonFilter.IsEnabled = _settings.MiddleButtonFilterEnabled;

        _trayManager = new TrayManager(_numLockWindow, _capsLockWindow, _settings, _middleButtonFilter);

        SettingsWindow.SettingsSaved += OnSettingsSaved;
    }

    private void OnSettingsSaved()
    {
        if (_middleButtonFilter != null)
        {
            _middleButtonFilter.IsEnabled = _settings!.MiddleButtonFilterEnabled;
            _middleButtonFilter.UpdateThreshold(_settings.MiddleButtonFilterThresholdMs);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _numLockWindow?.SavePosition();
        _capsLockWindow?.SavePosition();
        _settings?.Save();
        _middleButtonFilter?.Dispose();
        _trayManager?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
