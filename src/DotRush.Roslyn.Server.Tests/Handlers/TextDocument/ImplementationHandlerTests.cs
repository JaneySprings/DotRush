using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Implementation;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class ImplementationHandlerMock : ImplementationHandler {
    public ImplementationHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<ImplementationResponse?> Handle(ImplementationParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class ImplementationHandlerTests : MultitargetProjectFixture {
    private ImplementationHandlerMock handler;
    private NavigationService navigationService;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new ImplementationHandlerMock(navigationService);
    }

    [Test]
    public async Task InterfaceImplementationTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

interface IMyService {
    void Execute();
}
class MyServiceImpl : IMyService {
    public void Execute() { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 16, 7, 23)));
    }

    [Test]
    public async Task InterfaceImplementationOnInterfaceNameTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

interface IAnimal {
    void Speak();
}
class Dog : IAnimal {
    public void Speak() { }
}
class Cat : IAnimal {
    public void Speak() { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 14)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(2));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(6, 6, 6, 9)));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(9, 6, 9, 9)));
    }

    [Test]
    public async Task AbstractMethodOverrideTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

abstract class BaseClass {
    public abstract void DoWork();
}
class DerivedClass : BaseClass {
    public override void DoWork() { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 27)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 25, 7, 31)));
    }

    [Test]
    public async Task VirtualMethodOverrideTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

class Parent {
    public virtual void Run() { }
}
class Child : Parent {
    public override void Run() { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 26)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 25, 7, 28)));
    }

    [Test]
    public async Task DerivedClassTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

class Animal { }
class Dog : Animal { }
class Cat : Animal { }
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 8)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(2));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(4, 6, 4, 9)));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(5, 6, 5, 9)));
    }

    [Test]
    public async Task DerivedInterfaceTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

interface IBase { }
interface IDerived : IBase { }
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 14)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(4, 10, 4, 18)));
    }

    [Test]
    public async Task NoImplementationsTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

interface IEmpty {
    void Nothing();
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Is.Empty);
    }

    [Test]
    public async Task MultipleInterfaceMethodImplementationsTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

interface IProcessor {
    void Process();
}
class ProcessorA : IProcessor {
    public void Process() { }
}
class ProcessorB : IProcessor {
    public void Process() { }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(2));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 16, 7, 23)));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(10, 16, 10, 23)));
    }

    [Test]
    public async Task AbstractPropertyOverrideTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

abstract class Shape {
    public abstract double Area { get; }
}
class Circle : Shape {
    public override double Area => 3.14;
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 29)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(result.Result2, Has.Exactly(1).Matches<Location>(it => it.Range == PositionExtensions.CreateRange(7, 27, 7, 31)));
    }

    [Test]
    public async Task NoSymbolAtPositionTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

// just a comment
class MyClass { }
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(3, 5)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Is.Empty);
    }

    [Test]
    public async Task CodeGenerationImplementationTest() {
        var documentPath = CreateDocument(nameof(ImplementationHandlerTests), @"
namespace Tests;

public interface IService {
    void Execute();
}
");

        var generatedContent = @"
namespace Tests;

public class GeneratedService : IService {
    public void Execute() { }
}
";
        var solution = Workspace.Solution!;
        foreach (var project in solution.Projects) {
            var generatedDoc = project.AddDocument(
                "GeneratedService.g.cs",
                SourceText.From(generatedContent),
                filePath: Path.Combine("__generated__", "GeneratedService.g.cs"));
            solution = generatedDoc.Project.Solution;
        }

        navigationService.UpdateSolution(solution);

        var result = await handler.Handle(new ImplementationParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(4, 10)
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Is.Not.Empty);
        Assert.That(result.Result2, Has.Some.Matches<Location>(it => !PathExtensions.Equals(it.Uri.FileSystemPath, documentPath)));
    }
}
