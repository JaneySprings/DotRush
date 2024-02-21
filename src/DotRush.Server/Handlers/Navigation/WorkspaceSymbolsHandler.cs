using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WorkspaceSymbolsHandler : WorkspaceSymbolsHandlerBase {
    private readonly WorkspaceService solutionService;

    public WorkspaceSymbolsHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities) {
        return new WorkspaceSymbolRegistrationOptions();
    }

    public override async Task<Container<WorkspaceSymbol>?> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<Container<WorkspaceSymbol>?>(async () => {
            var symbols = new List<WorkspaceSymbol>();
            if (solutionService.Solution == null)
                return null;

            foreach (var project in solutionService.Solution.Projects) {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                    continue;

                foreach (var symbol in compilation.GetSymbolsWithName((s) => s.Contains(request.Query), SymbolFilter.TypeAndMember, cancellationToken)) {
                    foreach (var location in symbol.Locations) {
                        var lspLocation = location.ToLocation();
                        if (lspLocation == null)
                            continue;

                        symbols.Add(new WorkspaceSymbol {
                            Kind = symbol.Kind.ToSymbolKind(),
                            Name = symbol.Name,
                            Location = lspLocation,
                        });
                    }
                }
            }

            return new Container<WorkspaceSymbol>(symbols);
        });
    }
}