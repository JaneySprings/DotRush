using DotRush.Roslyn.CodeAnalysis;

namespace DotRush.Roslyn.Server.Services;

public class CodeAnalysisService {
    public CompilationHost CompilationHost { get; init; }
    public CodeActionHost CodeActionHost { get; init; }

    public CodeAnalysisService() {
        CodeActionHost = new CodeActionHost();
        CompilationHost = new CompilationHost();
    }
}