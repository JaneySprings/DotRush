using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class DocumentSymbolHandlerMock : DocumentSymbolHandler {
    public DocumentSymbolHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<DocumentSymbolResponse> Handle(DocumentSymbolParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class DocumentSymbolHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private DocumentSymbolHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new DocumentSymbolHandlerMock(navigationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(DocumentSymbolHandlerTests), @"
namespace Tests;

class Class1 {
    private int field1;
    public int Property1 { get; set; }
    private void Method1(int a, bool b) {}
    protected Class1(int c) {
        field1 = c;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result1, Has.Count.EqualTo(1));
        Assert.That(result.Result1[0].Name, Is.EqualTo("Tests"));
        Assert.That(result.Result1[0].Kind, Is.EqualTo(SymbolKind.Namespace));
        Assert.That(result.Result1[0].Range, Is.EqualTo(PositionExtensions.CreateRange(1, 0, 10, 1)));
        Assert.That(result.Result1[0].Children, Has.Count.EqualTo(1));

        var class1 = result.Result1[0].Children.FirstOrDefault(x => x.Name == "Class1");
        Assert.That(class1, Is.Not.Null);
        Assert.That(class1.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(class1.Range, Is.EqualTo(PositionExtensions.CreateRange(3, 0, 10, 1)));
        Assert.That(class1.Children, Has.Count.EqualTo(4));

        var field1 = class1.Children.FirstOrDefault(x => x.Name == "field1");
        Assert.That(field1, Is.Not.Null);
        Assert.That(field1.Kind, Is.EqualTo(SymbolKind.Field));
        Assert.That(field1.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 4, 4, 23)));
        Assert.That(field1.Children, Is.Null.Or.Empty);

        var property1 = class1.Children.FirstOrDefault(x => x.Name == "Property1");
        Assert.That(property1, Is.Not.Null);
        Assert.That(property1.Kind, Is.EqualTo(SymbolKind.Property));
        Assert.That(property1.Range, Is.EqualTo(PositionExtensions.CreateRange(5, 4, 5, 38)));
        Assert.That(property1.Children, Is.Null.Or.Empty);

        var method1 = class1.Children.FirstOrDefault(x => x.Name == "Method1(int, bool)");
        Assert.That(method1, Is.Not.Null);
        Assert.That(method1.Kind, Is.EqualTo(SymbolKind.Method));
        Assert.That(method1.Range, Is.EqualTo(PositionExtensions.CreateRange(6, 4, 6, 42)));
        Assert.That(method1.Children, Is.Null.Or.Empty);

        var constructor = class1.Children.FirstOrDefault(x => x.Name == "Class1(int)");
        Assert.That(constructor, Is.Not.Null);
        Assert.That(constructor.Kind, Is.EqualTo(SymbolKind.Constructor));
        Assert.That(constructor.Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 9, 5)));
        Assert.That(constructor.Children, Is.Null.Or.Empty);
    }

    [Test]
    public async Task CorrectRangeWithAttributesTest() {
        var documentPath = CreateDocument(nameof(DocumentSymbolHandlerTests), @"
namespace Tests;

[Serializable]
class Class1 {
    [Serializable] public int Property1 { get; set; }
    [Serializable]
    private void Method1() {
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result1, Has.Count.EqualTo(1));
        Assert.That(result.Result1[0].Children, Has.Count.EqualTo(1));

        var class1 = result.Result1[0].Children.FirstOrDefault(x => x.Name == "Class1");
        Assert.That(class1, Is.Not.Null);
        Assert.That(class1.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(class1.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 0, 9, 1)));
        Assert.That(class1.Children, Has.Count.EqualTo(2));

        var property1 = class1.Children.FirstOrDefault(x => x.Name == "Property1");
        Assert.That(property1, Is.Not.Null);
        Assert.That(property1.Kind, Is.EqualTo(SymbolKind.Property));
        Assert.That(property1.Range, Is.EqualTo(PositionExtensions.CreateRange(5, 4, 5, 53)));
        Assert.That(property1.Children, Is.Null.Or.Empty);

        var method1 = class1.Children.FirstOrDefault(x => x.Name == "Method1()");
        Assert.That(method1, Is.Not.Null);
        Assert.That(method1.Kind, Is.EqualTo(SymbolKind.Method));
        Assert.That(method1.Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 8, 5)));
        Assert.That(method1.Children, Is.Null.Or.Empty);
    }
}