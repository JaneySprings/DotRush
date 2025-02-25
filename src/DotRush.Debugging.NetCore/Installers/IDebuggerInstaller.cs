namespace DotRush.Debugging.NetCore.Installers;

interface IDebuggerInstaller {
    void BeginInstallation();
    string? GetDownloadLink();
    string? Install(string downloadUrl);
    void EndInstallation(string executablePath);
}