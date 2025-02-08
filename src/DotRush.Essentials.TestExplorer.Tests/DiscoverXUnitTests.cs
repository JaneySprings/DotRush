using DotRush.Essentials.TestExplorer;
using DotRush.Essentials.TestExplorer.Tests;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

public class DiscoverXUnitTests : TestFixture {

    [Test]
    public void DiscoverEmptyProjectTest() {
        var tests = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(tests, Is.Empty);
    }
    [Test]
    public void DiscoverSingleFixtureTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {}
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(fixtures, Is.Empty);
    }
    [Test]
    public void DiscoverSingleFixtureWithOneTestTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}

            public void MyTestHelper() {
                Assert.Pass();
            }
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(1));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixtures[0].Children!.ElementAt(0).Children, Is.Null.Or.Empty);
        });
    }
    [Test]
    public void DiscoverSingleFixtureWithThreeTestsTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
            [Fact]
            public void MyTest2() { Assert.Pass(); }

            [Fact]
            public void MyTest3() {
                Assert.Pass();
            }
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(3));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixtures[0].Children!.ElementAt(0).Children, Is.Null.Or.Empty);

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].Children!.ElementAt(1).Range!.End!.Line, Is.EqualTo(6));
            Assert.That(fixtures[0].Children!.ElementAt(1).Children, Is.Null.Or.Empty);

            Assert.That(fixtures[0].Children!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].Children!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
            Assert.That(fixtures[0].Children!.ElementAt(2).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(2).Range!.Start!.Line, Is.EqualTo(8));
            Assert.That(fixtures[0].Children!.ElementAt(2).Range!.End!.Line, Is.EqualTo(11));
            Assert.That(fixtures[0].Children!.ElementAt(2).Children, Is.Null.Or.Empty);
        });
    }
    [Test]
    public void DiscoverSingleFixtureWithBlockScopedNamespaceTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject {
            [TestFixture]
            public class MyFixture {
                [Fact]
                public void MyTest() {}
                [Fact]
                public void MyTest2() { Assert.Pass(); }

                [Fact]
                public void MyTest3() {
                    Assert.Pass();
                }
            }
        }
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Name, Is.EqualTo("MyFixture"));
            Assert.That(fixtures[0].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(3));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[0].Children!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].Children!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesInSingleFileTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
            [Fact]
            public void MyTest2() { Assert.Pass(); }
        }

        [TestFixture]
        public class MyFixture2 {
            [Fact]
            public void MyTest3() {
                Assert.Pass();
            }
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(2));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[1].Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixtures[1].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(15));
            Assert.That(fixtures[1].Children, Is.Not.Empty);
            Assert.That(fixtures[1].Children!.Count, Is.EqualTo(1));

            Assert.That(fixtures[1].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture2.MyTest3"));
            Assert.That(fixtures[1].Children!.ElementAt(0).Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverFixturesInsideDirectivesTest() {
        CreateProjectFile("SingleFixture.cs", @"#if DEBUGTEST
        namespace TestProject;

        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(1));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.End!.Line, Is.EqualTo(6));
            Assert.That(fixtures[0].Children!.ElementAt(0).Children, Is.Null.Or.Empty);
        });
    }
    [Test]
    public void DiscoverFixturesWithDuplicateTestsTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
            [Fact]
            public void MyTest() { Assert.Pass(); }
        }
        #endif
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(1));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Children, Is.Null.Or.Empty);
        });
    }
    [Test]
    public void DiscoverFixturesWithTestCasesTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
            [Theory(1)]
            [Theory(2)]
            public void MyTest2(int a) { Assert.Pass(); }

            [Fact]
            [Theory(1)]
            [Theory(2)]
            public void MyTest3(int a) { Assert.Pass(); }
        }
        #endif
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath).ToArray();
        Assert.That(fixtures, Is.Not.Empty);
        Assert.That(fixtures.Count, Is.EqualTo(1));
        Assert.Multiple(() => {
            Assert.That(fixtures[0].Id, Is.EqualTo("TestProject.MyFixture"));
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(3));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(1).Range!.Start!.Line, Is.EqualTo(5));
            Assert.That(fixtures[0].Children!.ElementAt(1).Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[0].Children!.ElementAt(1).Children, Is.Null.Or.Empty);

            Assert.That(fixtures[0].Children!.ElementAt(2).Id, Is.EqualTo("TestProject.MyFixture.MyTest3"));
            Assert.That(fixtures[0].Children!.ElementAt(2).Name, Is.EqualTo("MyTest3"));
            Assert.That(fixtures[0].Children!.ElementAt(2).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(2).Range!.Start!.Line, Is.EqualTo(9));
            Assert.That(fixtures[0].Children!.ElementAt(2).Range!.End!.Line, Is.EqualTo(12));
            Assert.That(fixtures[0].Children!.ElementAt(2).Children, Is.Null.Or.Empty);
        });
    }
    [Test]
    public void DiscoverMultipleFixturesTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Fact]
            public void MyTest() {}
            [Fact]
            public void MyTest2() { Assert.Pass(); }
        }
        ");
        CreateProjectFile("SingleFixture2.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture2 {
            [Fact]
            public void MyTest3() {
                Assert.Pass();
            }
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(2));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));

            Assert.That(fixtures[1].Id, Is.EqualTo("TestProject.MyFixture2"));
            Assert.That(fixtures[1].Name, Is.EqualTo("MyFixture2"));
            Assert.That(fixtures[1].FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixtures[1].Range!.Start!.Line, Is.EqualTo(1));
            Assert.That(fixtures[1].Range!.End!.Line, Is.EqualTo(7));
            Assert.That(fixtures[1].Children, Is.Not.Empty);
            Assert.That(fixtures[1].Children!.Count, Is.EqualTo(1));

            Assert.That(fixtures[1].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture2.MyTest3"));
            Assert.That(fixtures[1].Children!.ElementAt(0).Name, Is.EqualTo("MyTest3"));
        });
    }
    [Test]
    public void DiscoverMultipleFixturesWithSingleNameTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public partial class MyFixture {
            [Fact]
            public void MyTest() {}
        }
        ");
        CreateProjectFile("SingleFixture2.cs", @"namespace TestProject;
        [TestFixture]
        public partial class MyFixture {
            [Fact]
            public void MyTest2() {
                Assert.Pass();
            }
        }
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
            Assert.That(fixtures[0].Children, Is.Not.Empty);
            Assert.That(fixtures[0].Children!.Count, Is.EqualTo(2));

            Assert.That(fixtures[0].Children!.ElementAt(0).Id, Is.EqualTo("TestProject.MyFixture.MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).Name, Is.EqualTo("MyTest"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixtures[0].Children!.ElementAt(0).Children, Is.Null.Or.Empty);

            Assert.That(fixtures[0].Children!.ElementAt(1).Id, Is.EqualTo("TestProject.MyFixture.MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(1).Name, Is.EqualTo("MyTest2"));
            Assert.That(fixtures[0].Children!.ElementAt(0).FilePath, Is.EqualTo(Path.Combine(TestProjectPath, "SingleFixture2.cs")));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.Start!.Line, Is.EqualTo(3));
            Assert.That(fixtures[0].Children!.ElementAt(0).Range!.End!.Line, Is.EqualTo(4));
            Assert.That(fixtures[0].Children!.ElementAt(1).Children, Is.Null.Or.Empty);
        });
    }
}