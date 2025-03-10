using DotRush.Debugging.NetCore.Testing.Explorer;
using DotRush.Debugging.NetCore.Testing.Models;
using NUnit.Framework;
using TestFixtureModel = DotRush.Debugging.NetCore.Testing.Models.TestFixture;

namespace DotRush.Debugging.NetCore.Tests;

public abstract class DiscoverTestsBase : TestFixture {
    protected abstract string TestFixtureAttr { get; }
    protected abstract string TestCaseAttr { get; }
    protected abstract string TestDataAttr { get; }

    private TestExplorer TestExplorer = null!;

    private static TestFixtureModel GetFixtureById(IEnumerable<TestFixtureModel> fixtures, string fixtureId) {
        var fixture = fixtures.FirstOrDefault(f => f.Id == fixtureId);
        Assert.That(fixture, Is.Not.Null, $"Test fixture with ID '{fixtureId}' not found");
        return fixture!;
    }
    private static TestCase GetTestCaseById(IEnumerable<TestCase> testCases, string testCaseId) {
        var testCase = testCases.FirstOrDefault(tc => tc.Id == testCaseId);
        Assert.That(testCase, Is.Not.Null, $"Test case with ID '{testCaseId}' not found");
        return testCase!;
    }

    [SetUp]
    public void SetUp() {
        TestExplorer = new TestExplorer();
    }

