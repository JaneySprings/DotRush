using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Elfie.Serialization;

namespace DotRush.Roslyn.CodeAnalysis.Diagnostics;

public class DiagnosticsCollectionChangedEventArgs : EventArgs {
    public string DocumentPath { get; private set; }
    public Project? Source { get; private set; }
    public ReadOnlyCollection<Diagnostic> Diagnostics { get; private set; }

    public DiagnosticsCollectionChangedEventArgs(string documentPath, IList<Diagnostic> diagnostics, Project? source) {
        DocumentPath = documentPath;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics);
        Source = source;
    }
}