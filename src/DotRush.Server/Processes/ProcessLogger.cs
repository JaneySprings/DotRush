namespace DotRush.Server.Processes;

public interface IProcessLogger {
    void OnOutputDataReceived(string stdout);
    void OnErrorDataReceived(string stderr);
}
