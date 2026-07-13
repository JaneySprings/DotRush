using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Handlers.ExternalAccess;

public class TestExplorerHandler : IJsonHandler {
    private readonly TestExplorerService testExplorerService;
    private readonly WorkspaceService workspaceService;

    public TestExplorerHandler(TestExplorerService testExplorerService, WorkspaceService workspaceService) {
        this.testExplorerService = testExplorerService;
        this.workspaceService = workspaceService;
    }

    protected Task<ICollection<TestItem>> Handle(TestItemParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<ICollection<TestItem>>(Array.Empty<TestItem>(), async () => {
            var filePath = request.TextDocument?.Uri.FileSystemPath;
            if (string.IsNullOrEmpty(filePath))
                return Array.Empty<TestItem>();

            ICollection<INamedTypeSymbol>? fixtureSymbols = null;
            if (WorkspaceExtensions.IsProjectFile(filePath)) {
                var project = workspaceService.Solution?.Projects.FirstOrDefault(p => PathExtensions.Equals(p.FilePath, filePath));
                if (project != null)
                    fixtureSymbols = await testExplorerService.GetTestFixturesAsync(project, token);
            }
            if (WorkspaceExtensions.IsSourceCodeDocument(filePath)) {
                var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath);
                var document = workspaceService.Solution?.GetDocument(documentId?.FirstOrDefault());
                if (document != null)
                    fixtureSymbols = await testExplorerService.GetTestFixturesAsync(document, token);
            }

            if (fixtureSymbols == null)
                return Array.Empty<TestItem>();

            var result = new HashSet<TestItem>();
            foreach (var fixtureSymbol in fixtureSymbols) {
                var fixture = new TestItem(fixtureSymbol);
                if (request.IncludeChildren) {
                    var testCaseSymbols = await testExplorerService.GetTestCasesAsync(fixtureSymbol, token);
                    if (testCaseSymbols != null)
                        fixture.Children = testCaseSymbols.Select(x => new TestItem(x)).ToHashSet();
                }
                result.Add(fixture);
            }
            return result;
        });
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddRequestHandler("dotrush/testExplorer/tests", async delegate (RequestMessage message, CancellationToken token) {
            var request = message.Params?.Deserialize<TestItemParams>(JsonSerializerConfig.Options) ?? new TestItemParams();
            return JsonSerializer.SerializeToDocument(await Handle(request, token));
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(LanguageServer server, ClientCapabilities clientCapabilities) {
    }
}

public class TestItemParams {
    [JsonPropertyName("textDocument")] public TextDocumentIdentifier? TextDocument { get; set; }
    [JsonPropertyName("includeChildren")] public bool IncludeChildren { get; set; }
}
public class TestItem {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("filePath")] public string? FilePath { get; set; }
    [JsonPropertyName("range")] public DocumentRange Range { get; set; }
    [JsonPropertyName("locations")] public string[]? Locations { get; set; }
    [JsonPropertyName("children")] public ICollection<TestItem>? Children { get; set; }

    public TestItem(ISymbol symbol) {
        Id = symbol.GetFullName();
        Name = symbol.Name;

        if (symbol.Locations.Length > 0) {
            var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            var sourceText = symbol.Locations[0].SourceTree?.GetText();
            if (reference != null && sourceText != null)
                Range = reference.Span.ToRange(sourceText);
            else
                Range = symbol.Locations[0].ToRange();

            FilePath = symbol.Locations[0].SourceTree?.FilePath;
            Locations = symbol.Locations.Select(l => l.SourceTree?.FilePath).WhereNotNull().ToArray();
        }
    }

    public override int GetHashCode() {
        return Id.GetHashCode();
    }
    public override bool Equals(object? obj) {
        if (obj is TestItem other)
            return Id == other.Id;
        return false;
    }
}
