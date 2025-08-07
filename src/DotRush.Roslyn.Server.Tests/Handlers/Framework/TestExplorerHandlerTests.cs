using DotRush.Roslyn.Server.Handlers.Framework;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Tests.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class TestExplorerHandlerMock : TestExplorerHandler {
    public TestExplorerHandlerMock(TestExplorerService testExplorerService, WorkspaceService workspaceService) : base(testExplorerService, workspaceService) { }

    public async Task<TestItem[]> Handle(TestFixtureParams request) {
        return (await Handle(request, CancellationToken.None).ConfigureAwait(false))?.ToArray() ?? Array.Empty<TestItem>();
    }
    public async Task<TestItem[]> Handle(TestCaseParams request) {
        return (await Handle(request, CancellationToken.None).ConfigureAwait(false))?.ToArray() ?? Array.Empty<TestItem>();
    }
}

public class TestExplorerHandlerTests : NUnitTestProjectFixture {
    private TestExplorerService testExplorerService;
    private TestExplorerHandlerMock handler;

    [SetUp]
    public void SetUp() {
        testExplorerService = new TestExplorerService();
        handler = new TestExplorerHandlerMock(testExplorerService, (WorkspaceService)Workspace);
    }

    [Test]
    public async Task DiscoverEmptyFixtureTest() {
        CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture {}
");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);

        Assert.That(result, Is.Null.Or.Empty);
    }
    [Test]
    public async Task DiscoverFixtureWithOneCaseTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture<T> {
    [Test]
    public void MyTest<V>() {}
    public void MyTestHelper() {
        Assert.Pass();
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("MyFixture"));
        Assert.That(result[0].Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        Assert.That(result[0].FilePath, Is.EqualTo(documentPath));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(5, 0, 12, 1)));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("MyTest"));
        Assert.That(result[0].Id, Is.EqualTo("MyTest"));
        Assert.That(result[0].FilePath, Is.EqualTo(documentPath));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 8, 30)));
    }
    [Test]
    public async Task DiscoverFixtureWithMultipleCaseTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture {
    [Test]
    public void MyTest() {}
    [Test]
    public void MyTest2() { Assert.Pass(); }
    [Test]
    public void MyTest3<T>() {
        Assert.Pass();
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("MyFixture"));
        Assert.That(result[0].Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        Assert.That(result[0].FilePath, Is.EqualTo(documentPath));
        Assert.That(result[0].Range, Is.EqualTo(PositionExtensions.CreateRange(5, 0, 15, 1)));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(3));

        var test1 = result.FirstOrDefault(t => t.Name == "MyTest");
        Assert.That(test1, Is.Not.Null);
        Assert.That(test1.Id, Is.EqualTo("MyTest"));
        Assert.That(test1.FilePath, Is.EqualTo(documentPath));
        Assert.That(test1.Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 8, 27)));

        var test2 = result.FirstOrDefault(t => t.Name == "MyTest2");
        Assert.That(test2, Is.Not.Null);
        Assert.That(test2.Id, Is.EqualTo("MyTest2"));
        Assert.That(test2.FilePath, Is.EqualTo(documentPath));
        Assert.That(test2.Range, Is.EqualTo(PositionExtensions.CreateRange(9, 4, 10, 44)));

        var test3 = result.FirstOrDefault(t => t.Name == "MyTest3");
        Assert.That(test3, Is.Not.Null);
        Assert.That(test3.Id, Is.EqualTo("MyTest3"));
        Assert.That(test3.FilePath, Is.EqualTo(documentPath));
        Assert.That(test3.Range, Is.EqualTo(PositionExtensions.CreateRange(11, 4, 14, 5)));
    }
    [Test]
    public async Task DiscoverFixturesInsideDirectiveTest() {
        var documentPath = CreateUnitTestDocument(@"
#if NET8_0_OR_GREATER
[TestFixture]
public class MyFixture {
    [Test]
    public void MyTest() {}
}
#endif");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));
    }
    [Test]
    public async Task DiscoverFixturesWithDuplicateTestsTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture {
    [Test]
    public void MyTest() {}
    [Test]
    public void MyTest(int a) { Assert.Pass(); }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));
    }
    [Test]
    public async Task DiscoverFixturesWithTestCasesTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture {
    [Test]
    public void MyTest() {}
    [TestCase(1)]
    [TestCase(2)]
    public void MyTest2(int a) { Assert.Pass(); }

    [Test]
    [TestCase(1)]
    [TestCase(2)]
    public void MyTest3(int a) { Assert.Pass(); }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(3));
    }
    [Test]
    public async Task DiscoverFixturesWithHierarchyTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture : MyBaseFixture {
    [Test]
    public void MyTest() {}
}
public abstract class MyBaseFixture {
    [Test]
    public void MyTest2() {
        Assert.Pass();
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("MyFixture"));
        Assert.That(result[0].Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        Assert.That(result[0].FilePath, Is.EqualTo(documentPath));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(2));

        var testCase1 = result.FirstOrDefault(t => t.Name == "MyTest");
        Assert.That(testCase1, Is.Not.Null);
        Assert.That(testCase1.Id, Is.EqualTo("MyTest"));
        Assert.That(testCase1.FilePath, Is.EqualTo(documentPath));
        Assert.That(testCase1.Range, Is.EqualTo(PositionExtensions.CreateRange(7, 4, 8, 27)));

