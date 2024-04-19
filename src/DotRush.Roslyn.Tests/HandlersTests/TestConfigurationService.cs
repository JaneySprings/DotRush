using System.Collections.ObjectModel;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Tests.HandlersTests;

public class TestConfigurationService : IConfigurationService {
    public bool UseRoslynAnalyzers { get; set; }
    public bool ShowItemsFromUnimportedNamespaces { get; set; }
    public bool SkipUnrecognizedProjects { get; set; } = true;
    public bool LoadMetadataForReferencedProjects { get; set; } = true;
    public bool RestoreProjectsBeforeLoading { get; set; } = true;
    public bool CompileProjectsAfterLoading { get; set; } = true;
    public Dictionary<string, string> WorkspaceProperties { get; set; } = new();
    public ReadOnlyCollection<string> ProjectFiles { get; set; } = new ReadOnlyCollection<string>(new List<string>());
}