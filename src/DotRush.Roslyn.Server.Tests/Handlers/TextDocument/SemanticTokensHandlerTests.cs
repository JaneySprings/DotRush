using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SemanticToken;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class SemanticTokensHandlerMock : SemanticTokensHandler {
    public SemanticTokensHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class SemanticTokensHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private SemanticTokensHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new SemanticTokensHandlerMock(navigationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(SemanticTokensHandlerTests), @"
using System.Diagnostics.Process;

namespace Tests {
    public class SemanticTokensTest {
        private int field1 = 42;
        public string Property1 { get; set; } = ""hello"";
        
        public void Method1(int parameter) {
            var localVariable = $""world {field1}"";
            if (parameter > 0) {
                Console.WriteLine(localVariable);
            }
        }
        
        public enum TestEnum {
            Value1,
            Value2
        }
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new SemanticTokensParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result?.Data, Is.Not.Null.Or.Empty);
        Assert.That(result.Data.Count % 5, Is.EqualTo(0));
        Assert.That(result.Data.Count / 5, Is.EqualTo(41));

        AssertToken(result, 0, 1, 0, SemanticTokenType.Keyword, 5); // \nusing
        AssertToken(result, 1, 0, 6, SemanticTokenType.Namespace, 6); // System
        AssertToken(result, 2, 0, 7, SemanticTokenType.Namespace, 11); // Diagnostics
        AssertToken(result, 3, 0, 12, SemanticTokenType.Class, 7); // Process

        AssertToken(result, 4, 2, 0, SemanticTokenType.Keyword, 9); // namespace
        AssertToken(result, 5, 0, 10, SemanticTokenType.Namespace, 5); // Tests

        AssertToken(result, 6, 1, 4, SemanticTokenType.Keyword, 6); // public
        AssertToken(result, 7, 0, 7, SemanticTokenType.Keyword, 5); // class
        AssertToken(result, 8, 0, 6, SemanticTokenType.Class, 18); // SemanticTokensTest

        AssertToken(result, 9, 1, 8, SemanticTokenType.Keyword, 7); // private
        AssertToken(result, 10, 0, 8, SemanticTokenType.Keyword, 3); // int
        AssertToken(result, 11, 0, 4, SemanticTokenType.Variable, 6); // field1
        AssertToken(result, 12, 0, 9, SemanticTokenType.Number, 2); // 42

        AssertToken(result, 13, 1, 8, SemanticTokenType.Keyword, 6); // public
        AssertToken(result, 14, 0, 7, SemanticTokenType.Keyword, 6); // string
        AssertToken(result, 15, 0, 7, SemanticTokenType.Property, 9); // Property1
        AssertToken(result, 16, 0, 12, SemanticTokenType.Keyword, 3); // get
        AssertToken(result, 17, 0, 5, SemanticTokenType.Keyword, 3); // set
        AssertToken(result, 18, 0, 9, SemanticTokenType.String, 7); // "hello"

        AssertToken(result, 19, 2, 8, SemanticTokenType.Keyword, 6); // public
        AssertToken(result, 20, 0, 7, SemanticTokenType.Keyword, 4); // void
        AssertToken(result, 21, 0, 5, SemanticTokenType.Method, 7); // Method1
        AssertToken(result, 22, 0, 8, SemanticTokenType.Keyword, 3); // int
        AssertToken(result, 23, 0, 4, SemanticTokenType.Parameter, 9); // parameter

        AssertToken(result, 24, 1, 12, SemanticTokenType.Keyword, 3); // var
        AssertToken(result, 25, 0, 4, SemanticTokenType.Variable, 13); // localVariable
        AssertToken(result, 26, 0, 16, SemanticTokenType.String, 2); // $"
        AssertToken(result, 27, 0, 2, SemanticTokenType.String, 6); // world_
        AssertToken(result, 28, 0, 7, SemanticTokenType.Variable, 6); // field1
        AssertToken(result, 29, 0, 7, SemanticTokenType.String, 1); // "

        AssertToken(result, 30, 1, 12, SemanticTokenType.ControlKeyword, 2); // if
        AssertToken(result, 31, 0, 4, SemanticTokenType.Parameter, 9); // parameter
        AssertToken(result, 32, 0, 12, SemanticTokenType.Number, 1); // 0

        AssertToken(result, 33, 1, 16, SemanticTokenType.Class, 7); // Console
        AssertToken(result, 34, 0, 8, SemanticTokenType.Method, 9); // WriteLine
        AssertToken(result, 35, 0, 10, SemanticTokenType.Variable, 13); // localVariable

        AssertToken(result, 36, 4, 8, SemanticTokenType.Keyword, 6); // public
        AssertToken(result, 37, 0, 7, SemanticTokenType.Keyword, 4); // enum
        AssertToken(result, 38, 0, 5, SemanticTokenType.Enum, 8); // TestEnum

        AssertToken(result, 39, 1, 12, SemanticTokenType.EnumMember, 6); // Value1
        AssertToken(result, 40, 1, 12, SemanticTokenType.EnumMember, 6); // Value2
    }

    private void AssertToken(SemanticTokens tokens, int index, uint deltaLine, uint deltaColumn, SemanticTokenType type, uint length) {
        Assert.That(tokens.Data[index * 5], Is.EqualTo(deltaLine));
        Assert.That(tokens.Data[index * 5 + 1], Is.EqualTo(deltaColumn));
        Assert.That(tokens.Data[index * 5 + 2], Is.EqualTo(length));
        Assert.That(tokens.Data[index * 5 + 3], Is.EqualTo((uint)type), $"Expected token type {type}");
    }
}
