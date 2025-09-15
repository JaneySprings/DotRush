using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.InlayHint;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class InlayHintHandlerMock : InlayHintHandler {
    public InlayHintHandlerMock(WorkspaceService workspaceService) : base(workspaceService) { }

    public new Task<InlayHintResponse?> Handle(InlayHintParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class InlayHintHandlerTests : MultitargetProjectFixture {
    private InlayHintHandlerMock handler;

    [SetUp]
    public void SetUp() {
        handler = new InlayHintHandlerMock(Workspace);
    }

    [Test]
    public async Task InlayHintOnMethodCallParametersTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod(string firstName, int age, bool isActive) {
    }
    public void CallerMethod() {
        TestMethod(""John"", 25, true);
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(7, 0, 7, 35)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(3));
        var firstNameHint = result.InlayHints.FirstOrDefault(h => h.Label.String == ("firstName: "));
        Assert.That(firstNameHint, Is.Not.Null);
        Assert.That(firstNameHint.Position.Line, Is.EqualTo(7));
        Assert.That(firstNameHint.Position.Character, Is.GreaterThanOrEqualTo(16));
        Assert.That(firstNameHint.PaddingRight, Is.False);
        Assert.That(firstNameHint.PaddingLeft, Is.Null.Or.False);
        Assert.That(firstNameHint.Kind, Is.Null);
        Assert.That(firstNameHint.TextEdits, Is.Not.Null);
        Assert.That(firstNameHint.TextEdits, Has.Count.EqualTo(1));
        Assert.That(firstNameHint.TextEdits![0].NewText, Is.EqualTo("firstName: "));
        Assert.That(firstNameHint.TextEdits[0].Range.Start.Line, Is.EqualTo(7));
        Assert.That(firstNameHint.TextEdits[0].Range.Start.Character, Is.EqualTo(19));
        Assert.That(firstNameHint.TextEdits[0].Range.End.Line, Is.EqualTo(7));
        Assert.That(firstNameHint.TextEdits[0].Range.End.Character, Is.EqualTo(19));

        var ageHint = result.InlayHints.FirstOrDefault(h => h.Label.String == ("age: "));
        Assert.That(ageHint, Is.Not.Null);
        Assert.That(ageHint.Position.Line, Is.EqualTo(7));
        Assert.That(ageHint.Position.Character, Is.EqualTo(27));
        Assert.That(ageHint.PaddingRight, Is.False);
        Assert.That(ageHint.PaddingLeft, Is.Null.Or.False);
        Assert.That(ageHint.Kind, Is.Null);
        Assert.That(ageHint.TextEdits, Is.Not.Null);
        Assert.That(ageHint.TextEdits, Has.Count.EqualTo(1));
        Assert.That(ageHint.TextEdits![0].NewText, Is.EqualTo("age: "));
        Assert.That(ageHint.TextEdits[0].Range.Start.Line, Is.EqualTo(7));
        Assert.That(ageHint.TextEdits[0].Range.Start.Character, Is.EqualTo(27));
        Assert.That(ageHint.TextEdits[0].Range.End.Line, Is.EqualTo(7));
        Assert.That(ageHint.TextEdits[0].Range.End.Character, Is.EqualTo(27));

        var isActiveHint = result.InlayHints.FirstOrDefault(h => h.Label.String == ("isActive: "));
        Assert.That(isActiveHint, Is.Not.Null);
        Assert.That(isActiveHint.Position.Line, Is.EqualTo(7));
        Assert.That(isActiveHint.Position.Character, Is.EqualTo(31));
        Assert.That(isActiveHint.PaddingRight, Is.False);
        Assert.That(isActiveHint.PaddingLeft, Is.Null.Or.False);
        Assert.That(isActiveHint.Kind, Is.Null);
        Assert.That(isActiveHint.TextEdits, Is.Not.Null);
        Assert.That(isActiveHint.TextEdits, Has.Count.EqualTo(1));
        Assert.That(isActiveHint.TextEdits![0].NewText, Is.EqualTo("isActive: "));
        Assert.That(isActiveHint.TextEdits[0].Range.Start.Line, Is.EqualTo(7));
        Assert.That(isActiveHint.TextEdits[0].Range.Start.Character, Is.EqualTo(31));
        Assert.That(isActiveHint.TextEdits[0].Range.End.Line, Is.EqualTo(7));
        Assert.That(isActiveHint.TextEdits[0].Range.End.Character, Is.EqualTo(31));
    }
    [Test]
    public async Task InlayHintOnConstructorParametersTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;

public class Person {
    public Person(string name, int age) {
    }
}
public class TestClass {
    public void TestMethod() {
        var person = new Person(""Alice"", 30);
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(9, 0, 10, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(3));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == "name: "));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == "age: "));

        var personHint = result.InlayHints.FirstOrDefault(h => h.Label.String == "Person?");
        Assert.That(personHint, Is.Not.Null);
        Assert.That(personHint.Position.Line, Is.EqualTo(9));
        Assert.That(personHint.Position.Character, Is.EqualTo(8));
        Assert.That(personHint.PaddingRight, Is.True);
        Assert.That(personHint.PaddingLeft, Is.Null.Or.False);
        Assert.That(personHint.Kind, Is.Null);
        Assert.That(personHint.TextEdits, Is.Not.Null);
        Assert.That(personHint.TextEdits, Has.Count.EqualTo(1));
        Assert.That(personHint.TextEdits![0].NewText, Is.EqualTo("Person?"));
        Assert.That(personHint.TextEdits[0].Range.Start.Line, Is.EqualTo(9));
        Assert.That(personHint.TextEdits[0].Range.Start.Character, Is.EqualTo(8));
        Assert.That(personHint.TextEdits[0].Range.End.Line, Is.EqualTo(9));
        Assert.That(personHint.TextEdits[0].Range.End.Character, Is.EqualTo(11));
    }
    [Test]
    public async Task InlayHintOnLambdaParameterTypesTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;

