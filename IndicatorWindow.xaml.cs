using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NumLockIndicator;

public enum IndicatorType { NumLock, CapsLock }

public partial class IndicatorWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static bool IsNumLockOn() => (GetKeyState(0x90) & 0x0001) != 0;
    private static bool IsCapsLockOn() => (GetKeyState(0x14) & 0x0001) != 0;

    private readonly DispatcherTimer _timer;
    private readonly AppSettings _settings;
    private readonly IndicatorType _type;
    private bool _lastState;
    private readonly DispatcherTimer _topmostTimer;

    private static readonly SolidColorBrush OnBg = new(Color.FromArgb(0x30, 0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush OffBg = new(Color.FromArgb(0xE0, 0xE7, 0x4C, 0x3C));
    private static readonly SolidColorBrush OnDot = new(Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly SolidColorBrush OffDot = new(Color.FromRgb(0xFF, 0xD7, 0x00));

    public const double VisualMargin = 8;

    public IndicatorType Type => _type;

    public event Action<IndicatorWindow>? PositionChanged;
    public event Action<IndicatorWindow>? DragStarted;
    public event Action<IndicatorWindow>? DragCompleted;

    public Rect GetVisualRect()
    {
        return new Rect(
            Left + VisualMargin,
            Top + VisualMargin,
            ActualWidth - VisualMargin * 2,
            ActualHeight - VisualMargin * 2);
    }

    public IndicatorWindow(AppSettings settings, IndicatorType type)
    {
        _settings = settings;
        _type = type;
        InitializeComponent();

        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        double savedLeft = type == IndicatorType.NumLock ? _settings.WindowLeft : _settings.CapsWindowLeft;
        double savedTop = type == IndicatorType.NumLock ? _settings.WindowTop : _settings.CapsWindowTop;

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        if (!double.IsNaN(savedLeft) && !double.IsNaN(savedTop)
            && savedLeft > -Width && savedLeft < screenWidth
            && savedTop > -Height && savedTop < screenHeight)
        {
            Left = savedLeft;
            Top = savedTop;
        }
        else
        {
            double offset = type == IndicatorType.NumLock ? 0 : Height + 4;
            Left = screenWidth - Width - 20;
            Top = 10 + offset;
        }

        ApplySettings();

        _lastState = GetCurrentState();
        UpdateDisplay(false);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) => EnforceTopmost();
        _topmostTimer.Start();

        Loaded += (_, _) => EnforceTopmost();

        SettingsWindow.SettingsSaved += OnSettingsSaved;
    }

    private bool GetCurrentState() => _type == IndicatorType.NumLock ? IsNumLockOn() : IsCapsLockOn();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var currentState = GetCurrentState();
        if (currentState != _lastState)
        {
            _lastState = currentState;
            UpdateDisplay(true);
        }
    }

    private void UpdateDisplay(bool animate)
    {
        var isOn = GetCurrentState();
        var onText = _type == IndicatorType.NumLock ? _settings.OnText : _settings.CapsOnText;
        var offText = _type == IndicatorType.NumLock ? _settings.OffText : _settings.CapsOffText;

        if (isOn)
        {
            MainBorder.Background = OnBg;
            StatusDot.Fill = OnDot;
            StatusText.Text = onText;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1));
            ShadowEffect.Color = Color.FromArgb(0x40, 0x27, 0xAE, 0x60);
            ShadowEffect.BlurRadius = 10;
            ShadowEffect.Opacity = 0.3;
        }
        else
        {
            MainBorder.Background = OffBg;
            StatusDot.Fill = OffDot;
            StatusText.Text = offText;
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

    private void EnforceTopmost()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
                SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch { }
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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragStarted?.Invoke(this);
        DragMove();
        DragCompleted?.Invoke(this);
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        ClampToScreen();
        PositionChanged?.Invoke(this);
    }

    private void ClampToScreen()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;

        double minVisible = 20;

        if (Left + w - minVisible < 0) Left = minVisible - w;
        if (Top + h - minVisible < 0) Top = minVisible - h;
        if (Left > screenW - minVisible) Left = screenW - minVisible;
        if (Top > screenH - minVisible) Top = screenH - minVisible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer.Stop();
        SavePosition();
        base.OnClosed(e);
    }

    public void SavePosition()
    {
        if (_type == IndicatorType.NumLock)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }
        else
        {
            _settings.CapsWindowLeft = Left;
            _settings.CapsWindowTop = Top;
        }
    }

    public new void Show()
    {
        base.Show();
        EnforceTopmost();
    }
    public new void Hide() => base.Hide();
}
