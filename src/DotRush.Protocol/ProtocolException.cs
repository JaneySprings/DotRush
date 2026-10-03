namespace DotRush.Protocol;

// Fails the current request with a message that is shown to the user, any other exception is only logged
public class ProtocolException : Exception {
    public int Code { get; }

    public ProtocolException(string message) : this(ErrorCodes.RequestFailed, message) {
    }
    public ProtocolException(int code, string message) : base(message) {
        Code = code;
    }
}
