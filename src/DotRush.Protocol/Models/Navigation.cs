namespace DotRush.Protocol.Models;

public class DefinitionParams : TextDocumentPositionParams {
}

public class TypeDefinitionParams : TextDocumentPositionParams {
}

public class ImplementationParams : TextDocumentPositionParams {
}

public class ReferenceParams : TextDocumentPositionParams {
    public ReferenceContext? Context { get; set; }
}

public class ReferenceContext {
    public bool IncludeDeclaration { get; set; }
}
