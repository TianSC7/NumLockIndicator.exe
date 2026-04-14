using System;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace NumLockIndicator;

public class TrayManager : IDisposable
{
    private TaskbarIcon? _notifyIcon;
    private readonly MainWindow _mainWindow;
    private readonly AppSettings _settings;

    public TrayManager(MainWindow mainWindow, AppSettings settings)
    {
        _mainWindow = mainWindow;
        _settings = settings;
        InitializeTray();
    }

    private void InitializeTray()
    {
        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "NumLock Indicator",
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/icon.ico", UriKind.Absolute))
        };

        var contextMenu = new ContextMenu();

        var settingsItem = new MenuItem { Header = "设置..." };
        settingsItem.Click += (s, e) => OpenSettings();
        contextMenu.Items.Add(settingsItem);

        var toggleItem = new MenuItem { Header = "显示/隐藏窗口" };
        toggleItem.Click += (s, e) => ToggleWindow();
        contextMenu.Items.Add(toggleItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => ExitApp();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;

        _notifyIcon.TrayLeftMouseDown += (s, e) => ToggleWindow();
    }

    private void OpenSettings()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var settingsWindow = new SettingsWindow(_settings);
                settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "设置窗口异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));
    }

    private void ToggleWindow()
    {
        if (_mainWindow.IsVisible)
            _mainWindow.Hide();
        else
            _mainWindow.Show();
    }

    private void ExitApp()
    {
        _notifyIcon?.Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
