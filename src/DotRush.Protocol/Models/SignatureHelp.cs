namespace DotRush.Protocol.Models;

public class SignatureHelpParams : TextDocumentPositionParams {
}

public class SignatureHelp {
    public List<SignatureInformation> Signatures { get; set; } = new List<SignatureInformation>();
    public uint? ActiveSignature { get; set; }
    public uint? ActiveParameter { get; set; }
}

public class SignatureInformation {
    public string Label { get; set; } = string.Empty;
    public MarkupContent? Documentation { get; set; }
    public List<ParameterInformation> Parameters { get; set; } = new List<ParameterInformation>();
    public uint? ActiveParameter { get; set; }
}

public class ParameterInformation {
    public string Label { get; set; } = string.Empty;
    public MarkupContent? Documentation { get; set; }
}
