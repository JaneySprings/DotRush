using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis;

public interface IComponentLoader<T> where T : class {
    void InitializeEmbeddedComponents();
    ReadOnlyCollection<T> LoadFromProject(Project project);
    ReadOnlyCollection<T> LoadFromAssembly(Assembly assembly);
    ImmutableArray<T> GetComponents(Project? project = null);
}