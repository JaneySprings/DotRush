using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public class CodeRefactoringProvidersLoader : IComponentLoader<CodeRefactoringProvider> {
    public MemoryCache<CodeRefactoringProvider> ComponentsCache { get; } = new MemoryCache<CodeRefactoringProvider>();

    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromAssembly(string assemblyPath) {
        throw new NotImplementedException();
    }
    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromProject(Project project) {
        throw new NotImplementedException();
    }
    public ImmutableArray<CodeRefactoringProvider> GetComponents(Project? project = null) {
        throw new NotImplementedException();
    }
}