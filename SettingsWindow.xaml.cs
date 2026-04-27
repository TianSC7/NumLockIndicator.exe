using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NumLockIndicator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public static event Action? SettingsSaved;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        FontSizeSlider.Value = _settings.FontSize;
        FontSizeLabel.Text = ((int)_settings.FontSize).ToString();
        WindowWidthBox.Text = _settings.WindowWidth.ToString("F0");
        WindowHeightBox.Text = _settings.WindowHeight.ToString("F0");
        OnTextBox.Text = _settings.OnText;
        OffTextBox.Text = _settings.OffText;
        CapsOnTextBox.Text = _settings.CapsOnText;
        CapsOffTextBox.Text = _settings.CapsOffText;

        FilterEnabledCheck.IsChecked = _settings.MiddleButtonFilterEnabled;
        FilterThresholdSlider.Value = _settings.MiddleButtonFilterThresholdMs;
        FilterThresholdLabel.Text = $"{_settings.MiddleButtonFilterThresholdMs}ms";

        foreach (var family in Fonts.SystemFontFamilies)
        {
            FontFamilyCombo.Items.Add(family.Source);
        }

        FontFamilyCombo.Text = _settings.FontFamily;
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel == null) return;
        FontSizeLabel.Text = ((int)FontSizeSlider.Value).ToString();
    }

    private void FilterThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FilterThresholdLabel == null) return;
        FilterThresholdLabel.Text = $"{(int)FilterThresholdSlider.Value}ms";
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
        _settings.CapsOnText = CapsOnTextBox.Text;
        _settings.CapsOffText = CapsOffTextBox.Text;
        _settings.MiddleButtonFilterEnabled = FilterEnabledCheck.IsChecked ?? true;
        _settings.MiddleButtonFilterThresholdMs = (int)FilterThresholdSlider.Value;

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