        var testCase2 = result.FirstOrDefault(t => t.Name == "MyTest2");
        Assert.That(testCase2, Is.Not.Null);
        Assert.That(testCase2.Id, Is.EqualTo("MyTest2"));
        Assert.That(testCase2.FilePath, Is.EqualTo(documentPath));
        Assert.That(testCase2.Range, Is.EqualTo(PositionExtensions.CreateRange(11, 4, 14, 5)));
    }
    [Test]
    public async Task DiscoverFixturesWithHierarchy2Test() {
        var documentPath = CreateUnitTestDocument(@"
public class MyFixture2 : MyFixture {
    [Test]
    public void MyTest() {}
}

public class MyFixture : MyBaseFixture { }

[TestFixture]
public class MyBaseFixture {
    [Test]
    public void MyTest2() {
        Assert.Pass();
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(3));

        var fixture1 = result.FirstOrDefault(f => f.Name == "MyFixture2");
        Assert.That(fixture1, Is.Not.Null);
        Assert.That(fixture1.Id, Is.EqualTo("NUnitTestProject.MyFixture2"));
        Assert.That(fixture1.FilePath, Is.EqualTo(documentPath));
        var tests1 = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = fixture1.Id }).ConfigureAwait(false);
        Assert.That(tests1, Has.Length.EqualTo(2));

        var fixture2 = result.FirstOrDefault(f => f.Name == "MyBaseFixture");
        Assert.That(fixture2, Is.Not.Null);
        Assert.That(fixture2.Id, Is.EqualTo("NUnitTestProject.MyBaseFixture"));
        Assert.That(fixture2.FilePath, Is.EqualTo(documentPath));
        var tests2 = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = fixture2.Id }).ConfigureAwait(false);
        Assert.That(tests2, Has.Length.EqualTo(1));

        var fixture3 = result.FirstOrDefault(f => f.Name == "MyFixture");
        Assert.That(fixture3, Is.Not.Null);
        Assert.That(fixture3.Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        Assert.That(fixture3.FilePath, Is.EqualTo(documentPath));
        var tests3 = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = fixture3.Id }).ConfigureAwait(false);
        Assert.That(tests3, Has.Length.EqualTo(1));
    }
    [Test]
    public async Task DiscoverFixturesWithHierarchy3Test() {
        var documentPath = CreateUnitTestDocument(@"
public class MyFixture : MyBaseFixture {
    public override void MyTest() {}
}

[TestFixture]
public class MyBaseFixture {
    [Test]
    public virtual void MyTest() {
        Assert.Pass();
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(2));

        var fixture1 = result.FirstOrDefault(f => f.Name == "MyFixture");
        Assert.That(fixture1, Is.Not.Null);
        Assert.That(fixture1.Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        var tests1 = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = fixture1.Id }).ConfigureAwait(false);
        Assert.That(tests1, Has.Length.EqualTo(1));

        var fixture2 = result.FirstOrDefault(f => f.Name == "MyBaseFixture");
        Assert.That(fixture2, Is.Not.Null);
        Assert.That(fixture2.Id, Is.EqualTo("NUnitTestProject.MyBaseFixture"));
        var tests2 = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = fixture2.Id }).ConfigureAwait(false);
        Assert.That(tests2, Has.Length.EqualTo(1));
    }
    [Test]
    public async Task DiscoverFixturesWithNotExistsHierarchyTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture : NotExistFixture {
    [Test]
    public void MyTest() {}
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));

        result = await handler.Handle(new TestCaseParams { TextDocument = documentPath.CreateDocumentId(), FixtureId = result[0].Id }).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(1));
    }
    [Test]
    public async Task DiscoverFixturesInsideInnerClassTest() {
        var documentPath = CreateUnitTestDocument(@"
[TestFixture]
public class MyFixture {
    [Test]
    public void MyTest() {}

    public class InnerFixture {
        [Test]
        public void MyInnerTest() {}
    }
}");
        var arguments = new TestFixtureParams { TextDocument = ProjectFilePath.CreateDocumentId() };
        var result = await handler.Handle(arguments).ConfigureAwait(false);
        Assert.That(result, Has.Length.EqualTo(2));

        var fixture1 = result.FirstOrDefault(f => f.Name == "MyFixture");
        Assert.That(fixture1, Is.Not.Null);
        Assert.That(fixture1.Id, Is.EqualTo("NUnitTestProject.MyFixture"));
        Assert.That(fixture1.FilePath, Is.EqualTo(documentPath));
        Assert.That(fixture1.Range, Is.EqualTo(PositionExtensions.CreateRange(5, 0, 14, 1)));

        var fixture2 = result.FirstOrDefault(f => f.Name == "InnerFixture");
        Assert.That(fixture2, Is.Not.Null);
        Assert.That(fixture2.Id, Is.EqualTo("NUnitTestProject.MyFixture.InnerFixture"));
        Assert.That(fixture2.FilePath, Is.EqualTo(documentPath));
        Assert.That(fixture2.Range, Is.EqualTo(PositionExtensions.CreateRange(10, 4, 13, 5)));
    }
}