using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Containers;

public class DecompilationContainer {
    public ISymbol Symbol { get; set; }
    public SourceText SourceText { get; set; }

    public DecompilationContainer(ISymbol symbol, SourceText sourceText) {
        this.Symbol = symbol;
        this.SourceText = sourceText;
    }
}