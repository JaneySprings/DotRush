using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class DocumentSymbolHandlerMock : DocumentSymbolHandler {
    public DocumentSymbolHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<List<DocumentSymbol>> Handle(DocumentSymbolParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class DocumentSymbolHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private DocumentSymbolHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService(Workspace);
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
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Tests"));
        Assert.That(result[0].Kind, Is.EqualTo(SymbolKind.Namespace));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(1, 0, 10, 1)));
        Assert.That(result[0].Children, Has.Count.EqualTo(1));

        var class1 = result[0].Children.FirstOrDefault(x => x.Name == "Class1");
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
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Children, Has.Count.EqualTo(1));

        var class1 = result[0].Children.FirstOrDefault(x => x.Name == "Class1");
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

    [Test]
    public async Task RecordDeclarationsTest() {
        var documentPath = CreateDocument(nameof(DocumentSymbolHandlerTests), @"
namespace Tests;

record Record1(int Value) {
    public int Property1 { get; set; }
}
record struct Record2(int Value);
");
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result[0].Children, Has.Count.EqualTo(2));

        var record1 = result[0].Children.FirstOrDefault(x => x.Name == "Record1");
        Assert.That(record1, Is.Not.Null);
        Assert.That(record1.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(record1.Children, Has.Count.EqualTo(1));
        Assert.That(record1.Children[0].Name, Is.EqualTo("Property1"));

        var record2 = result[0].Children.FirstOrDefault(x => x.Name == "Record2");
        Assert.That(record2, Is.Not.Null);
        Assert.That(record2.Kind, Is.EqualTo(SymbolKind.Struct));
    }

    [Test]
    public async Task EventFieldDeclarationsTest() {
        var documentPath = CreateDocument(nameof(DocumentSymbolHandlerTests), @"
namespace Tests;

class Class1 {
    public event Action Event1, Event2;
}
");
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result[0].Children, Has.Count.EqualTo(1));

        var class1 = result[0].Children[0];
        Assert.That(class1.Children, Has.Count.EqualTo(2));
        Assert.That(class1.Children.Select(x => x.Name), Is.EqualTo(new[] { "Event1", "Event2" }));
        Assert.That(class1.Children.Select(x => x.Kind), Is.All.EqualTo(SymbolKind.Event));
    }

    [Test]
    public async Task OperatorDeclarationsTest() {
        var documentPath = CreateDocument(nameof(DocumentSymbolHandlerTests), @"
namespace Tests;

class Class1 {
    public static Class1 operator +(Class1 a, Class1 b) => a;
    public static implicit operator int(Class1 a) => 0;
    ~Class1() { }
}
");
        var result = await handler.Handle(new DocumentSymbolParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result[0].Children, Has.Count.EqualTo(1));

        var class1 = result[0].Children[0];
        Assert.That(class1.Children, Has.Count.EqualTo(3));

        var binaryOperator = class1.Children.FirstOrDefault(x => x.Name == "operator +(Class1, Class1)");
        Assert.That(binaryOperator, Is.Not.Null);
        Assert.That(binaryOperator.Kind, Is.EqualTo(SymbolKind.Operator));

        var conversionOperator = class1.Children.FirstOrDefault(x => x.Name == "implicit operator int(Class1)");
        Assert.That(conversionOperator, Is.Not.Null);
        Assert.That(conversionOperator.Kind, Is.EqualTo(SymbolKind.Operator));

        var destructor = class1.Children.FirstOrDefault(x => x.Name == "~Class1()");
        Assert.That(destructor, Is.Not.Null);
        Assert.That(destructor.Kind, Is.EqualTo(SymbolKind.Method));
    }
}
