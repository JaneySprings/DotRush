using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Kind;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class CompletionHandlerMock : CompletionHandler {
    public CompletionHandlerMock(WorkspaceService workspaceService, ConfigurationService configurationService) : base(workspaceService, configurationService) { }

    public new Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        return base.Resolve(item, token);
    }
}

public class CompletionHandlerTests : MultitargetProjectFixture {
    private ConfigurationService configurationService;
    private CompletionHandlerMock handler;

    [SetUp]
    public void SetUp() {
        configurationService = new ConfigurationService(null);
        handler = new CompletionHandlerMock(Workspace, configurationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(CompletionHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() {
        MyClass1 a = new M
    }
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 26),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.List, Is.Null.Or.Empty);
        Assert.That(result.Items, Has.Count.EqualTo(483));

        var preselect = result.Items.Where(it => it.Preselect == true).ToArray();
        Assert.That(preselect, Has.Length.EqualTo(1));
        Assert.That(preselect[0].Label, Is.EqualTo("MyClass1"));
        Assert.That(preselect[0].FilterText, Is.EqualTo("MyClass1"));
        Assert.That(preselect[0].InsertText, Is.Null);
        Assert.That(preselect[0].TextEdit, Is.Null);
        Assert.That(preselect[0].AdditionalTextEdits, Is.Null);
        Assert.That(preselect[0].Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(preselect[0].InsertTextFormat, Is.Null);
        Assert.That(preselect[0].InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(preselect[0].Documentation, Is.Null);

        var resolve = await handler.Resolve(preselect[0], CancellationToken.None);
        Assert.That(resolve, Is.Not.Null);
        Assert.That(resolve.Label, Is.EqualTo("MyClass1"));
        Assert.That(resolve.FilterText, Is.EqualTo("MyClass1"));
        Assert.That(resolve.InsertText, Is.Null);
        Assert.That(resolve.TextEdit, Is.Null);
        Assert.That(resolve.AdditionalTextEdits, Is.Null);
        Assert.That(resolve.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(resolve.InsertTextFormat, Is.Null);
        Assert.That(resolve.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(resolve.Documentation, Is.Not.Null);
    }
    [Test]
    public async Task GeneralHandlerWithGlobalScopeTest() {
        var documentPath = CreateDocument(nameof(CompletionHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() {
        JsonSer
    }
}
");
        configurationService.ChangeConfiguration(new ConfigurationSection {
            DotRush = new DotRushSection {
                Roslyn = new RoslynSection {
                    ShowItemsFromUnimportedNamespaces = true
                }
            }
        });
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 15),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.List, Is.Null.Or.Empty);
        Assert.That(result.Items, Is.Not.Empty);

        var autoUsingItem = result.Items.FirstOrDefault(it => it.Label == "JsonSerializer");
        Assert.That(autoUsingItem, Is.Not.Null);
        Assert.That(autoUsingItem.FilterText, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.InsertText, Is.Null);
        Assert.That(autoUsingItem.TextEdit, Is.Null);
        Assert.That(autoUsingItem.AdditionalTextEdits, Is.Null);
        Assert.That(autoUsingItem.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(autoUsingItem.InsertTextFormat, Is.Null);
        Assert.That(autoUsingItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(autoUsingItem.Documentation, Is.Null);

        autoUsingItem = await handler.Resolve(autoUsingItem, CancellationToken.None);
        Assert.That(autoUsingItem, Is.Not.Null);
        Assert.That(autoUsingItem.Label, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.InsertText, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.TextEdit, Is.Not.Null);
        Assert.That(autoUsingItem.TextEdit.TextEdit?.NewText, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.AdditionalTextEdits, Has.Count.EqualTo(1));
        Assert.That(autoUsingItem.AdditionalTextEdits[0].NewText, Does.StartWith("using System.Text.Json;"));
        Assert.That(autoUsingItem.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(autoUsingItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(autoUsingItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(autoUsingItem.Documentation, Is.Not.Null);
    }

    [Test]
    public async Task ResolveOverridesWithPriorityTest() {
        var documentPath = CreateDocument(nameof(CompletionHandlerTests), @"
namespace Tests;

class MyClass1 {
    override E
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 14),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.List, Is.Null.Or.Empty);
        Assert.That(result.Items, Has.Count.EqualTo(3));

        var equalsOvrItem = result.Items.FirstOrDefault(it => it.Label == "Equals(object? obj)");
        Assert.That(equalsOvrItem, Is.Not.Null);
        Assert.That(equalsOvrItem.FilterText, Is.EqualTo("override Equals"));
        Assert.That(equalsOvrItem.InsertText, Is.EqualTo(@"public override bool Equals(object? obj)
    {
        return base.Equals(obj);$0
    \}"));
        Assert.That(equalsOvrItem.TextEdit, Is.Not.Null);
        Assert.That(equalsOvrItem.TextEdit.TextEdit?.NewText, Does.StartWith("public override bool Equals(object? obj)"));
        Assert.That(equalsOvrItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(equalsOvrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(equalsOvrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(equalsOvrItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(equalsOvrItem.Documentation, Is.Null);

        equalsOvrItem = await handler.Resolve(equalsOvrItem, CancellationToken.None);
        Assert.That(equalsOvrItem, Is.Not.Null);
        Assert.That(equalsOvrItem.Label, Is.EqualTo("Equals(object? obj)"));
        Assert.That(equalsOvrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(equalsOvrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(equalsOvrItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(equalsOvrItem.Documentation, Is.Not.Null);
    }
    [Test]
    public async Task ResolveSnippetsWithPriorityTest() {
        var documentPath = CreateDocument(nameof(CompletionHandlerTests), @"
namespace Tests;

class MyClass1 {
    void A() {
        int[] data = [ 1, 2, 3];
        data.
    }
}
");

        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 13),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.List, Is.Null.Or.Empty);
        Assert.That(result.Items, Has.Count.EqualTo(127));

        var forSnippetItem = result.Items.FirstOrDefault(it => it.Label == "for");
        Assert.That(forSnippetItem, Is.Not.Null);
        Assert.That(forSnippetItem.FilterText, Is.EqualTo("data.for"));
        Assert.That(forSnippetItem.InsertText, Does.StartWith(@"for (int i = 0; i < data.Length; i++)"));
        Assert.That(forSnippetItem.TextEdit, Is.Not.Null);
        Assert.That(forSnippetItem.TextEdit.TextEdit?.NewText, Is.EqualTo(@"for (int i = 0; i < data.Length; i++)
        {
            $0
        \}"));
        Assert.That(forSnippetItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(forSnippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(forSnippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(forSnippetItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(forSnippetItem.Documentation, Is.Null);

        forSnippetItem = await handler.Resolve(forSnippetItem, CancellationToken.None);
        Assert.That(forSnippetItem, Is.Not.Null);
        Assert.That(forSnippetItem.Label, Is.EqualTo("for"));
        Assert.That(forSnippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(forSnippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(forSnippetItem.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(forSnippetItem.Documentation, Is.Not.Null);
    }
}
