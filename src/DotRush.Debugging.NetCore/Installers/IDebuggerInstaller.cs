namespace DotRush.Debugging.NetCore.Installers;

public interface IDebuggerInstaller {
    void BeginInstallation();
    string? GetDownloadLink();
    string? Install(string downloadUrl);
    void EndInstallation(string executablePath);
}