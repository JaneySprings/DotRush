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
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server.Handlers.Framework;

public class TestExplorerHandler : IJsonHandler {
    private readonly TestExplorerService testExplorerService;
    private readonly WorkspaceService workspaceService;

    public TestExplorerHandler(TestExplorerService testExplorerService, WorkspaceService workspaceService) {
        this.testExplorerService = testExplorerService;
        this.workspaceService = workspaceService;
    }

    protected Task<List<TestItem>?> Handle(TestFixtureParams? request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<List<TestItem>?>(async () => {
            var project = workspaceService.Solution?.Projects.FirstOrDefault(p => PathExtensions.Equals(p.FilePath, request?.TextDocument?.Uri.FileSystemPath));
            if (project == null)
                return null;

            var fixtureSymbols = await testExplorerService.GetTestFixturesAsync(project, token).ConfigureAwait(false);
            return fixtureSymbols.Select(symbol => new TestItem(symbol)).ToList();
        });
    }
    protected Task<List<TestItem>?> Handle(TestCaseParams? request, CancellationToken token) {
        return SafeExtensions.InvokeAsync<List<TestItem>?>(async () => {
            var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request?.TextDocument?.Uri.FileSystemPath);
            var document = workspaceService.Solution?.GetDocument(documentId?.FirstOrDefault());
            if (document == null)
                return null;

            if (string.IsNullOrEmpty(request?.FixtureId))
                return null;

            var testCases = await testExplorerService.GetTestCasesAsync(document, request.FixtureId, token).ConfigureAwait(false);
            return testCases.Select(symbol => new TestItem(symbol)).ToList();
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
    public void RegisterDynamicCapability(EmmyLuaLanguageServer server, ClientCapabilities clientCapabilities) {
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

    public TestItem(ISymbol symbol) {
        Id = symbol.GetFullName();
        Name = symbol.Name;

        var location = symbol.Locations.FirstOrDefault();
        if (location != null) {
            FilePath = location.SourceTree?.FilePath;
            Range = location.ToRange();
        }
    }
}
