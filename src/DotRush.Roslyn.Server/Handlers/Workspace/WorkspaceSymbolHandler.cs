using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceSymbol;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class WorkspaceSymbolHandler : WorkspaceSymbolHandlerBase {
    private readonly WorkspaceService solutionService;

    public WorkspaceSymbolHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.WorkspaceSymbolProvider = true;
    }
    protected override Task<WorkspaceSymbolResponse> Handle(WorkspaceSymbolParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new WorkspaceSymbolResponse(new List<WorkspaceSymbol>()), async () => {
            var workspaceSymbols = new HashSet<WorkspaceSymbol>(WorkspaceSymbolEqualityComparer.Default);
            if (solutionService.Solution == null || string.IsNullOrEmpty(request.Query))
                return new WorkspaceSymbolResponse(new List<WorkspaceSymbol>());

            foreach (var project in solutionService.Solution.Projects) {
                var compilation = await project.GetCompilationAsync(token).ConfigureAwait(false);
                if (compilation == null)
                    continue;

                var symbols = compilation.GetSymbolsWithName((s) => WorkspaceSymbolFilter(s, request.Query), SymbolFilter.TypeAndMember, token);
                foreach (var symbol in symbols) {
                    foreach (var location in symbol.Locations) {
                        var lspLocation = location.ToLocation();
                        if (lspLocation == null)
                            continue;

                        workspaceSymbols.Add(new WorkspaceSymbol {
                            Kind = symbol.ToSymbolKind(),
                            Name = symbol.Name,
                            Location = lspLocation,
                            ContainerName = symbol.ContainingType?.Name
                        });
                    }
                }
            }

            return new WorkspaceSymbolResponse(workspaceSymbols.ToList());
        });
    }
    protected override Task<WorkspaceSymbol> Resolve(WorkspaceSymbol request, CancellationToken token) {
        return Task.FromResult(request);
    }

    private static bool WorkspaceSymbolFilter(string symbolName, string query) {
        return symbolName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    class WorkspaceSymbolEqualityComparer : IEqualityComparer<WorkspaceSymbol> {
        public static WorkspaceSymbolEqualityComparer Default { get; } = new WorkspaceSymbolEqualityComparer();

        public bool Equals(WorkspaceSymbol? x, WorkspaceSymbol? y) {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            return GetHashCode(x) == GetHashCode(y);
        }
        public int GetHashCode(WorkspaceSymbol obj) {
            return HashCode.Combine(obj.Name, obj.Kind, obj.Location);
        }
    }
}