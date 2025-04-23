using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public interface IComponentLoader<T> where T : class {
    MemoryCache<T> ComponentsCache { get; }

    List<T> LoadFromProject(Project project);
    List<T> LoadFromAssembly(string assemblyName);
    List<T> LoadFromDotRush();
    ImmutableArray<T> GetComponents(Project project);
}