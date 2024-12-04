namespace DotRush.Essentials.Common.External;

public interface IProcessLogger {
    void OnOutputDataReceived(string stdout);
    void OnErrorDataReceived(string stderr);
}