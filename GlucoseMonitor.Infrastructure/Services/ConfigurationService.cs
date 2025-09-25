using GlucoseMonitor.Core.Interfaces;

namespace GlucoseMonitor.Infrastructure.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;

    public ConfigurationService()
    {
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlucoseMonitor");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.txt");
    }

    public void SaveConfiguration(string url, string token, string units, int interval)
    {
        try
        {
            File.WriteAllLines(_configPath, new[]
            {
                url ?? "",
                token ?? "",
                units,
                interval.ToString()
            });
        }
        catch
        {
            // Ignore save errors for KISS approach
        }
    }

    public (string url, string token, string units, int interval) LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var lines = File.ReadAllLines(_configPath);
                if (lines.Length >= 4)
                {
                    var url = lines[0];
                    var token = lines[1];
                    var units = lines[2];
                    var interval = int.TryParse(lines[3], out var i) ? i : 1;
                    return (url, token, units, interval);
                }
            }
        }
        catch
        {
            // Return defaults on error
        }

        return ("", "", "mg", 1);
    }
}