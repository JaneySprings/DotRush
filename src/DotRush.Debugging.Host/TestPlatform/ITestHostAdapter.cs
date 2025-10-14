namespace DotRush.Debugging.Host.TestPlatform;

public interface ITestHostAdapter {
    Task StartSession(string[] testAssemblies, string[] typeFilters);
    Task StartSession(string[] testAssemblies, string[] typeFilters, string? runSettingsFilePath);
}