using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Definition;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class DefinitionHandlerMock : DefinitionHandler {
    public DefinitionHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<DefinitionResponse?> Handle(DefinitionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class DefinitionHandlerTests : MultitargetProjectFixture {
    private DefinitionHandlerMock handler;
    private NavigationService navigationService;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new DefinitionHandlerMock(navigationService);
    }

    [Test]
    public async Task SourceDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        MyClass instance = new MyClass();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result2[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result2[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 6, 3, 13)));
    }

    [Test]
    public async Task DecompiledTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        Console.WriteLine(""test"");
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!)
            AssertDecompiledLocation(location, "class Console");
    }
    [Test]
    public async Task DecompiledMethodDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        Console.WriteLine(""test"");
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 18)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!)
            AssertDecompiledLocation(location, "WriteLine(string");
    }
    [Test]
    public async Task DecompiledNestedTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        var folder = Environment.SpecialFolder.Desktop;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 36)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!)
            AssertDecompiledLocation(location, "enum SpecialFolder");
    }
    [Test]
    public async Task DecompiledDefinitionCacheTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        Console.WriteLine(""test"");
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var request = new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        };
        var firstResult = await handler.Handle(request, CancellationToken.None);
        var secondResult = await handler.Handle(request, CancellationToken.None);

        Assert.That(firstResult?.Result2, Is.Not.Null.And.Not.Empty);
        Assert.That(secondResult?.Result2, Is.Not.Null.And.Not.Empty);
        Assert.That(secondResult!.Result2, Is.EquivalentTo(firstResult!.Result2!));
    }

    [Test]
    public async Task CompilerGeneratedDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

public partial class MyClass {
    public static void MyMethod() {
        GeneratedMethod();
    }
}
");

        var generatedContent = @"
namespace Tests;

public partial class MyClass {
    public static void GeneratedMethod() {
    }
}
";
        var solution = Workspace.Solution!;
        foreach (var project in solution.Projects) {
            var generatedDoc = project.AddDocument(
                "MyClass.g.cs",
                SourceText.From(generatedContent),
                filePath: Path.Combine("__generated__", "MyClass.g.cs"));
            solution = generatedDoc.Project.Solution;
        }

        navigationService.UpdateSolution(solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            var filePath = location.Uri.FileSystemPath;
            Assert.That(filePath, Does.Contain("_generated_"));
            Assert.That(File.Exists(filePath), Is.True, $"Emitted file '{filePath}' does not exist");
            Assert.That(location.Range, Is.EqualTo(PositionExtensions.CreateRange(4, 23, 4, 38)));
        }
    }

    [Test]
    public async Task SourceGenericTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyGeneric<T> {
    public void DoWork(T item) {}
}

class Usage {
    void M() {
        var instance = new MyGeneric<int>();
        instance.DoWork(1);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(9, 30)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result2[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result2[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 6, 3, 15)));
    }
    [Test]
    public async Task SourceGenericMethodDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyGeneric<T> {
    public void DoWork(T item) {}
}

class Usage {
    void M() {
        var instance = new MyGeneric<int>();
        instance.DoWork(1);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(10, 19)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result2[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result2[0].Range, Is.EqualTo(PositionExtensions.CreateRange(4, 16, 4, 22)));
    }

    [Test]
    public async Task DecompiledGenericTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        List<int> list = new();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 9)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            Assert.That(location.Uri.FileSystemPath, Does.Contain("List`1"));
            AssertDecompiledLocation(location, "class List<T>");
        }
    }
    [Test]
    public async Task DecompiledGenericConstructorDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        var list = new List<int>();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 24)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            Assert.That(location.Uri.FileSystemPath, Does.Contain("List`1"));
            AssertDecompiledLocation(location, "public List()");
        }
    }
    [Test]
    public async Task DecompiledGenericMethodDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        var list = new List<int>();
        list.Add(1);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 14)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            Assert.That(location.Uri.FileSystemPath, Does.Contain("List`1"));
            AssertDecompiledLocation(location, "Add(T item)");
        }
    }
    [Test]
    public async Task DecompiledGenericTypeArityDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        Dictionary<string, List<int>> map = new();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            Assert.That(location.Uri.FileSystemPath, Does.Contain("Dictionary`2"));
            AssertDecompiledLocation(location, "class Dictionary<TKey, TValue>");
        }
    }
    [Test]
    public async Task DecompiledGenericNestedTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        List<int>.Enumerator enumerator = default;
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 20)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            Assert.That(location.Uri.FileSystemPath, Does.Contain("List`1"));
            AssertDecompiledLocation(location, "struct Enumerator");
        }
    }
    [Test]
    public async Task DecompiledGenericMethodInferredDefinitionTest() {
        var documentPath = CreateDocument(nameof(DefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        var tuple = Tuple.Create(42);
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new DefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(5, 28)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!)
            AssertDecompiledLocation(location, "Create<T1>(");
    }

    private static void AssertDecompiledLocation(Location location, string expectedDeclaration) {
        var filePath = location.Uri.FileSystemPath;
        Assert.That(filePath, Does.Contain("_decompiled_"));
        Assert.That(File.Exists(filePath), Is.True, $"Emitted file '{filePath}' does not exist");

        var declarationLine = File.ReadAllLines(filePath)[location.Range.Start.Line];
        Assert.That(declarationLine, Does.Contain(expectedDeclaration));
    }
}
