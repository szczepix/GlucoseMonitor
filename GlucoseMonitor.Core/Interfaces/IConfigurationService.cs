namespace GlucoseMonitor.Core.Interfaces;

public interface IConfigurationService
{
    void SaveConfiguration(string url, string token, string units, int interval);
    (string url, string token, string units, int interval) LoadConfiguration();
}