namespace DotRush.Debugging.Host.Installers;

public interface IDebuggerInstaller {
    void BeginInstallation();
    string? GetDownloadLink();
    string? Install(string downloadUrl);
    void EndInstallation(string executablePath);
}