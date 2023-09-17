using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Server.Services;

public class CodeFixProviderContainer {
    public CodeFixProvider Provider { get; }
    public string? AssemblyPath { get; }
    public string? ProjectPath { get; }

    public CodeFixProviderContainer(CodeFixProvider provider, string? assemblyPath = null, string? projectPath = null) {
        Provider = provider;
        AssemblyPath = assemblyPath;
        ProjectPath = projectPath;
    }


    public override bool Equals(object? obj) {
        return this.GetHashCode() == obj?.GetHashCode();
    }

    public override int GetHashCode() {
        return Provider.ToString()?.GetHashCode() ?? -1;
    }
}