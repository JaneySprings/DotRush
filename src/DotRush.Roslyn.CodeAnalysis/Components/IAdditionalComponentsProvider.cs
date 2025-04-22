namespace DotRush.Roslyn.CodeAnalysis.Components;

public interface IAdditionalComponentsProvider {
    public bool IsEnabled { get; }
    public IEnumerable<string> GetAdditionalAssemblies();
}