using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TypeDefinition;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class TypeDefinitionHandlerMock : TypeDefinitionHandler {
    public TypeDefinitionHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<TypeDefinitionResponse?> Handle(TypeDefinitionParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class TypeDefinitionHandlerTests : MultitargetProjectFixture {
    private TypeDefinitionHandlerMock handler;
    private NavigationService navigationService;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new TypeDefinitionHandlerMock(navigationService);
    }

    [Test]
    public async Task SourceTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(TypeDefinitionHandlerTests), @"
namespace Tests;

class MyClass {
}

class Usage {
    void M() {
        var instance = new MyClass();
        instance.ToString();
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new TypeDefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(9, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null);
        Assert.That(result.Result2, Has.Count.EqualTo(1));
        Assert.That(PathExtensions.Equals(result.Result2[0].Uri.FileSystemPath, documentPath), Is.True);
        Assert.That(result.Result2[0].Range, Is.EqualTo(PositionExtensions.CreateRange(3, 6, 3, 13)));
    }

    [Test]
    public async Task DecompiledTypeDefinitionTest() {
        var documentPath = CreateDocument(nameof(TypeDefinitionHandlerTests), @"
namespace Tests;

class MyClass {
    void Method() {
        var builder = new System.Text.StringBuilder();
        builder.Append('c');
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);

        var result = await handler.Handle(new TypeDefinitionParams() {
            TextDocument = documentPath.CreateDocumentId(),
            Position = PositionExtensions.CreatePosition(6, 10)
        }, CancellationToken.None);

        Assert.That(result?.Result2, Is.Not.Null.And.Not.Empty);
        foreach (var location in result!.Result2!) {
            var filePath = location.Uri.FileSystemPath;
            Assert.That(filePath, Does.Contain("_decompiled_"));
            Assert.That(File.Exists(filePath), Is.True, $"Emitted file '{filePath}' does not exist");

            var declarationLine = File.ReadAllLines(filePath)[location.Range.Start.Line];
            Assert.That(declarationLine, Does.Contain("class StringBuilder"));
        }
    }
}
