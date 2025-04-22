namespace DotRush.Roslyn.CodeAnalysis.Components;

public interface IAdditionalComponentsProvider {
    public IEnumerable<string> GetAdditionalAssemblies();
}