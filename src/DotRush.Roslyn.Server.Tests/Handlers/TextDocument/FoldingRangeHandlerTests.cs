using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Message.FoldingRange;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class FoldingRangeHandlerMock : FoldingRangeHandler {
    public FoldingRangeHandlerMock(NavigationService navigationService) : base(navigationService) { }

    public new Task<FoldingRangeResponse> Handle(FoldingRangeParams request, CancellationToken token) {
        return base.Handle(request, token);
    }
}

public class FoldingRangeHandlerTests : MultitargetProjectFixture {
    private NavigationService navigationService;
    private FoldingRangeHandlerMock handler;

    [SetUp]
    public void SetUp() {
        navigationService = new NavigationService();
        handler = new FoldingRangeHandlerMock(navigationService);
    }

    [Test]
    public async Task GeneralHandlerTest() {
        var documentPath = CreateDocument(nameof(FoldingRangeHandlerTests), @"
using System;
using System.Collections.Generic;

namespace Tests {
    class FoldingRangeTests {
        /// <summary>
        /// This is a test class.
        /// </summary>
        private void Method() {
            var test = () => {
                Console.WriteLine();
            };
        }
    }
}
");
        navigationService.UpdateSolution(Workspace.Solution);
        var result = await handler.Handle(new FoldingRangeParams() {
            TextDocument = documentPath.CreateDocumentId()
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.FoldingRanges, Has.Count.EqualTo(6));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 1 && x.EndLine == 2));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 4 && x.EndLine == 15));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 5 && x.EndLine == 14));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 6 && x.EndLine == 8));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 9 && x.EndLine == 13));
        Assert.That(result.FoldingRanges, Has.Some.Matches<FoldingRange>(x => x.StartLine == 10 && x.EndLine == 12));
    }
}