    [Test]
    public void DiscoverEmptyProjectTest() {
        var tests = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(tests, Is.Empty);
    }
    [Test]
    public void DiscoverSingleFixtureTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{}}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(fixtures, Is.Empty);
    }
    [Test]
    public void DiscoverSingleFixtureWithOneTestTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}

            public void MyTestHelper() {{
                Assert.Pass();
            }}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(9));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases, Has.Count.EqualTo(1));

            var testCase = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase.Name, Is.EqualTo("MyTest"));
            Assert.That(testCase.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase.Range!.End!.Line, Is.EqualTo(4));
        });
    }
    [Test]
    public void DiscoverSingleFixtureWithThreeTestsTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestCaseAttr}]
            public void MyTest2() {{ Assert.Pass(); }}

            [{TestCaseAttr}]
            public void MyTest3() {{
                Assert.Pass();
            }}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(12));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases, Has.Count.EqualTo(3));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));
            Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(4));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(6));

            var testCase3 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest3");
            Assert.That(testCase3.Name, Is.EqualTo("MyTest3"));
            Assert.That(testCase3.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase3.Range!.Start!.Line, Is.EqualTo(8));
            Assert.That(testCase3.Range!.End!.Line, Is.EqualTo(11));
        });
    }
    [Test]
    public void DiscoverSingleFixtureWithBlockScopedNamespaceTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject {{
            [{TestFixtureAttr}]
            public class MyFixture {{
                [{TestCaseAttr}]
                public void MyTest() {{}}
                [{TestCaseAttr}]
                public void MyTest2() {{ Assert.Pass(); }}

                [{TestCaseAttr}]
                public void MyTest3() {{
                    Assert.Pass();
                }}
            }}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases, Has.Count.EqualTo(3));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));

            var testCase3 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest3");
            Assert.That(testCase3.Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesInSingleFileTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestCaseAttr}]
            public void MyTest2() {{ Assert.Pass(); }}
        }}

        [{TestFixtureAttr}]
        public class MyFixture2 {{
            [{TestCaseAttr}]
            public void MyTest3() {{
                Assert.Pass();
            }}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(2));
        
        var fixture1 = GetFixtureById(fixtures, "TestProject.MyFixture");
        var fixture2 = GetFixtureById(fixtures, "TestProject.MyFixture2");
        
        Assert.Multiple(() => {
            Assert.That(fixture1.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture1.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture1.Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixture1.TestCases, Is.Not.Empty);
            Assert.That(fixture1.TestCases, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));

            var testCase2 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));

            Assert.That(fixture2.Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixture2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture2.Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(fixture2.Range!.End!.Line, Is.EqualTo(15));
            Assert.That(fixture2.TestCases, Is.Not.Empty);
            Assert.That(fixture2.TestCases, Has.Count.EqualTo(1));

            var testCase3 = GetTestCaseById(fixture2.TestCases!, "TestProject.MyFixture2.MyTest3");
            Assert.That(testCase3.Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverFixturesInsideDirectivesTest() {
        CreateFileInProject("SingleFixture.cs", $@"#if DEBUGTEST
        namespace TestProject;

        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}
        #endif
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases, Has.Count.EqualTo(1));

            var testCase = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase.Name, Is.EqualTo("MyTest"));
            Assert.That(testCase.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase.Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(testCase.Range!.End!.Line, Is.EqualTo(6));
        });
    }
    [Test]
    public void DiscoverFixturesWithDuplicateTestsTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestCaseAttr}]
            public void MyTest() {{ Assert.Pass(); }}
        }}
        #endif
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.TestCases, Has.Count.EqualTo(1));

            var testCase = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase.Name, Is.EqualTo("MyTest"));
            Assert.That(testCase.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
        });
    }
    [Test]
    public void DiscoverFixturesWithTestCasesTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestDataAttr}(1)]
            [{TestDataAttr}(2)]
            public void MyTest2(int a) {{ Assert.Pass(); }}

            [{TestCaseAttr}]
            [{TestDataAttr}(1)]
            [{TestDataAttr}(2)]
            public void MyTest3(int a) {{ Assert.Pass(); }}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.TestCases, Has.Count.EqualTo(3));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(7));

            var testCase3 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest3");
            Assert.That(testCase3.Name, Is.EqualTo("MyTest3"));
            Assert.That(testCase3.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase3.Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(testCase3.Range!.End!.Line, Is.EqualTo(12));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestCaseAttr}]
            public void MyTest2() {{ Assert.Pass(); }}
        }}
        ");
        CreateFileInProject("SingleFixture2.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture2 {{
            [{TestCaseAttr}]
            public void MyTest3() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(2));
        
        var fixture1 = GetFixtureById(fixtures, "TestProject.MyFixture");
        var fixture2 = GetFixtureById(fixtures, "TestProject.MyFixture2");
        
        Assert.Multiple(() => {
            Assert.That(fixture1.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture1.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture1.Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixture1.TestCases, Is.Not.Empty);
            Assert.That(fixture1.TestCases, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));

            var testCase2 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));

            Assert.That(fixture2.Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixture2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixture2.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture2.Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixture2.TestCases, Is.Not.Empty);
            Assert.That(fixture2.TestCases, Has.Count.EqualTo(1));

            var testCase3 = GetTestCaseById(fixture2.TestCases!, "TestProject.MyFixture2.MyTest3");
            Assert.That(testCase3.Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesWithSingleNameTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public partial class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{
            }}
        }}
        ");
        CreateFileInProject("SingleFixture2.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public partial class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest2() {{
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.Name, Is.EqualTo("MyFixture"));
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")).Or.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(6));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases!, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.Name, Is.EqualTo("MyTest"));
            Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(5));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.Name, Is.EqualTo("MyTest2"));
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(5));
        });
    }

    [Test]
    public void DiscoverFixturesWithHierarchyTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture : IFixture, MyBaseFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}

        public abstract class MyBaseFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(5));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases!, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(4));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(10));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(13));
        });
    }
    [Test]
    public void DiscoverFixturesWithHierarchy2Test() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        public class MyFixture2 : MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}

        public class MyFixture : MyBaseFixture {{ }}

        [{TestFixtureAttr}]
        public class MyBaseFixture : IFixture {{
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}

        public interface IFixture {{
            [{TestCaseAttr}]
            public void MyTest3() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(3));
        
        var fixture1 = GetFixtureById(fixtures, "TestProject.MyFixture2");
        var fixture2 = GetFixtureById(fixtures, "TestProject.MyFixture");
        var fixture3 = GetFixtureById(fixtures, "TestProject.MyBaseFixture");
        
        Assert.Multiple(() => {
            Assert.That(fixture1.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture1.Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixture1.TestCases, Is.Not.Empty);
            Assert.That(fixture1.TestCases!, Has.Count.EqualTo(2));
            
            Assert.That(fixture2.Range!.Start!.Line, Is.EqualTo(6));
            Assert.That(fixture2.Range!.End!.Line, Is.EqualTo(6));
            Assert.That(fixture2.TestCases, Is.Not.Empty);
            Assert.That(fixture2.TestCases!, Has.Count.EqualTo(1));

            Assert.That(fixture3.Range!.Start!.Line, Is.EqualTo(8));
            Assert.That(fixture3.Range!.End!.Line, Is.EqualTo(14));
            Assert.That(fixture3.TestCases, Is.Not.Empty);
            Assert.That(fixture3.TestCases!, Has.Count.EqualTo(1));
        });
    }
    [Test]
    public void DiscoverFixturesWithHierarchy3Test() {
        CreateFileInProject("Fixture.cs", $@"namespace TestProject;
        public class MyFixture : MyBaseFixture {{ }}
        ");
        CreateFileInProject("Fixture2.cs", $@"namespace TestProject;
        public class MyFixture2 : MyBaseFixture {{ }}
        ");
        CreateFileInProject("FixtureBase.cs", $@"namespace TestProject;
        public abstract class MyBaseFixture : MyBaseFixture2 {{
            [{TestCaseAttr}]
            public void MyTest() {{
                Assert.Pass();
            }}
        }}
        ");
        CreateFileInProject("FixtureBase2.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public abstract class MyBaseFixture2 {{
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(2));
        
        var fixture1 = GetFixtureById(fixtures, "TestProject.MyFixture");
        var fixture2 = GetFixtureById(fixtures, "TestProject.MyFixture2");
        
        Assert.Multiple(() => {
            Assert.That(fixture1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "Fixture.cs")));
            Assert.That(fixture1.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture1.Range!.End!.Line, Is.EqualTo(1));
            Assert.That(fixture1.TestCases, Is.Not.Empty);
            Assert.That(fixture1.TestCases!, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase.cs")));
            Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(2));
            Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(5));

            var testCase2 = GetTestCaseById(fixture1.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase2.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(6));

            Assert.That(fixture2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "Fixture2.cs")));
            Assert.That(fixture2.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture2.Range!.End!.Line, Is.EqualTo(1));
            Assert.That(fixture2.TestCases, Is.Not.Empty);
            Assert.That(fixture2.TestCases!, Has.Count.EqualTo(2));

            var testCase3 = GetTestCaseById(fixture2.TestCases!, "TestProject.MyFixture2.MyTest");
            Assert.That(testCase3.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase.cs")));
            Assert.That(testCase3.Range!.Start!.Line, Is.EqualTo(2));
            Assert.That(testCase3.Range!.End!.Line, Is.EqualTo(5));

            var testCase4 = GetTestCaseById(fixture2.TestCases!, "TestProject.MyFixture2.MyTest2");
            Assert.That(testCase4.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase2.cs")));
            Assert.That(testCase4.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase4.Range!.End!.Line, Is.EqualTo(6));
        });
    }
    [Test]
    public void DiscoverFixturesWithNotExistsHierarchyTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture : NotExistFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(5));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases, Has.Count.EqualTo(1));

            var testCase = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase.Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(testCase.Range!.End!.Line, Is.EqualTo(4));
        });
    }
    [Test]
    public void DiscoverFixturesWithPartialHierarchyTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture : MyBaseFixture {{
        }}

        public abstract class MyBaseFixture {{
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}
        public abstract class MyBaseFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        
        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.Multiple(() => {
            Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixture.Range!.End!.Line, Is.EqualTo(3));
            Assert.That(fixture.TestCases, Is.Not.Empty);
            Assert.That(fixture.TestCases!, Has.Count.EqualTo(2));

            var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
            Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(6));
            Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(9));

            var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
            Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(12));
            Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(13));
        });
    }
    [Test]
    public void DiscoverFixturesWithHierarchyAndGenericTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture : MyBaseFixture<int> {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}

        public abstract class MyBaseFixture<T> {{
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));

        var fixture = GetFixtureById(fixtures, "TestProject.MyFixture");
        Assert.That(fixture.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
        Assert.That(fixture.Range!.Start!.Line, Is.EqualTo(1));
        Assert.That(fixture.Range!.End!.Line, Is.EqualTo(5));
        Assert.That(fixture.TestCases, Is.Not.Empty);
        Assert.That(fixture.TestCases!, Has.Count.EqualTo(2));

        var testCase1 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest");
        Assert.That(testCase1.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
        Assert.That(testCase1.Range!.Start!.Line, Is.EqualTo(3));
        Assert.That(testCase1.Range!.End!.Line, Is.EqualTo(4));

        var testCase2 = GetTestCaseById(fixture.TestCases!, "TestProject.MyFixture.MyTest2");
        Assert.That(testCase2.FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
        Assert.That(testCase2.Range!.Start!.Line, Is.EqualTo(8));
        Assert.That(testCase2.Range!.End!.Line, Is.EqualTo(11));
    }
}