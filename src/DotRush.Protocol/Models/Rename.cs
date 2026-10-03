namespace DotRush.Protocol.Models;

public class PrepareRenameParams : TextDocumentPositionParams {
}

public class RenameParams : TextDocumentPositionParams {
    public string NewName { get; set; } = string.Empty;
}
