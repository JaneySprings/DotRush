using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace DotRush.Roslyn.CodeAnalysis.Refactorings;

public class CodeRefactoringProvidersLoader : IComponentLoader<CodeRefactoringProvider> {
    public void InitializeEmbeddedComponents() {
        throw new NotImplementedException();
    }

    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromAssembly(Assembly assembly) {
        throw new NotImplementedException();
    }
    public ReadOnlyCollection<CodeRefactoringProvider> LoadFromProject(Project project) {
        throw new NotImplementedException();
    }
    public ImmutableArray<CodeRefactoringProvider> GetComponents(Project? project = null) {
        throw new NotImplementedException();
    }
}