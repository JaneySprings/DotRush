using System.Text.Json;
using System.Text.Json.Nodes;
using DotRush.Common;
using DotRush.Protocol;
using DotRush.Protocol.Models;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class ProtocolSerializerTests {
    private static readonly string workspaceDirectory = RuntimeInfo.IsWindows ? @"C:\workspace" : "/workspace";
    private static readonly string workspaceUri = RuntimeInfo.IsWindows ? "file:///C:/workspace" : "file:///workspace";

    [Test]
    public void LocationTest() {
        var location = new Location(FilePath("Test.cs"), new DocumentRange(new Position(1, 2), new Position(3, 4)));
        var json = @"{""uri"":""file:///workspace/Test.cs"",""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":3,""character"":4}}}";

        AssertJson(location, json);
        Assert.That(Deserialize<Location>(json), Is.EqualTo(location));
    }
    [Test]
    public void DocumentUriTest() {
        var uri = (DocumentUri)(RuntimeInfo.IsWindows ? @"C:\Projects\C#\My App\Test.cs" : "/Projects/C#/My App/Test.cs");
        var json = RuntimeInfo.IsWindows ? @"""file:///C:/Projects/C%23/My%20App/Test.cs""" : @"""file:///Projects/C%23/My%20App/Test.cs""";

        AssertJson(uri, json);
        Assert.That(Deserialize<DocumentUri>(json), Is.EqualTo(uri));
        Assert.That(Deserialize<DocumentUri>(json).FileSystemPath, Is.EqualTo(RuntimeInfo.IsWindows ? @"C:\Projects\C#\My App\Test.cs" : "/Projects/C#/My App/Test.cs"));

        var encodedUri = Deserialize<DocumentUri>(@"""file:///c%3A/Projects/Test.cs""");
        Assert.That(encodedUri.FileSystemPath, Is.EqualTo(RuntimeInfo.IsWindows ? @"c:\Projects\Test.cs" : "/c:/Projects/Test.cs"));
        Assert.Throws<JsonException>(() => Deserialize<DocumentUri>(@"""Test.cs"""));
    }
    [Test]
    public void TextDocumentParamsTest() {
        var didOpenParams = Deserialize<DidOpenTextDocumentParams>(@"{""textDocument"":{""uri"":""file:///workspace/Test.cs"",""languageId"":""csharp"",""version"":1,""text"":""class Test {}""}}");
        Assert.That(didOpenParams.TextDocument.Uri, Is.EqualTo((DocumentUri)FilePath("Test.cs")));
        Assert.That(didOpenParams.TextDocument.LanguageId, Is.EqualTo("csharp"));
        Assert.That(didOpenParams.TextDocument.Version, Is.EqualTo(1));
        Assert.That(didOpenParams.TextDocument.Text, Is.EqualTo("class Test {}"));

        var didChangeParams = Deserialize<DidChangeTextDocumentParams>(@"{""textDocument"":{""uri"":""file:///workspace/Test.cs"",""version"":2},""contentChanges"":[{""text"":""class Test2 {}""}]}");
        Assert.That(didChangeParams.TextDocument.Uri, Is.EqualTo((DocumentUri)FilePath("Test.cs")));
        Assert.That(didChangeParams.TextDocument.Version, Is.EqualTo(2));
        Assert.That(didChangeParams.ContentChanges, Has.Count.EqualTo(1));
        Assert.That(didChangeParams.ContentChanges[0].Text, Is.EqualTo("class Test2 {}"));
        Assert.That(didChangeParams.ContentChanges[0].Range, Is.Null);

        var positionParams = Deserialize<ReferenceParams>(@"{""textDocument"":{""uri"":""file:///workspace/Test.cs""},""position"":{""line"":5,""character"":10},""context"":{""includeDeclaration"":true},""workDoneToken"":""token""}");
        Assert.That(positionParams.TextDocument.Uri, Is.EqualTo((DocumentUri)FilePath("Test.cs")));
        Assert.That(positionParams.Position, Is.EqualTo(new Position(5, 10)));
        Assert.That(positionParams.Context?.IncludeDeclaration, Is.True);
    }
    [Test]
    public void ServerCapabilitiesTest() {
        var result = new InitializeResult {
            ServerInfo = new ServerInfo { Name = "DotRush" },
            Capabilities = new ServerCapabilities {
                TextDocumentSync = new TextDocumentSyncOptions { OpenClose = true, Change = TextDocumentSyncKind.Full },
                CompletionProvider = new CompletionOptions { TriggerCharacters = new List<string> { ".", "<" }, ResolveProvider = true },
                CodeActionProvider = new CodeActionOptions { CodeActionKinds = new List<CodeActionKind> { CodeActionKind.QuickFix, CodeActionKind.Refactor }, ResolveProvider = true },
                SemanticTokensProvider = new SemanticTokensOptions { Full = true, Range = true, Legend = new SemanticTokensLegend { TokenTypes = new List<string> { "class" } } },
                SignatureHelpProvider = new SignatureHelpOptions { TriggerCharacters = new List<string> { "(" } },
                HoverProvider = true,
            }
        };

        AssertJson(result, @"{""capabilities"":{
            ""textDocumentSync"":{""openClose"":true,""change"":1},
            ""completionProvider"":{""triggerCharacters"":[""."",""<""],""resolveProvider"":true},
            ""hoverProvider"":true,
            ""signatureHelpProvider"":{""triggerCharacters"":[""(""]},
            ""codeActionProvider"":{""codeActionKinds"":[""quickfix"",""refactor""],""resolveProvider"":true},
            ""semanticTokensProvider"":{""legend"":{""tokenTypes"":[""class""],""tokenModifiers"":[]},""range"":true,""full"":true}
        },""serverInfo"":{""name"":""DotRush""}}");
    }
    [Test]
    public void CompletionItemTest() {
        var item = new CompletionItem {
            Label = "List<T>",
            Kind = CompletionItemKind.Class,
            SortText = "0_List",
            Preselect = false,
            InsertTextFormat = InsertTextFormat.Snippet,
            InsertTextMode = InsertTextMode.AsIs,
            Documentation = new MarkupContent { Kind = MarkupKind.Markdown, Value = "**List**" },
            TextEdit = new TextEditOrInsertReplaceEdit(new TextEdit { NewText = "List", Range = new DocumentRange(new Position(1, 2), new Position(1, 4)) }),
            AdditionalTextEdits = new List<TextEdit> { new TextEdit { NewText = "using System;\n" } },
            CommitCharacters = new List<string> { ";" },
            Command = new Command { Title = "Handler", Name = "dotrush.completionHandler", Arguments = new List<LSPAny> { "Test.cs", true, 42, new LSPAny(new Position(1, 2)) } },
            Data = 123,
        };
        var json = @"{
            ""label"":""List<T>"",""kind"":7,""documentation"":{""kind"":""markdown"",""value"":""**List**""},""preselect"":false,""sortText"":""0_List"",
            ""insertTextFormat"":2,""insertTextMode"":1,
            ""textEdit"":{""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":4}},""newText"":""List""},
            ""additionalTextEdits"":[{""range"":{""start"":{""line"":0,""character"":0},""end"":{""line"":0,""character"":0}},""newText"":""using System;\n""}],
            ""commitCharacters"":["";""],
            ""command"":{""title"":""Handler"",""command"":""dotrush.completionHandler"",""arguments"":[""Test.cs"",true,42,{""line"":1,""character"":2}]},
            ""data"":123
        }";

        AssertJson(item, json);
        Assert.That(Serialize(item), Does.Contain(@"""label"":""List<T>"""));

        item = Deserialize<CompletionItem>(json);
        Assert.That(item.Label, Is.EqualTo("List<T>"));
        Assert.That(item.Kind, Is.EqualTo(CompletionItemKind.Class));
        Assert.That(item.Deprecated, Is.False);
        Assert.That(item.Documentation?.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(item.TextEdit?.TextEdit?.NewText, Is.EqualTo("List"));
        Assert.That(item.Command?.Name, Is.EqualTo("dotrush.completionHandler"));
        Assert.That(item.Command?.Arguments?.Select(it => it.Value).Take(3), Is.EqualTo(new object[] { "Test.cs", true, 42 }));
        Assert.That(item.Data?.Value, Is.EqualTo(123));
        AssertJson(item, json);

        // The client sends an item back for resolving with the insert and replace ranges
        item = Deserialize<CompletionItem>(@"{""label"":""Test"",""kind"":2,""deprecated"":true,""tags"":[1],""textEdit"":{""newText"":""Test"",""insert"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":3}},""replace"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}}},""data"":5}");
        Assert.That(item.Deprecated, Is.True);
        Assert.That(item.Tags, Is.EqualTo(new[] { CompletionItemTag.Deprecated }));
        Assert.That(item.TextEdit?.TextEdit, Is.Null);
        Assert.That(item.TextEdit?.InsertReplaceEdit?.Insert, Is.EqualTo(new DocumentRange(new Position(1, 2), new Position(1, 3))));
        Assert.That(item.TextEdit?.InsertReplaceEdit?.Replace, Is.EqualTo(new DocumentRange(new Position(1, 2), new Position(1, 6))));
        AssertJson(item, @"{""label"":""Test"",""kind"":2,""deprecated"":true,""tags"":[1],""textEdit"":{""newText"":""Test"",""insert"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":3}},""replace"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}}},""data"":5}");
    }
    [Test]
    public void CompletionListTest() {
        var list = new CompletionList {
            IsIncomplete = true,
            Items = new List<CompletionItem> { new CompletionItem { Label = "Test", Kind = CompletionItemKind.Method, TextEditText = "Test" } },
            ItemDefaults = new CompletionItemDefaults {
                InsertTextMode = InsertTextMode.AsIs,
                EditRange = new EditRangeWithInsertReplace {
                    Insert = new DocumentRange(new Position(1, 2), new Position(1, 3)),
                    Replace = new DocumentRange(new Position(1, 2), new Position(1, 6)),
                }
            }
        };

        AssertJson(list, @"{""isIncomplete"":true,
            ""itemDefaults"":{""editRange"":{""insert"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":3}},""replace"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}}},""insertTextMode"":1},
            ""items"":[{""label"":""Test"",""kind"":2,""textEditText"":""Test""}]}");

        var completionParams = Deserialize<CompletionParams>(@"{""textDocument"":{""uri"":""file:///workspace/Test.cs""},""position"":{""line"":1,""character"":2},""context"":{""triggerKind"":2,""triggerCharacter"":"".""}}");
        Assert.That(completionParams.Context?.TriggerKind, Is.EqualTo(CompletionTriggerKind.TriggerCharacter));
        Assert.That(completionParams.Context?.TriggerCharacter, Is.EqualTo("."));
    }
    [Test]
    public void CodeActionTest() {
        // Diagnostics of other extensions are not used and must not break the request
        var codeActionParams = Deserialize<CodeActionParams>(@"{""textDocument"":{""uri"":""file:///workspace/Test.cs""},""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},
            ""context"":{""diagnostics"":[{""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},""message"":""Typo"",""code"":1001,""severity"":2,""source"":""spell""}],""only"":[""quickfix"",""refactor.extract""],""triggerKind"":1}}");
        Assert.That(codeActionParams.Range, Is.EqualTo(new DocumentRange(new Position(1, 2), new Position(1, 6))));
        Assert.That(codeActionParams.Context?.Only, Is.EqualTo(new[] { CodeActionKind.QuickFix, CodeActionKind.RefactorExtract }));
        Assert.That(codeActionParams.Context?.TriggerKind, Is.EqualTo(CodeActionTriggerKind.Invoked));

        var codeAction = new CodeAction { Title = "Remove unused variable", Kind = CodeActionKind.QuickFix, IsPreferred = true, Data = 42 };
        var json = @"{""title"":""Remove unused variable"",""kind"":""quickfix"",""isPreferred"":true,""data"":42}";
        AssertJson(codeAction, json);

        codeAction = Deserialize<CodeAction>(json);
        Assert.That(codeAction.Title, Is.EqualTo("Remove unused variable"));
        Assert.That(codeAction.Kind, Is.EqualTo(CodeActionKind.QuickFix));
        Assert.That(codeAction.Data?.Value, Is.EqualTo(42));
    }
    [Test]
    public void WorkspaceEditTest() {
        var edit = new WorkspaceEdit {
            DocumentChanges = new List<IDocumentChange> {
                new TextDocumentEdit {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier(FilePath("Test.cs"), null),
                    Edits = new List<TextEdit> { new TextEdit { NewText = "Test", Range = new DocumentRange(new Position(1, 2), new Position(1, 6)) } }
                },
                new CreateFile(FilePath("New.cs"), new CreateFileOptions(Overwrite: true, IgnoreIfExists: false), null),
                new RenameFile(FilePath("Old.cs"), FilePath("Renamed.cs"), null, null),
                new DeleteFile(FilePath("Old.cs"), new DeleteFileOptions(Recursive: false, IgnoreIfNotExists: true), null),
            }
        };
        var json = @"{""documentChanges"":[
            {""textDocument"":{""uri"":""file:///workspace/Test.cs"",""version"":null},""edits"":[{""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},""newText"":""Test""}]},
            {""kind"":""create"",""uri"":""file:///workspace/New.cs"",""options"":{""overwrite"":true,""ignoreIfExists"":false}},
            {""kind"":""rename"",""oldUri"":""file:///workspace/Old.cs"",""newUri"":""file:///workspace/Renamed.cs""},
            {""kind"":""delete"",""uri"":""file:///workspace/Old.cs"",""options"":{""recursive"":false,""ignoreIfNotExists"":true}}
        ]}";

        AssertJson(edit, json);

        edit = Deserialize<WorkspaceEdit>(json);
        Assert.That(edit.DocumentChanges?.Select(it => it.GetType()), Is.EqualTo(new[] { typeof(TextDocumentEdit), typeof(CreateFile), typeof(RenameFile), typeof(DeleteFile) }));
        AssertJson(edit, json);

        edit = new WorkspaceEdit {
            Changes = new Dictionary<DocumentUri, List<TextEdit>> {
                { FilePath("Test.cs"), new List<TextEdit> { new TextEdit { NewText = "Test" } } }
            }
        };
        AssertJson(edit, @"{""changes"":{""file:///workspace/Test.cs"":[{""range"":{""start"":{""line"":0,""character"":0},""end"":{""line"":0,""character"":0}},""newText"":""Test""}]}}");
    }
    [Test]
    public void DiagnosticsTest() {
        var diagnosticsParams = new PublishDiagnosticsParams {
            Uri = FilePath("Test.cs"),
            Diagnostics = new List<Diagnostic> {
                new Diagnostic {
                    Range = new DocumentRange(new Position(1, 2), new Position(1, 6)),
                    Severity = DiagnosticSeverity.Warning,
                    Code = "CS0219",
                    CodeDescription = new CodeDescription(new Uri("https://msdn.microsoft.com/query/roslyn.query?appId=roslyn&k=k(CS0219)")),
                    Source = "Test",
                    Message = "The variable 'a' is assigned but its value is never used",
                }
            }
        };

        AssertJson(diagnosticsParams, @"{""uri"":""file:///workspace/Test.cs"",""diagnostics"":[{
            ""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},""severity"":2,""code"":""CS0219"",
            ""codeDescription"":{""href"":""https://msdn.microsoft.com/query/roslyn.query?appId=roslyn&k=k(CS0219)""},
            ""source"":""Test"",""message"":""The variable 'a' is assigned but its value is never used""
        }]}");
    }
    [Test]
    public void HoverAndSymbolsTest() {
        AssertJson(new Hover { Contents = new MarkupContent { Kind = MarkupKind.PlainText, Value = "Test" } }, @"{""contents"":{""kind"":""plaintext"",""value"":""Test""}}");
        AssertJson(new DocumentSymbol { Name = "Test", Kind = SymbolKind.Class, Children = new List<DocumentSymbol>() },
            @"{""name"":""Test"",""kind"":5,""range"":{""start"":{""line"":0,""character"":0},""end"":{""line"":0,""character"":0}},""selectionRange"":{""start"":{""line"":0,""character"":0},""end"":{""line"":0,""character"":0}},""children"":[]}");
        AssertJson(new WorkspaceSymbol { Name = "Test", Kind = SymbolKind.Method, Location = new Location(FilePath("Test.cs"), default) },
            @"{""name"":""Test"",""kind"":6,""location"":{""uri"":""file:///workspace/Test.cs"",""range"":{""start"":{""line"":0,""character"":0},""end"":{""line"":0,""character"":0}}}}");
        AssertJson(new FoldingRange { StartLine = 1, StartCharacter = 2, EndLine = 3, EndCharacter = 4, CollapsedText = "..." }, @"{""startLine"":1,""startCharacter"":2,""endLine"":3,""endCharacter"":4,""collapsedText"":""...""}");
        AssertJson(new InlayHint { Position = new Position(1, 2), Label = "name: ", PaddingRight = false }, @"{""position"":{""line"":1,""character"":2},""label"":""name: "",""paddingRight"":false}");
        AssertJson(new SemanticTokens { Data = new List<uint> { 1, 2, 3, 4, 0 } }, @"{""data"":[1,2,3,4,0]}");
        AssertJson(new SignatureHelp(), @"{""signatures"":[]}");
        AssertJson(new SignatureHelp {
            Signatures = new List<SignatureInformation> { new SignatureInformation { Label = "void Test(int a)", Parameters = new List<ParameterInformation> { new ParameterInformation { Label = "int a" } }, ActiveParameter = 0 } },
            ActiveSignature = 0,
            ActiveParameter = 0,
        }, @"{""signatures"":[{""label"":""void Test(int a)"",""parameters"":[{""label"":""int a""}],""activeParameter"":0}],""activeSignature"":0,""activeParameter"":0}");
    }
    [Test]
    public void TypeHierarchyItemTest() {
        var json = @"{""name"":""Test"",""kind"":5,""detail"":""Tests.Test"",""uri"":""file:///workspace/Test.cs"",
            ""range"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},""selectionRange"":{""start"":{""line"":1,""character"":2},""end"":{""line"":1,""character"":6}},""data"":-1024}";
        var supertypesParams = Deserialize<TypeHierarchySupertypesParams>($@"{{""item"":{json}}}");

        Assert.That(supertypesParams.Item.Name, Is.EqualTo("Test"));
        Assert.That(supertypesParams.Item.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(supertypesParams.Item.Data?.Value, Is.EqualTo(-1024));
        AssertJson(supertypesParams.Item, json);
    }

    private static string FilePath(string name) {
        return Path.Combine(workspaceDirectory, name);
    }
    private static string Serialize(object value) {
        return JsonSerializer.Serialize(value, value.GetType(), ProtocolSerializer.Options);
    }
    private static T Deserialize<T>(string json) {
        return JsonSerializer.Deserialize<T>(json.Replace("file:///workspace", workspaceUri), ProtocolSerializer.Options)!;
    }
    private static void AssertJson(object value, string expectedJson) {
        var json = Serialize(value);
        Assert.That(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(expectedJson.Replace("file:///workspace", workspaceUri))), Is.True, json);
    }
}
