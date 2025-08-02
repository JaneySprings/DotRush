using System.Text.Json;
using System.Text.Json.Serialization;
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

namespace DotRush.Roslyn.Server.Handlers.Framework;

public class TestExplorerHandler : IJsonHandler {
    private readonly TestExplorerService testExplorerService;
    private readonly WorkspaceService workspaceService;

    public TestExplorerHandler(TestExplorerService testExplorerService, WorkspaceService workspaceService) {
        this.testExplorerService = testExplorerService;
        this.workspaceService = workspaceService;
    }

    protected Task<ICollection<TestItem>> Handle(TestFixtureParams? request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<ICollection<TestItem>>(Array.Empty<TestItem>(), async () => {
            var project = workspaceService.Solution?.Projects.FirstOrDefault(p => PathExtensions.Equals(p.FilePath, request?.TextDocument?.Uri.FileSystemPath));
            if (project == null)
                return Array.Empty<TestItem>();

            var fixtureSymbols = await testExplorerService.GetTestFixturesAsync(project, token).ConfigureAwait(false);
            return fixtureSymbols.Select(symbol => new TestItem(symbol)).ToHashSet();
        });
    }
    protected Task<ICollection<TestItem>> Handle(TestCaseParams? request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<ICollection<TestItem>>(Array.Empty<TestItem>(), async () => {
            var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request?.TextDocument?.Uri.FileSystemPath);
            var document = workspaceService.Solution?.GetDocument(documentId?.FirstOrDefault());
            if (document == null)
                return Array.Empty<TestItem>();

            if (string.IsNullOrEmpty(request?.FixtureId))
                return Array.Empty<TestItem>();

            var testCases = await testExplorerService.GetTestCasesAsync(document, request.FixtureId, token).ConfigureAwait(false);
            return testCases.Select(symbol => new TestItem(symbol)).ToHashSet();
        });
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddRequestHandler("dotrush/testExplorer/fixtures", async delegate (RequestMessage message, CancellationToken token) {
            var request = message.Params?.Deserialize<TestFixtureParams>();
            return JsonSerializer.SerializeToDocument(await Handle(request, token).ConfigureAwait(false));
        });
        lspCommunication.AddRequestHandler("dotrush/testExplorer/tests", async delegate (RequestMessage message, CancellationToken token) {
            var request = message.Params?.Deserialize<TestCaseParams>();
            return JsonSerializer.SerializeToDocument(await Handle(request, token).ConfigureAwait(false));
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(LanguageServer server, ClientCapabilities clientCapabilities) {
    }
}

public class TestFixtureParams {
    [JsonPropertyName("textDocument")] public TextDocumentIdentifier? TextDocument { get; set; }
}
public class TestCaseParams {
    [JsonPropertyName("textDocument")] public TextDocumentIdentifier? TextDocument { get; set; }
    [JsonPropertyName("fixtureId")] public string? FixtureId { get; set; }
}
public class TestItem {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("filePath")] public string? FilePath { get; set; }
    [JsonPropertyName("range")] public DocumentRange Range { get; set; }
    [JsonPropertyName("locations")] public string[]? Locations { get; set; }

    public TestItem(ISymbol symbol) {
        Id = symbol.GetFullName();
        Name = symbol.Name;

        if (symbol.Locations.Length > 0) {
            FilePath = symbol.Locations[0].SourceTree?.FilePath;
            Range = symbol.Locations[0].ToRange();
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
