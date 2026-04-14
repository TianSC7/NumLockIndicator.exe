using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NumLockIndicator;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private static bool IsNumLockOn() => (GetKeyState(0x90) & 0x0001) != 0;

    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings;
    private bool _lastState;

    private static readonly SolidColorBrush OnBg = new(Color.FromArgb(0x30, 0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush OffBg = new(Color.FromArgb(0xE0, 0xE7, 0x4C, 0x3C));
    private static readonly SolidColorBrush OnDot = new(Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush OffDot = new(Color.FromRgb(0xFF, 0xD7, 0x00));

    public MainWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        else
        {
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = 10;
        }

        ApplySettings();

        _lastState = IsNumLockOn();
        UpdateDisplay(false);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        SettingsWindow.SettingsSaved += OnSettingsSaved;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var currentState = IsNumLockOn();
        if (currentState != _lastState)
        {
            _lastState = currentState;
            UpdateDisplay(true);
        }
    }

    private void UpdateDisplay(bool animate)
    {
        var isOn = IsNumLockOn();

        if (isOn)
        {
            MainBorder.Background = OnBg;
            StatusDot.Fill = OnDot;
            StatusText.Text = _settings.OnText;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1));
            ShadowEffect.Color = Color.FromArgb(0x40, 0x27, 0xAE, 0x60);
            ShadowEffect.BlurRadius = 10;
            ShadowEffect.Opacity = 0.3;
        }
        else
        {
            MainBorder.Background = OffBg;
            StatusDot.Fill = OffDot;
            StatusText.Text = _settings.OffText;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            ShadowEffect.Color = Color.FromArgb(0x80, 0xE7, 0x4C, 0x3C);
            ShadowEffect.BlurRadius = 16;
            ShadowEffect.Opacity = 0.6;
        }

        if (animate)
        {
            var sb = new Storyboard();
            var scaleUp = new DoubleAnimation(1.0, 1.08, TimeSpan.FromMilliseconds(80)) { EasingFunction = new QuadraticEase() };
            var scaleDown = new DoubleAnimation(1.08, 1.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(scaleUp, BorderScale);
            Storyboard.SetTargetProperty(scaleUp, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(scaleDown, BorderScale);
            Storyboard.SetTargetProperty(scaleDown, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(scaleUp);
            sb.Children.Add(scaleDown);

            var scaleYUp = new DoubleAnimation(1.0, 1.08, TimeSpan.FromMilliseconds(80)) { EasingFunction = new QuadraticEase() };
            var scaleYDown = new DoubleAnimation(1.08, 1.0, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase() };
            Storyboard.SetTarget(scaleYUp, BorderScale);
            Storyboard.SetTargetProperty(scaleYUp, new PropertyPath(ScaleTransform.ScaleYProperty));
            Storyboard.SetTarget(scaleYDown, BorderScale);
            Storyboard.SetTargetProperty(scaleYDown, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(scaleYUp);
            sb.Children.Add(scaleYDown);

            sb.Begin(this);
        }
    }

    private void ApplySettings()
    {
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        try { StatusText.FontFamily = new FontFamily(_settings.FontFamily); }
        catch { StatusText.FontFamily = new FontFamily("Microsoft YaHei"); }

        StatusText.FontSize = _settings.FontSize;
    }

    private void OnSettingsSaved()
    {
        Dispatcher.Invoke(() =>
        {
            ApplySettings();
            UpdateDisplay(false);
        });
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "ms-settings:keyboard",
            UseShellExecute = true
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
        base.OnClosed(e);
    }

    public new void Show() => base.Show();
    public new void Hide() => base.Hide();
}
