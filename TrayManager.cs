using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace NumLockIndicator;

public class TrayManager : IDisposable
{
    private TaskbarIcon? _notifyIcon;
    private readonly List<IndicatorWindow> _windows = new();
    private readonly AppSettings _settings;
    private MenuItem? _toggleItem;

    public TrayManager(IndicatorWindow numLockWindow, IndicatorWindow capsLockWindow, AppSettings settings)
    {
        _windows.Add(numLockWindow);
        _windows.Add(capsLockWindow);
        _settings = settings;
        InitializeTray();
    }

    private void InitializeTray()
    {
        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "NumLock & CapsLock Indicator",
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/icon.ico", UriKind.Absolute))
        };

        var contextMenu = new ContextMenu();

        var settingsItem = new MenuItem { Header = "设置..." };
        settingsItem.Click += (s, e) => OpenSettings();
        contextMenu.Items.Add(settingsItem);

        _toggleItem = new MenuItem { Header = "隐藏" };
        _toggleItem.Click += (s, e) => ToggleWindows();
        contextMenu.Items.Add(_toggleItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => ExitApp();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;
        contextMenu.Opened += (s, e) => UpdateToggleText();
    }

    private void UpdateToggleText()
    {
        if (_toggleItem != null)
        {
            bool anyVisible = _windows.Exists(w => w.IsVisible);
            _toggleItem.Header = anyVisible ? "隐藏" : "显示";
        }
    }

    private SettingsWindow? _openSettingsWindow;

    private void OpenSettings()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_openSettingsWindow != null && _openSettingsWindow.IsLoaded)
                {
                    _openSettingsWindow.Activate();
                    return;
                }

                _openSettingsWindow = new SettingsWindow(_settings);
                _openSettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _openSettingsWindow.Closed += (s, e) => _openSettingsWindow = null;
                _openSettingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "设置窗口异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));
    }

    private void ToggleWindows()
    {
        bool anyVisible = _windows.Exists(w => w.IsVisible);
        foreach (var w in _windows)
        {
            if (anyVisible)
                w.Hide();
            else
                w.Show();
        }
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
