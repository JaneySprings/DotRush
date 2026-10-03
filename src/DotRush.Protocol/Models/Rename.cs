namespace DotRush.Protocol.Models;

public class RenameParams : TextDocumentPositionParams {
    public string NewName { get; set; } = string.Empty;
}
