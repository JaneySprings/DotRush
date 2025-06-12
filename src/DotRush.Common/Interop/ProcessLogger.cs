namespace DotRush.Common.Interop;

public interface IProcessLogger {
    void OnOutputDataReceived(string stdout);
    void OnErrorDataReceived(string stderr);
}