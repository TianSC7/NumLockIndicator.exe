using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NumLockIndicator;

public class AppSettings
{
    public double FontSize { get; set; } = 20;
    public string FontFamily { get; set; } = "Microsoft YaHei";
    public double WindowWidth { get; set; } = 120;
    public double WindowHeight { get; set; } = 50;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public string OnText { get; set; } = "NUM ON";
    public string OffText { get; set; } = "NUM OFF";
    public string CapsOnText { get; set; } = "CAPS ON";
    public string CapsOffText { get; set; } = "CAPS OFF";
    public double CapsWindowLeft { get; set; } = double.NaN;
    public double CapsWindowTop { get; set; } = double.NaN;
    public bool MiddleButtonFilterEnabled { get; set; } = true;
    public int MiddleButtonFilterThresholdMs { get; set; } = 200;

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NumLockIndicator");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static AppSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
