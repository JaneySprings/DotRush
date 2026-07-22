using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Completion;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Kind;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using NUnit.Framework;
using CompletionExtensions = DotRush.Roslyn.Server.Extensions.CompletionExtensions;

namespace DotRush.Roslyn.Server.Tests;

public class CompletionV2HandlerMock : CompletionV2Handler {
    public CompletionV2HandlerMock(WorkspaceService workspaceService, ConfigurationService configurationService) : base(workspaceService, configurationService) { }

    public new Task<CompletionResponse?> Handle(CompletionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
    public new Task<CompletionItem> Resolve(CompletionItem item, CancellationToken token) {
        return base.Resolve(item, token);
    }
}

public class CompletionV2HandlerTests : MultitargetProjectFixture {
    private ConfigurationService configurationService;
    private CompletionV2HandlerMock handler;

    [SetUp]
    public void SetUp() {
        configurationService = new ConfigurationService(null);
        handler = new CompletionV2HandlerMock(Workspace, configurationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
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

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(5, 25, 5, 26)));
        Assert.That(result.List.ItemDefaults.CommitCharacters, Is.EquivalentTo(CompletionExtensions.DefaultCommitCharacters));
        Assert.That(result.List.Items, Has.Count.EqualTo(483));

        var preselect = result.List.Items.Where(it => it.Preselect == true).FirstOrDefault();
        Assert.That(preselect, Is.Not.Null);
        Assert.That(preselect.CommitCharacters, Is.EquivalentTo([" ", "(", "[", "{", ";", "."]));
        Assert.That(preselect.Label, Is.EqualTo("MyClass1"));
        Assert.That(preselect.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(preselect.SortText, Is.EqualTo("0_MyClass1"));
        Assert.That(preselect.FilterText, Is.EqualTo("MyClass1"));
        Assert.That(preselect.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(preselect.TextEditText, Is.Null.Or.Empty);
        Assert.That(preselect.Data, Is.Not.Null);

        var resolve = await handler.Resolve(preselect, CancellationToken.None);
        Assert.That(resolve.Label, Is.EqualTo("MyClass1"));
        Assert.That(resolve.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(resolve.Documentation, Is.Not.Null);
        Assert.That(resolve.SortText, Is.EqualTo("0_MyClass1"));
        Assert.That(resolve.FilterText, Is.EqualTo("MyClass1"));
        Assert.That(resolve.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(resolve.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(resolve.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(resolve.Command, Is.Null);
    }

    [Test]
    public async Task HandleAutoImportTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
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

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(5, 8, 5, 15)));
        Assert.That(result.List.Items, Has.Count.EqualTo(3439));

        var autoUsingItem = result.List.Items.FirstOrDefault(it => it.Label == "JsonSerializer");
        Assert.That(autoUsingItem, Is.Not.Null);
        Assert.That(autoUsingItem.Label, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.Detail, Is.EqualTo("System.Text.Json"));
        Assert.That(autoUsingItem.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(autoUsingItem.SortText, Is.EqualTo("~JsonSerializer  System.Text.Json"));
        Assert.That(autoUsingItem.FilterText, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(autoUsingItem.TextEditText, Is.Null.Or.Empty);
        Assert.That(autoUsingItem.Data, Is.Not.Null);

        autoUsingItem = await handler.Resolve(autoUsingItem, CancellationToken.None);
        Assert.That(autoUsingItem.Label, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(autoUsingItem.Documentation, Is.Not.Null);
        Assert.That(autoUsingItem.SortText, Is.EqualTo("~JsonSerializer  System.Text.Json"));
        Assert.That(autoUsingItem.FilterText, Is.EqualTo("JsonSerializer"));
        Assert.That(autoUsingItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(autoUsingItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(autoUsingItem.AdditionalTextEdits, Has.Count.EqualTo(1));
        Assert.That(autoUsingItem.AdditionalTextEdits[0].NewText.ToLF(), Is.EqualTo("using System.Text.Json;\n\n"));
        Assert.That(autoUsingItem.AdditionalTextEdits[0].Range, Is.EqualTo(PositionExtensions.CreateRange(1, 0, 1, 0)));
        Assert.That(autoUsingItem.Command, Is.Null);
    }
    [Test]
    public async Task HandleOverridesTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 {
    override Eq
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 15),
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(4, 13, 4, 15)));
        Assert.That(result.List.ItemDefaults.CommitCharacters, Is.EqualTo(CompletionExtensions.DefaultCommitCharacters));
        Assert.That(result.List.Items, Has.Count.EqualTo(3));

        var equalsOvrItem = result.List.Items.FirstOrDefault(it => it.Label == "Equals(object? obj)");
        Assert.That(equalsOvrItem, Is.Not.Null);
        Assert.That(equalsOvrItem.CommitCharacters, Is.EqualTo(["("]));
        Assert.That(equalsOvrItem.Label, Is.EqualTo("Equals(object? obj)"));
        Assert.That(equalsOvrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(equalsOvrItem.SortText, Is.EqualTo("Equals")); //0000Equals
        Assert.That(equalsOvrItem.FilterText, Is.EqualTo("Equals"));
        Assert.That(equalsOvrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(equalsOvrItem.TextEditText, Is.EqualTo("Eq"));
        Assert.That(equalsOvrItem.Data, Is.Not.Null);

        equalsOvrItem = await handler.Resolve(equalsOvrItem, CancellationToken.None);
        Assert.That(equalsOvrItem.Label, Is.EqualTo("Equals(object? obj)"));
        Assert.That(equalsOvrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(equalsOvrItem.Documentation, Is.Not.Null);
        Assert.That(equalsOvrItem.SortText, Is.EqualTo("Equals")); //0000Equals
        Assert.That(equalsOvrItem.FilterText, Is.EqualTo("Equals"));
        Assert.That(equalsOvrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(equalsOvrItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(equalsOvrItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(equalsOvrItem.Command, Is.Not.Null);
        Assert.That(equalsOvrItem.Command.Title, Is.EqualTo(nameof(CompletionV2Handler)));
        Assert.That(equalsOvrItem.Command.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(equalsOvrItem.Command.Arguments, Has.Count.EqualTo(4));
        Assert.That(equalsOvrItem.Command.Arguments[0].Value, Is.TypeOf<string>());
        Assert.That(equalsOvrItem.Command.Arguments[1].Value, Is.TypeOf<TextEdit>());
        Assert.That(equalsOvrItem.Command.Arguments[2].Value, Is.TypeOf<bool>());
        Assert.That(equalsOvrItem.Command.Arguments[3].Value, Is.TypeOf<int>());
        var argument0 = equalsOvrItem.Command.Arguments[0].Value as string;
        var argument1 = equalsOvrItem.Command.Arguments[1].Value as TextEdit;
        var argument2 = equalsOvrItem.Command.Arguments[2].Value as bool?;
        var argument3 = equalsOvrItem.Command.Arguments[3].Value as int?;
        Assert.That(argument0, Is.EqualTo(documentPath));
        Assert.That(argument1!.NewText.ToLF(), Is.EqualTo("public override bool Equals(object? obj)\n    {\n        return base.Equals(obj);\n    }"));
        Assert.That(argument1.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 4, 15)));
        Assert.That(argument2!.Value, Is.False);
        Assert.That(argument3!.Value, Is.EqualTo(OnPlatform(119, 125)));
    }
    [Test]
    public async Task HandleOverridesWithAutoImportTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 : MyBase {
    override MyM
}
class MyBase {
    protected virtual void MyMethod(System.Text.Json.JsonSerializerOptions obj) { }
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
            Position = PositionExtensions.CreatePosition(4, 16),
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(4, 13, 4, 16)));
        Assert.That(result.List.Items, Has.Count.EqualTo(4));

        var ovrItem = result.List.Items.FirstOrDefault(it => it.Label.StartsWith("MyMethod"));
        Assert.That(ovrItem, Is.Not.Null);
        Assert.That(ovrItem.Label, Is.EqualTo("MyMethod(System.Text.Json.JsonSerializerOptions obj)"));
        Assert.That(ovrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(ovrItem.SortText, Is.EqualTo("MyMethod"));
        Assert.That(ovrItem.FilterText, Is.EqualTo("MyMethod"));
        Assert.That(ovrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(ovrItem.TextEditText, Is.EqualTo("MyM"));
        Assert.That(ovrItem.Data, Is.Not.Null);

        ovrItem = await handler.Resolve(ovrItem, CancellationToken.None);
        Assert.That(ovrItem.Label, Is.EqualTo("MyMethod(System.Text.Json.JsonSerializerOptions obj)"));
        Assert.That(ovrItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(ovrItem.Documentation, Is.Not.Null);
        Assert.That(ovrItem.SortText, Is.EqualTo("MyMethod"));
        Assert.That(ovrItem.FilterText, Is.EqualTo("MyMethod"));
        Assert.That(ovrItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(ovrItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(ovrItem.AdditionalTextEdits, Has.Count.EqualTo(1));
        Assert.That(ovrItem.AdditionalTextEdits[0].NewText.ToLF(), Is.EqualTo("using System.Text.Json;\n\n"));
        Assert.That(ovrItem.AdditionalTextEdits[0].Range, Is.EqualTo(PositionExtensions.CreateRange(1, 0, 1, 0)));
        Assert.That(ovrItem.Command, Is.Not.Null);
        Assert.That(ovrItem.Command.Title, Is.EqualTo(nameof(CompletionV2Handler)));
        Assert.That(ovrItem.Command.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(ovrItem.Command.Arguments, Has.Count.EqualTo(4));
        Assert.That(ovrItem.Command.Arguments[0].Value, Is.TypeOf<string>());
        Assert.That(ovrItem.Command.Arguments[1].Value, Is.TypeOf<TextEdit>());
        Assert.That(ovrItem.Command.Arguments[2].Value, Is.TypeOf<bool>());
        Assert.That(ovrItem.Command.Arguments[3].Value, Is.TypeOf<int>());
        var argument0 = ovrItem.Command.Arguments[0].Value as string;
        var argument1 = ovrItem.Command.Arguments[1].Value as TextEdit;
        var argument2 = ovrItem.Command.Arguments[2].Value as bool?;
        var argument3 = ovrItem.Command.Arguments[3].Value as int?;
        Assert.That(argument0, Is.EqualTo(documentPath));
        Assert.That(argument1!.NewText.ToLF(), Is.EqualTo("protected override void MyMethod(JsonSerializerOptions obj)\n    {\n        base.MyMethod(obj);\n    }"));
        Assert.That(argument1.Range, Is.EqualTo(PositionExtensions.CreateRange(6, 4, 6, 16))); // Since additionalEdit adds two new lines, textChange shiffted to next lines
        Assert.That(argument2!.Value, Is.False);
        Assert.That(argument3!.Value, Is.Not.Zero);
    }
    [Test]
    public async Task HandleExplicitInterfaceTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 : IInterface {
    bool IInterface.My
}
interface IInterface {
    bool MyMethod();
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 22),
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(4, 20, 4, 22)));
        Assert.That(result.List.Items, Has.Count.EqualTo(1));

        var ifaceImplItem = result.List.Items.FirstOrDefault(it => it.Label == "MyMethod()");
        Assert.That(ifaceImplItem, Is.Not.Null);
        Assert.That(ifaceImplItem.Label, Is.EqualTo("MyMethod()"));
        Assert.That(ifaceImplItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(ifaceImplItem.SortText, Is.EqualTo("MyMethod"));
        Assert.That(ifaceImplItem.FilterText, Is.EqualTo("MyMethod"));
        Assert.That(ifaceImplItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(ifaceImplItem.TextEditText, Is.EqualTo("My"));
        Assert.That(ifaceImplItem.Data, Is.Not.Null);

        ifaceImplItem = await handler.Resolve(ifaceImplItem, CancellationToken.None);
        Assert.That(ifaceImplItem.Label, Is.EqualTo("MyMethod()"));
        Assert.That(ifaceImplItem.Kind, Is.EqualTo(CompletionItemKind.Method));
        Assert.That(ifaceImplItem.Documentation, Is.Not.Null);
        Assert.That(ifaceImplItem.SortText, Is.EqualTo("MyMethod"));
        Assert.That(ifaceImplItem.FilterText, Is.EqualTo("MyMethod"));
        Assert.That(ifaceImplItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.PlainText));
        Assert.That(ifaceImplItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(ifaceImplItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(ifaceImplItem.Command, Is.Not.Null);
        Assert.That(ifaceImplItem.Command.Title, Is.EqualTo(nameof(CompletionV2Handler)));
        Assert.That(ifaceImplItem.Command.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(ifaceImplItem.Command.Arguments, Has.Count.EqualTo(4));
        Assert.That(ifaceImplItem.Command.Arguments[0].Value, Is.TypeOf<string>());
        Assert.That(ifaceImplItem.Command.Arguments[1].Value, Is.TypeOf<TextEdit>());
        Assert.That(ifaceImplItem.Command.Arguments[2].Value, Is.TypeOf<bool>());
        Assert.That(ifaceImplItem.Command.Arguments[3].Value, Is.TypeOf<int>());
        var argument0 = ifaceImplItem.Command.Arguments[0].Value as string;
        var argument1 = ifaceImplItem.Command.Arguments[1].Value as TextEdit;
        var argument2 = ifaceImplItem.Command.Arguments[2].Value as bool?;
        var argument3 = ifaceImplItem.Command.Arguments[3].Value as int?;
        Assert.That(argument0, Is.EqualTo(documentPath));
        Assert.That(argument1!.NewText.ToLF(), Is.EqualTo("Method()\n    {\n        throw new NotImplementedException();\n    }"));
        Assert.That(argument1.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 22, 4, 22)));
        Assert.That(argument2!.Value, Is.False);
        Assert.That(argument3!.Value, Is.EqualTo(OnPlatform(130, 136)));
    }
    [Test]
    public async Task HandleSimpleSnippetTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 : IInterface {
    pro
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 7),
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.False);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 4, 7)));
        Assert.That(result.List.Items, Has.Count.EqualTo(521));

        var snippetItem = result.List.Items.FirstOrDefault(it => it.Label == "prop");
        Assert.That(snippetItem, Is.Not.Null);
        Assert.That(snippetItem.Label, Is.EqualTo("prop"));
        Assert.That(snippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(snippetItem.SortText, Is.EqualTo("prop "));
        Assert.That(snippetItem.FilterText, Is.EqualTo("prop"));
        Assert.That(snippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(snippetItem.TextEditText, Is.EqualTo("pro"));
        Assert.That(snippetItem.Data, Is.Not.Null);

        snippetItem = await handler.Resolve(snippetItem, CancellationToken.None);
        Assert.That(snippetItem.Label, Is.EqualTo("prop"));
        Assert.That(snippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(snippetItem.Documentation, Is.Not.Null);
        Assert.That(snippetItem.SortText, Is.EqualTo("prop "));
        Assert.That(snippetItem.FilterText, Is.EqualTo("prop"));
        Assert.That(snippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(snippetItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(snippetItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(snippetItem.Command, Is.Not.Null);
        Assert.That(snippetItem.Command.Title, Is.EqualTo(nameof(CompletionV2Handler)));
        Assert.That(snippetItem.Command.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(snippetItem.Command.Arguments, Has.Count.EqualTo(4));
        Assert.That(snippetItem.Command.Arguments[0].Value, Is.TypeOf<string>());
        Assert.That(snippetItem.Command.Arguments[1].Value, Is.TypeOf<TextEdit>());
        Assert.That(snippetItem.Command.Arguments[2].Value, Is.TypeOf<bool>());
        Assert.That(snippetItem.Command.Arguments[3].Value, Is.TypeOf<int>());
        var argument0 = snippetItem.Command.Arguments[0].Value as string;
        var argument1 = snippetItem.Command.Arguments[1].Value as TextEdit;
        var argument2 = snippetItem.Command.Arguments[2].Value as bool?;
        var argument3 = snippetItem.Command.Arguments[3].Value as int?;
        Assert.That(argument0, Is.EqualTo(documentPath));
        Assert.That(argument1!.NewText.ToLF(), Is.EqualTo("public ${1:int} ${2:MyProperty} { get; set; }$0"));
        Assert.That(argument1.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 4, 7)));
        Assert.That(argument2!.Value, Is.True);
        Assert.That(argument3!.Value, Is.EqualTo(OnPlatform(88, 92)));
    }
    [Test]
    public async Task HandleSoftSelectionAfterTriggerCharacterTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1(bool value) {
        bool myValue = false;
        Method1(
    }
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 16),
            Context = new CompletionContext() {
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = "(",
            },
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        // Nothing typed after `(` - commit characters like `!` or ` ` must not commit the selected item
        Assert.That(result.List.IsIncomplete, Is.True);
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.CommitCharacters, Is.Null);

        var localItem = result.List.Items.FirstOrDefault(it => it.Label == "myValue");
        Assert.That(localItem, Is.Not.Null);
        Assert.That(localItem.CommitCharacters, Is.Null.Or.Empty);
    }
    [Test]
    public async Task HandleCollectionSnippetTest() {
        var documentPath = CreateDocument(nameof(CompletionV2HandlerTests), @"
namespace Tests;

class MyClass1 {
    void A() {
        int[] data = [ 1, 2, 3 ];
        data.
    }
}
");
        var result = await handler.Handle(new CompletionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 13),
        }, CancellationToken.None);

        Assert.That(result?.List, Is.Not.Null);
        Assert.That(result.List.IsIncomplete, Is.True); // nothing typed after `data.` - list must be re-requested to restore commit characters
        Assert.That(result.List.ItemDefaults, Is.Not.Null);
        Assert.That(result.List.ItemDefaults.InsertTextMode, Is.EqualTo(InsertTextMode.AsIs));
        Assert.That(result.List.ItemDefaults.EditRange?.Result1, Is.EqualTo(PositionExtensions.CreateRange(6, 13, 6, 13)));
        Assert.That(result.List.ItemDefaults.CommitCharacters, Is.Null);
        Assert.That(result.List.Items, Has.Count.EqualTo(127));

        var snippetItem = result.List.Items.FirstOrDefault(it => it.Label == "for");
        Assert.That(snippetItem, Is.Not.Null);
        Assert.That(snippetItem.Label, Is.EqualTo("for"));
        Assert.That(snippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(snippetItem.SortText, Is.EqualTo("for "));
        Assert.That(snippetItem.FilterText, Is.EqualTo("for"));
        Assert.That(snippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(snippetItem.TextEditText, Is.EqualTo(string.Empty));
        Assert.That(snippetItem.Data, Is.Not.Null);

        snippetItem = await handler.Resolve(snippetItem, CancellationToken.None);
        Assert.That(snippetItem.Label, Is.EqualTo("for"));
        Assert.That(snippetItem.Kind, Is.EqualTo(CompletionItemKind.Snippet));
        Assert.That(snippetItem.Documentation, Is.Not.Null);
        Assert.That(snippetItem.SortText, Is.EqualTo("for "));
        Assert.That(snippetItem.FilterText, Is.EqualTo("for"));
        Assert.That(snippetItem.InsertTextFormat, Is.EqualTo(InsertTextFormat.Snippet));
        Assert.That(snippetItem.TextEdit, Is.Null); // vscode provide calculated textEdit here by itemsDefault
        Assert.That(snippetItem.AdditionalTextEdits, Is.Null.Or.Empty);
        Assert.That(snippetItem.Command, Is.Not.Null);
        Assert.That(snippetItem.Command.Title, Is.EqualTo(nameof(CompletionV2Handler)));
        Assert.That(snippetItem.Command.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(snippetItem.Command.Arguments, Has.Count.EqualTo(4));
        Assert.That(snippetItem.Command.Arguments[0].Value, Is.TypeOf<string>());
        Assert.That(snippetItem.Command.Arguments[1].Value, Is.TypeOf<TextEdit>());
        Assert.That(snippetItem.Command.Arguments[2].Value, Is.TypeOf<bool>());
        Assert.That(snippetItem.Command.Arguments[3].Value, Is.TypeOf<int>());
        var argument0 = snippetItem.Command.Arguments[0].Value as string;
        var argument1 = snippetItem.Command.Arguments[1].Value as TextEdit;
        var argument2 = snippetItem.Command.Arguments[2].Value as bool?;
        var argument3 = snippetItem.Command.Arguments[3].Value as int?;
        Assert.That(argument0, Is.EqualTo(documentPath));
        Assert.That(argument1!.NewText.ToLF(), Is.EqualTo("for (int ${1:i} = 0; ${1:i} < data.Length; ${1:i}++)\n{\n    $0\n}")); // This text includes indentation, but vscode doesn't expect it. Remove it before
        Assert.That(argument1.Range, Is.EqualTo(PositionExtensions.CreateRange(6, 8, 6, 13)));
        Assert.That(argument2!.Value, Is.True);
        Assert.That(argument3!.Value, Is.EqualTo(OnPlatform(153, 161)));
    }
}
