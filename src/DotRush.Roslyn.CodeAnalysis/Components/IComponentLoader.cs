using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public interface IComponentLoader<T> where T : class {
    MemoryCache<T> ComponentsCache { get; }

    ReadOnlyCollection<T> LoadFromProject(Project project);
    ReadOnlyCollection<T> LoadFromAssembly(string assemblyName);
    ReadOnlyCollection<T> LoadFromDotRush();
    ImmutableArray<T> GetComponents(Project? project = null);
}