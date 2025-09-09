using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Handlers.Workspace;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceSymbol;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceSymbolHandlerMock : WorkspaceSymbolHandler {
    public WorkspaceSymbolHandlerMock(WorkspaceService workspaceService) : base(workspaceService) { }

    public new Task<WorkspaceSymbolResponse> Handle(WorkspaceSymbolParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class WorkspaceSymbolHandlerTests : MultitargetProjectFixture {
    private WorkspaceSymbolHandlerMock handler;
    private List<string> createdDocumentPaths;

    [SetUp]
    public void SetUp() {
        handler = new WorkspaceSymbolHandlerMock(Workspace);
        createdDocumentPaths = new List<string>();
    }

    [TearDown]
    public void TearDown() {
        foreach (var path in createdDocumentPaths) {
            Workspace.DeleteDocument(path);
            FileSystemExtensions.TryDeleteFile(path);
        }
        createdDocumentPaths.Clear();
    }

    [Test]
    public async Task SearchForClassTest() {
        var documentPath = CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

/// <summary>
/// A test class for demonstration
/// </summary>
public class TestClass {
    public int Property { get; set; }
}

public class AnotherClass {
    public string Name { get; set; }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "TestClass"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var testClassSymbol = result.Symbols.First();
        Assert.That(testClassSymbol.Name, Is.EqualTo("TestClass"));
        Assert.That(testClassSymbol.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(testClassSymbol.Location, Is.Not.Null);
        Assert.That(testClassSymbol.Location.Value.Uri.FileSystemPath, Is.EqualTo(documentPath));
        Assert.That(testClassSymbol.ContainerName, Is.Null);
    }

    [Test]
    public async Task SearchForMethodTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public int Add(int a, int b) {
        return a + b;
    }
    public void DoSomething() {
        // Another method
    }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "Add"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var addMethodSymbol = result.Symbols.FirstOrDefault(s => s.Name == "Add");
        Assert.That(addMethodSymbol, Is.Not.Null);
        Assert.That(addMethodSymbol.Kind, Is.EqualTo(SymbolKind.Method));
        Assert.That(addMethodSymbol.ContainerName, Is.EqualTo("TestClass"));
        Assert.That(addMethodSymbol.Location, Is.Not.Null);
    }

    [Test]
    public async Task SearchForPropertyTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public string Name { get; set; }
    public int Age { get; set; }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "Name"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var namePropertySymbol = result.Symbols.FirstOrDefault(s => s.Name == "Name");
        Assert.That(namePropertySymbol, Is.Not.Null);
        Assert.That(namePropertySymbol.Kind, Is.EqualTo(SymbolKind.Property));
        Assert.That(namePropertySymbol.ContainerName, Is.EqualTo("TestClass"));
    }

    [Test]
    public async Task SearchForFieldTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    private DateTime _testField;
    private string _anotherField;
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "_testField"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var fieldSymbol = result.Symbols.FirstOrDefault(s => s.Name == "_testField");
        Assert.That(fieldSymbol, Is.Not.Null);
        Assert.That(fieldSymbol.Kind, Is.EqualTo(SymbolKind.Field));
        Assert.That(fieldSymbol.ContainerName, Is.EqualTo("TestClass"));
    }

    [Test]
    public async Task SearchForInterfaceTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public interface ITestInterface {
    void TestMethod();
}

public interface IAnotherInterface {
    string GetValue();
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "ITestInterface"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var interfaceSymbol = result.Symbols.FirstOrDefault(s => s.Name == "ITestInterface");
        Assert.That(interfaceSymbol, Is.Not.Null);
        Assert.That(interfaceSymbol.Kind, Is.EqualTo(SymbolKind.Interface));
    }

    [Test]
    public async Task SearchForEnumMemberTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public enum TestEnum {
    Value1,
    Value2,
    SpecialValue
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "SpecialValue"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var enumMemberSymbol = result.Symbols.FirstOrDefault(s => s.Name == "SpecialValue");
        Assert.That(enumMemberSymbol, Is.Not.Null);
        Assert.That(enumMemberSymbol.Kind, Is.EqualTo(SymbolKind.EnumMember));
        Assert.That(enumMemberSymbol.ContainerName, Is.EqualTo("TestEnum"));
    }

    [Test]
    public async Task SearchForStructTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public struct TestStruct {
    public int X { get; set; }
    public int Y { get; set; }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "TestStruct"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var structSymbol = result.Symbols.FirstOrDefault(s => s.Name == "TestStruct");
        Assert.That(structSymbol, Is.Not.Null);
        Assert.That(structSymbol.Kind, Is.EqualTo(SymbolKind.Struct));
    }

    [Test]
    public async Task SearchCaseInsensitiveTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public void MyMethod() { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "mymethod"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var methodSymbol = result.Symbols.FirstOrDefault(s => s.Name == "MyMethod");
        Assert.That(methodSymbol, Is.Not.Null);
        Assert.That(methodSymbol.Kind, Is.EqualTo(SymbolKind.Method));
    }

    [Test]
    public async Task SearchPartialMatchTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public void SomeMethodWithLongName() { }
    public void AnotherMethod() { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "Method"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(2));
        Assert.That(result.Symbols.Any(s => s.Name == "SomeMethodWithLongName"), Is.True);
        Assert.That(result.Symbols.Any(s => s.Name == "AnotherMethod"), Is.True);
    }

    [Test]
    public async Task SearchForGenericTypeTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class GenericClass<T> {
    public T Value { get; set; }
    
    public void GenericMethod<U>(U parameter) { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "GenericClass"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var genericClassSymbol = result.Symbols.FirstOrDefault(s => s.Name == "GenericClass");
        Assert.That(genericClassSymbol, Is.Not.Null);
        Assert.That(genericClassSymbol.Kind, Is.EqualTo(SymbolKind.Class));
    }

    [Test]
    public async Task SearchMultipleFilesTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class ClassInFile1 {
    public void Method1() { }
}
");

        createdDocumentPaths.Add(CreateDocument($"{nameof(WorkspaceSymbolHandlerTests)}2", @"
namespace Tests;

public class ClassInFile2 {
    public void Method2() { }
}
"));

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "Class"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Is.Not.Empty);

        var classes = result.Symbols.Where(s => s.Name.Contains("Class")).ToList();
        Assert.That(classes, Has.Count.EqualTo(2));
        Assert.That(classes.Any(s => s.Name == "ClassInFile1"), Is.True);
        Assert.That(classes.Any(s => s.Name == "ClassInFile2"), Is.True);
    }

    [Test]
    public async Task SearchWithEmptyQueryTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod() { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = ""
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Is.Empty);
    }

    [Test]
    public async Task SearchNoMatchesTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class TestClass {
    public void TestMethod() { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "NonExistentSymbol"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Is.Empty);
    }

    [Test]
    public async Task SearchForNestedClassTest() {
        CreateDocument(nameof(WorkspaceSymbolHandlerTests), @"
namespace Tests;

public class OuterClass {
    public class NestedClass {
        public void NestedMethod() { }
    }
    
    public void OuterMethod() { }
}
");

        var result = await handler.Handle(new WorkspaceSymbolParams {
            Query = "NestedClass"
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Symbols, Has.Count.EqualTo(1));

        var nestedClassSymbol = result.Symbols.FirstOrDefault(s => s.Name == "NestedClass");
        Assert.That(nestedClassSymbol, Is.Not.Null);
        Assert.That(nestedClassSymbol.Kind, Is.EqualTo(SymbolKind.Class));
        Assert.That(nestedClassSymbol.ContainerName, Is.EqualTo("OuterClass"));
    }
}
