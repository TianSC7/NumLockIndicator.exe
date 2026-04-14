using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NumLockIndicator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public static event Action? SettingsSaved;

    private bool _initialized;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
        _initialized = true;
        UpdatePreview();
    }

    private void LoadSettings()
    {
        FontSizeSlider.Value = _settings.FontSize;
        FontSizeLabel.Text = ((int)_settings.FontSize).ToString();
        WindowWidthBox.Text = _settings.WindowWidth.ToString("F0");
        WindowHeightBox.Text = _settings.WindowHeight.ToString("F0");
        OnTextBox.Text = _settings.OnText;
        OffTextBox.Text = _settings.OffText;

        foreach (var family in Fonts.SystemFontFamilies)
        {
            FontFamilyCombo.Items.Add(family.Source);
        }

        FontFamilyCombo.Text = _settings.FontFamily;

        OnTextBox.TextChanged += (s, e) => UpdatePreview();
        OffTextBox.TextChanged += (s, e) => UpdatePreview();
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel == null) return;
        FontSizeLabel.Text = ((int)FontSizeSlider.Value).ToString();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (!_initialized || PreviewOnText == null) return;

        var fontFamilyName = FontFamilyCombo.Text;
        var fontSize = (int)FontSizeSlider.Value;
        var onText = string.IsNullOrEmpty(OnTextBox.Text) ? "NUM ON" : OnTextBox.Text;
        var offText = string.IsNullOrEmpty(OffTextBox.Text) ? "\u26A0 NUM OFF" : OffTextBox.Text;

        try
        {
            var ff = new FontFamily(fontFamilyName);
            PreviewOnText.FontFamily = ff;
            PreviewOffText.FontFamily = ff;
        }
        catch
        {
            PreviewOnText.FontFamily = new FontFamily("Microsoft YaHei");
            PreviewOffText.FontFamily = new FontFamily("Microsoft YaHei");
        }

        PreviewOnText.Text = onText;
        PreviewOnText.FontSize = Math.Max(12, Math.Min(fontSize, 36));
        PreviewOnText.Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1));
        PreviewOnBorder.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x27, 0xAE, 0x60));

        PreviewOffText.Text = offText;
        PreviewOffText.FontSize = Math.Max(12, Math.Min(fontSize, 36));
        PreviewOffText.Foreground = Brushes.White;
        PreviewOffBorder.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0xE7, 0x4C, 0x3C));
    }

    private bool ApplyValues()
    {
        if (!double.TryParse(WindowWidthBox.Text, out double w) || w < 50)
        {
            MessageBox.Show("窗口宽度必须 >= 50", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!double.TryParse(WindowHeightBox.Text, out double h) || h < 30)
        {
            MessageBox.Show("窗口高度必须 >= 30", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _settings.FontFamily = FontFamilyCombo.Text;
        _settings.FontSize = (int)FontSizeSlider.Value;
        _settings.WindowWidth = w;
        _settings.WindowHeight = h;
        _settings.OnText = OnTextBox.Text;
        _settings.OffText = OffTextBox.Text;

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        SettingsSaved?.Invoke();
        return true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyValues()) DialogResult = true;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyValues();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
