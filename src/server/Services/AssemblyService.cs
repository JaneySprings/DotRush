using System.Reflection;
using DotRush.Server.Extensions;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Server.Services;

public class AssemblyService {
    public List<Assembly> Assemblies { get; }
    private readonly string[] embeddedAnalyzerReferences = new string[] {
        "Microsoft.CodeAnalysis.CSharp.Features",
        "Microsoft.CodeAnalysis.CSharp.Workspaces",
        "Microsoft.CodeAnalysis.Workspaces",
        "Microsoft.CodeAnalysis.Features"
    };

    public AssemblyService() {
        Assemblies = new List<Assembly>();
        LoadEmbeddedAssemblies();
    }

    public void ClearAssemblyCache() {
        Assemblies.Clear();
    }

    public void LoadAssemblies(string analyzersPath) {
        if (string.IsNullOrEmpty(analyzersPath) || !Directory.Exists(analyzersPath))
            return;

        foreach (var assemblyPath in Directory.GetFiles(analyzersPath, "*.dll", SearchOption.AllDirectories))
            LoadAssemblyFromPath(assemblyPath);
    }

    private void LoadEmbeddedAssemblies() {
        foreach (var assemblyName in embeddedAnalyzerReferences)
            LoadAssemblyFromName(assemblyName);
    }

    private void LoadAssemblyFromPath(string path) {
        ServerExtensions.SafeHandler(() => Assemblies.Add(Assembly.LoadFrom(path)));
    }

    private void LoadAssemblyFromName(string name) {
        ServerExtensions.SafeHandler(() => Assemblies.Add(Assembly.Load(name)));
    }
}