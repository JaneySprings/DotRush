using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SignatureHelp;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class SignatureHelpHandlerMock : SignatureHelpHandler {
    public SignatureHelpHandlerMock(WorkspaceService workspaceService) : base(workspaceService) { }

    public new Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class SignatureHelpHandlerTests : MultitargetProjectFixture {
    private SignatureHelpHandlerMock handler;

    [SetUp]
    public void SetUp() {
        handler = new SignatureHelpHandlerMock(Workspace);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() { }
    private void Method1(bool value) { }
    private void Method1(string value, int count) { }
    private void Test() {
        Method1();
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 16),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(3));
        Assert.That(result.Signatures.Select(it => it.Label), Is.EquivalentTo([
            "void MyClass1.Method1()",
            "void MyClass1.Method1(bool value)",
            "void MyClass1.Method1(string value, int count)"
        ]));

        // Compiler binds `Method1()` to the parameterless overload
        var boundIndex = result.Signatures.FindIndex(it => it.Label == "void MyClass1.Method1()");
        Assert.That(result.ActiveSignature, Is.EqualTo((uint)boundIndex));
        Assert.That(result.ActiveParameter, Is.EqualTo(0));

        var signature = result.Signatures.First(it => it.Label == "void MyClass1.Method1(string value, int count)");
        Assert.That(signature.Parameters.Select(it => it.Label.Result1).ToList(), Is.EqualTo(new List<string> { "string value", "int count" }));
    }

    [Test]
    public async Task OverloadFilterByArgumentsCountTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method1() { }
    private void Method1(bool value) { }
    private void Method1(string value, int count) { }
    private void Test() {
        Method1(""text"", );
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 24),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.Signatures[0].Label, Is.EqualTo("void MyClass1.Method1(string value, int count)"));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task OverloadSelectionByArgumentTypeTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method2(bool value, int count) { }
    private void Method2(string value, int count) { }
    private void Test() {
        Method2(false, );
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 23),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(2));

        var expectedIndex = result.Signatures.FindIndex(it => it.Label == "void MyClass1.Method2(bool value, int count)");
        Assert.That(result.ActiveSignature, Is.EqualTo((uint)expectedIndex));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task BoundOverloadSelectionTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method2(bool value, int count) { }
    private void Method2(string value, int count) { }
    private void Test() {
        Method2(""text"", 1);
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 16),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(2));

        var expectedIndex = result.Signatures.FindIndex(it => it.Label == "void MyClass1.Method2(string value, int count)");
        Assert.That(result.ActiveSignature, Is.EqualTo((uint)expectedIndex));
        Assert.That(result.ActiveParameter, Is.EqualTo(0));
    }

    [Test]
    public async Task ParamsArrayTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method3(params int[] values) { }
    private void Test() {
        Method3(1, 2, 3, );
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 25),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.Signatures[0].Label, Is.EqualTo("void MyClass1.Method3(params int[] values)"));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(0)); // all arguments fall into the `params` parameter
    }

    [Test]
    public async Task NamedArgumentTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method4(string name, int count) { }
    private void Test() {
        Method4(count: 5);
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 24),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task ConstructorTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Test() {
        var a = new MyClass2(""text"", );
    }
}
class MyClass2 {
    public MyClass2(int value) { }
    public MyClass2(string value, bool flag) { }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 37),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.Signatures[0].Label, Does.Contain("MyClass2(string value, bool flag)"));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task AttributeTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

[Obsolete(""message"", )]
class MyClass1 { }
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 21),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.Signatures[0].Label, Does.Contain("ObsoleteAttribute").And.Contain("bool error"));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleConditionalDirectivesTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
#if !NET8_0
    private void Method6(bool value, int count) { }
#endif
    private void Test() {
        Method6(true, );
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 22),
        }, CancellationToken.None);

        // Method6 does not resolve in the net8.0 document - the handler must skip it and take the signatures from net10.0
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Has.Count.EqualTo(1));
        Assert.That(result.Signatures[0].Label, Is.EqualTo("void MyClass1.Method6(bool value, int count)"));
        Assert.That(result.ActiveSignature, Is.EqualTo(0));
        Assert.That(result.ActiveParameter, Is.EqualTo(1));
    }

    [Test]
    public async Task NoSignaturesForParameterlessMethodTest() {
        var documentPath = CreateDocument(nameof(SignatureHelpHandlerTests), @"
namespace Tests;

class MyClass1 {
    private void Method5() { }
    private void Test() {
        Method5();
    }
}
");
        var result = await handler.Handle(new SignatureHelpParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 16),
        }, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Signatures, Is.Null.Or.Empty);
    }
}
