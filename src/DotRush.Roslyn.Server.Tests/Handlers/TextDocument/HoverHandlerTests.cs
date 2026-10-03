using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class HoverHandlerMock : HoverHandler {
    public HoverHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<Hover?> Handle(HoverParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class HoverHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private HoverHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService(Workspace);
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(10, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("int TestClass.Add(int a, int b)"));
        Assert.That(result.Contents.Value, Does.Contain("Calculates the sum of two numbers"));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 11)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.String"));
        Assert.That(result.Contents.Value, Does.Contain(@"Represents text as a sequence of UTF\-16 code units\."));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 11)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.Int32"));
        Assert.That(result.Contents.Value, Does.Contain(@"Represents a 32\-bit signed integer\."));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("Tests.GenericClass<T>"));
        Assert.That(result.Contents.Value, Does.Contain("Generic test class"));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(13, 16)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("void TestClass.TestMethod(int value)"));
        Assert.That(result.Contents.Value, Does.Contain(@"Overloaded method \- version 2"));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 25)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("delegate void Tests.EventHandler(object sender, System.EventArgs args)"));
        Assert.That(result.Contents.Value, Does.Contain("A delegate for handling events"));
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
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(12, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("delegate int Tests.TestClass.Calculator(int x, int y)"));
        Assert.That(result.Contents.Value, Does.Contain("Result of the calculation"));
    }

    [Test]
    public async Task HoverOnTypeAliasTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
using MyString = System.String;

namespace Tests;

public class TestClass {
    public void TestMethod() {
        MyString text = ""Hello"";
        var result = text.Length;
    }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 12)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.String"));
    }

    [Test]
    public async Task HoverOnGenericTypeAliasTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
using System.Collections.Generic;
using MyList = System.Collections.Generic.List<string>;

namespace Tests;

public class TestClass {
    public void TestMethod() {
        MyList items = new MyList();
        var count = items.Count;
    }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 13)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("System.Collections.Generic.List<T>"));
        Assert.That(result.Contents.Value, Does.Contain("T is string"));
    }

    [Test]
    public async Task HoverOnConditionalTypeAliasMultiTargetingTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
#if NET8_0
using MyObj = System.String;
#else
using MyObj = System.Int32;
#endif

namespace Tests;

public class TestClass {
    public void TestMethod() {
        MyObj obj = default(MyObj);
        var result = obj.ToString();
    }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(11, 8)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents, Is.Not.Null);
        Assert.That(result.Contents.Kind, Is.EqualTo(MarkupKind.Markdown));
        Assert.That(result.Contents.Value, Does.Contain("(net8.0): class System.String"));
        Assert.That(result.Contents.Value, Does.Contain("(net10.0): readonly struct System.Int32"));
    }

    [Test]
    public async Task HoverWithSystemTypesTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;
class TestClass {
    void Test() {
        System.Diagnostics.Process.Start();
    }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 37)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);

        var returnSection = result.Contents.Value.Split("Returns:")[1];
        Assert.That(returnSection, Does.Contain("true if a process resource is started; false if no new process resource is started"));
    }

    [Test]
    public async Task HoverWithInlineDocumentationTagsTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Returns <c>true</c> when <paramref name=""value""/> is <em>not</em> a <see cref=""TestClass""/>
    ///     and continues on the next line.
    /// </summary>
    public bool TestMethod(object value) => false;
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(8, 18)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents.Value, Does.Contain("Returns `true` when value is _not_ a TestClass and continues on the next line"));
    }

    [Test]
    public async Task HoverWithGenericDocumentationReferencesTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    /// <summary>
    /// Uses <see cref=""Dictionary{TKey, TValue}""/> and <see cref=""Array.Empty{T}""/>
    /// </summary>
    public void TestMethod() { }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(7, 18)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents.Value, Does.Contain(@"Uses Dictionary\<TKey, TValue\> and Array\.Empty\<T\>\(\)"));
    }

    [Test]
    public async Task HoverOnInheritDocTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public interface ITestInterface {
    /// <summary>
    /// Interface method documentation
    /// </summary>
    void TestMethod();
}
public class TestClass : ITestInterface {
    /// <inheritdoc/>
    public void TestMethod() { }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(11, 18)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents.Value, Does.Contain("Interface method documentation"));
    }

    [Test]
    public async Task HoverWithRemarksTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

/// <summary>Summary text</summary>
/// <remarks>Remarks text</remarks>
public class TestClass {
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 15)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents.Value, Does.Contain("Remarks text"));
    }

    [Test]
    public async Task HoverOnNullableVariableTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod(string? value) {
        Console.WriteLine(value);
    }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 28)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Contents.Value, Does.Contain("'value' may be null here"));
    }

    [Test]
    public async Task HoverRangeTest() {
        var documentPath = CreateDocument(nameof(HoverHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod() { }
}
");
        var result = await handler.Handle(new HoverParams {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 18)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 16, 4, 26)));
    }
}
