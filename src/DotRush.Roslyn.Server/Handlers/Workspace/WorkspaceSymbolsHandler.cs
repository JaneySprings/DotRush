using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class WorkspaceSymbolsHandler : WorkspaceSymbolsHandlerBase {
    private readonly WorkspaceService solutionService;

    public WorkspaceSymbolsHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities) {
        return new WorkspaceSymbolRegistrationOptions();
    }

    public override Task<Container<WorkspaceSymbol>?> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            var workspaceSymbols = new HashSet<WorkspaceSymbol>();
            if (solutionService.Solution == null || string.IsNullOrEmpty(request.Query))
                return null;

            foreach (var project in solutionService.Solution.Projects) {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                    continue;

                var symbols = compilation.GetSymbolsWithName((s) => WorkspaceSymbolFilter(s, request.Query), SymbolFilter.TypeAndMember, cancellationToken);
                foreach (var symbol in symbols) {
                    foreach (var location in symbol.Locations) {
                        var lspLocation = location.ToLocation();
                        if (lspLocation == null)
                            continue;

                        workspaceSymbols.Add(new WorkspaceSymbol {
                            Kind = symbol.ToSymbolKind(),
                            Name = symbol.Name,
                            Location = lspLocation,
                        });
                    }
                }
            }

            return new Container<WorkspaceSymbol>(workspaceSymbols);
        });
    }

    private static bool WorkspaceSymbolFilter(string symbolName, string query) {
        return symbolName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}