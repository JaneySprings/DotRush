using Microsoft.CodeAnalysis;

namespace DotRush.Server.Containers;

public class SourceDiagnostic {
    public Diagnostic InnerDiagnostic { get; private set; }
    public ProjectId SourceId { get; private set; }
    public string SourceName { get; private set; }

    public SourceDiagnostic(Diagnostic diagnostic, Project source) {
        InnerDiagnostic = diagnostic;
        SourceName = source.Name;
        SourceId = source.Id;
    }
}