using DotRush.Debugging.NetCore.Testing.Explorer;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public abstract class DiscoverTestsBase : TestFixture {
    protected abstract string TestFixtureAttr { get; }
    protected abstract string TestCaseAttr { get; }
    protected abstract string TestDataAttr { get; }

    private TestExplorer TestExplorer = null!;

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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(9));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(12));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(3));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(6));

            Assert.That(fixtures[0].TestCases!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Range!.Start!.Line, Is.EqualTo(8));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Range!.End!.Line, Is.EqualTo(11));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(3));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[0].TestCases!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
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
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[1].Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixtures[1].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(15));
            Assert.That(fixtures[1].TestCases, Is.Not.Empty);
            Assert.That(fixtures[1].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[1].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture2.MyTest3"));
            Assert.That(fixtures[1].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest3"));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(6));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(3));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(7));

            Assert.That(fixtures[0].TestCases!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(fixtures[0].TestCases!.ElementAt(2).Range!.End!.Line, Is.EqualTo(12));
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
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[1].Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixtures[1].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[1].TestCases, Is.Not.Empty);
            Assert.That(fixtures[1].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[1].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture2.MyTest3"));
            Assert.That(fixtures[1].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesWithSingleNameTest() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public partial class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}
        ");
        CreateFileInProject("SingleFixture2.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public partial class MyFixture {{
            [{TestCaseAttr}]
            public void MyTest2() {{
                Assert.Pass();
            }}
        }}
        ");

        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases!, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(6));
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases!, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(10));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(13));
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
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases!, Has.Count.EqualTo(2));
            
            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(6));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(6));
            Assert.That(fixtures[1].TestCases, Is.Not.Empty);
            Assert.That(fixtures[1].TestCases!, Has.Count.EqualTo(1));

            Assert.That(fixtures[2].Id, Is.EqualTo("TestProject.MyBaseFixture"));
            Assert.That(fixtures[2].Range!.Start!.Line, Is.EqualTo(8));
            Assert.That(fixtures[2].Range!.End!.Line, Is.EqualTo(14));
            Assert.That(fixtures[2].TestCases, Is.Not.Empty);
            Assert.That(fixtures[2].TestCases!, Has.Count.EqualTo(1));
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
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "Fixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases!, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(2));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(5));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase2.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(6));
    

            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[1].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "Fixture2.cs")));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(1));
            Assert.That(fixtures[1].TestCases, Is.Not.Empty);
            Assert.That(fixtures[1].TestCases!, Has.Count.EqualTo(2));

            Assert.That(fixtures[1].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture2.MyTest"));
            Assert.That(fixtures[1].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase.cs")));
            Assert.That(fixtures[1].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(2));
            Assert.That(fixtures[1].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(5));

            Assert.That(fixtures[1].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture2.MyTest2"));
            Assert.That(fixtures[1].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "FixtureBase2.cs")));
            Assert.That(fixtures[1].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[1].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(6));
        });
    }
    [Test]
    public void DiscoverFixturesWithHierarchy4Test() {
        CreateFileInProject("SingleFixture.cs", $@"namespace TestProject;
        [{TestFixtureAttr}]
        public class MyFixture : NotExistFixture {{
            [{TestCaseAttr}]
            public void MyTest() {{}}
        }}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases, Has.Count.EqualTo(1));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
        });
    }
    [Test]
    public void DiscoverFixturesWithHierarchy5Test() {
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
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[0].Range!.End!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].TestCases, Is.Not.Empty);
            Assert.That(fixtures[0].TestCases!, Has.Count.EqualTo(2));

            Assert.That(fixtures[0].TestCases!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(6));
            Assert.That(fixtures[0].TestCases!.ElementAt(0).Range!.End!.Line, Is.EqualTo(9));

            Assert.That(fixtures[0].TestCases!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(12));
            Assert.That(fixtures[0].TestCases!.ElementAt(1).Range!.End!.Line, Is.EqualTo(13));
        });
    }
}