using System;
using System.Linq;

public class TestClass {
    public void TestMethod() {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var result = numbers.Where(x => x > 2).Select(y => y * 2);
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(9, 0, 10, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(5));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("IEnumerable<int>?")));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("predicate: ")));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("selector: ")));

        var intHint = result.InlayHints.FirstOrDefault(h => h.Label.String == ("int "));
        Assert.That(intHint, Is.Not.Null);
        Assert.That(intHint.PaddingRight, Is.False);
        Assert.That(intHint.PaddingLeft, Is.Null.Or.False);
        Assert.That(intHint.TextEdits, Is.Null.Or.Empty);
    }
    [Test]
    public async Task InlayHintOnUnknownTypeTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;
public class TestClass {
    public void TestMethod() {
        var obj = new UnknownType();
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 0, 5, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(1));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("UnknownType?")));
    }
    [Test]
    public async Task InlayHintOnInvalidSyntaxTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;
public class TestClass {
    public void TestMethod() {
        var obj = new();
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 0, 5, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(1));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("?")));
    }
    [Test]
    public async Task MultitargetInlayHintTest() {
        var documentPath = CreateDocument(nameof(InlayHintHandlerTests), @"
namespace Tests;
public class TestClass {
    public void TestMethod() {
#if NET8_0
    var obj = new MyClass();
#else
    var obj = new MyClass2();
#endif
    }
}
");
        var result = await handler.Handle(new InlayHintParams {
            TextDocument = documentPath.CreateDocumentId(),
            Range = PositionExtensions.CreateRange(4, 0, 8, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.InlayHints, Has.Count.EqualTo(2));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("MyClass?")));
        Assert.That(result.InlayHints, Has.One.Matches<InlayHint>(h => h.Label.String == ("MyClass2?")));
    }
}