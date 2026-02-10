using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Components;

public interface IComponentLoader<T> : IClearable where T : class {
    MemoryCache<T> ComponentsCache { get; }
    string[] SkippedComponentNames { get; }

    List<T> LoadFromProject(Project project);
    List<T> LoadFromAssembly(string assemblyName);
    List<T> LoadFromDotRush();
    ImmutableArray<T> GetComponents(Project project);
}
