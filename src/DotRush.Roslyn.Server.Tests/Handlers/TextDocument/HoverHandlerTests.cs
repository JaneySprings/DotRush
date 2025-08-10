using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Hover;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class HoverHandlerMock : HoverHandler {
    public HoverHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<HoverResponse?> Handle(HoverParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class HoverHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private HoverHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new HoverHandlerMock(navigationService);
    }

    [Test]
    public async Task HoverOnClassTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>
/// A test class for demonstration
/// </summary>
public class TestClass {
    public int Property { get; set; }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests.TestClass"));
        Assert.That(result.Contents.Value, Does.Contain("A test class for demonstration"));
    }

    [Test]
    public async Task HoverOnMethodTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Calculates the sum of two numbers
    /// </summary>
    /// <param name=""firstParamA"">First number</param>
    /// <param name=""secondParamB"">Second number</param>
    /// <returns>The sum of a and b</returns>
    public int Add(int a, int b) {
        return a + b;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(10, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("int TestClass.Add(int a, int b)"));
        Assert.That(result.Contents.Value, Does.Contain("Calculates the sum of two numbers"));
        Assert.That(result.Contents.Value, Does.Contain("firstParamA"));
        Assert.That(result.Contents.Value, Does.Contain("secondParamB"));
        Assert.That(result.Contents.Value, Does.Contain("First number"));
        Assert.That(result.Contents.Value, Does.Contain("Second number"));
        Assert.That(result.Contents.Value, Does.Contain("The sum of a and b"));
    }

    [Test]
    public async Task HoverOnPropertyTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Gets or sets the name of the test
    /// </summary>
    public string Name { get; set; }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 19)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("string TestClass.Name"));
        Assert.That(result.Contents.Value, Does.Contain("Gets or sets the name of the test"));
    }

    [Test]
    public async Task HoverOnFieldTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// A private field for testing
    /// </summary>
    private DateTime _testField;
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 21)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("DateTime TestClass._testField"));
        Assert.That(result.Contents.Value, Does.Contain("A private field for testing"));
    }

    [Test]
    public async Task HoverOnNamespaceTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests.SubNamespace;

public class TestClass {
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(1, 10)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests"));
    }

    [Test]
    public async Task HoverOnSystemTypeTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    public string GetString() {
        return string.Empty;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 11)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.String"));
        Assert.That(result.Contents.Value, Does.Contain("Represents text as a sequence of UTF-16 code units."));
    }

    [Test]
    public async Task HoverOnSystemType2Test() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    public int GetInt() {
        return 0;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 11)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.Int32"));
        Assert.That(result.Contents.Value, Does.Contain("Represents a 32-bit signed integer."));
    }

    [Test]
    public async Task HoverOnVariableTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod() {
        var testVariable = 42;
        Console.WriteLine(testVariable);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 12)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("int testVariable"));
    }

    [Test]
    public async Task HoverOnParameterTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Test method with parameter
    /// </summary>
    /// <param name=""input"">The input parameter</param>
    public void TestMethod(string input) {
        Console.WriteLine(input);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(9, 26)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("string input"));
    }

    [Test]
    public async Task HoverOnInterfaceTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>
/// A test interface
/// </summary>
public interface ITestInterface {
    void TestMethod();
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 18)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests.ITestInterface"));
        Assert.That(result.Contents.Value, Does.Contain("A test interface"));
    }

    [Test]
    public async Task HoverOnEnumTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>
/// Test enumeration
/// </summary>
public enum TestEnum {
    /// <summary>
    /// First value
    /// </summary>
    Value1,
    /// <summary>
    /// Second value
    /// </summary>
    Value2
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 13)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests.TestEnum"));
        Assert.That(result.Contents.Value, Does.Contain("Test enumeration"));
    }

    [Test]
    public async Task HoverOnInvalidPosition() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(0, 0)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HoverOnGenericTypeTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>
/// Generic test class
/// </summary>
/// <typeparam name=""T"">The type parameter</typeparam>
public class GenericClass<T> {
    public T Value { get; set; }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests.GenericClass<T>"));
        Assert.That(result.Contents.Value, Does.Contain("Generic test class"));
        Assert.That(result.Contents.Value, Does.Contain("The type parameter"));
    }

    [Test]
    public async Task HoverOnMethodOverloadTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Overloaded method - version 1
    /// </summary>
    public void TestMethod() { }
    
    /// <summary>
    /// Overloaded method - version 2
    /// </summary>
    /// <param name=""value"">Input value</param>
    public void TestMethod(int value) { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(13, 16)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("void TestClass.TestMethod(int value)"));
        Assert.That(result.Contents.Value, Does.Contain("Overloaded method - version 2"));
    }

    [Test]
    public async Task HoverOnDelegateTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>
/// A delegate for handling events
/// </summary>
/// <param name=""sender"">The event sender</param>
/// <param name=""args"">The event arguments</param>
public delegate void EventHandler(object sender, EventArgs args);
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 25)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("void Tests.EventHandler(System.Object, System.EventArgs)"));
        Assert.That(result.Contents.Value, Does.Contain("A delegate for handling events"));
        Assert.That(result.Contents.Value, Does.Contain("The event sender"));
        Assert.That(result.Contents.Value, Does.Contain("The event arguments"));
    }

    [Test]
    public async Task HoverOnDelegatePropertyTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Custom delegate for calculations
    /// </summary>
    /// <param name=""x"">First operand</param>
    /// <param name=""y"">Second operand</param>
    /// <returns>Result of the calculation</returns>
    public delegate int Calculator(int x, int y);
    
    public Calculator Calc { get; set; }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(11, 22)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.Int32 Tests.TestClass.Calculator(System.Int32, System.Int32)"));
        Assert.That(result.Contents.Value, Does.Contain("First operand"));
        Assert.That(result.Contents.Value, Does.Contain("Second operand"));
        Assert.That(result.Contents.Value, Does.Contain("Result of the calculation"));
    }
}
