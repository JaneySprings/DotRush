using DotRush.Roslyn.CodeAnalysis.Embedded.Refactorings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

[Ignore("Fix issues with refactoring")]
public class ExtractToNewMethodProviderTests : RefactoringsTestFixture {
    private readonly CodeRefactoringProvider refactoringProvider = new ExtractToNewMethodRefactoringProvider();

    [Test]
    public async Task TestExtractSingleStatement() {
        const string source = @"
class TestClass
{
    public int TestMethod()
    {
        int a = 5;
        int b = 10;
        return a + b;
    }
}";
        Document document = CreateDocument($"{nameof(TestExtractSingleStatement)}.cs", source);
        var tree = await document.GetSyntaxTreeAsync();
        var span = new TextSpan(source.IndexOf("int b = 10;"), "int b = 10;".Length);

        // Act
        var actions = await GetRefactoringActionsAsync(refactoringProvider, document, span);

        // Assert
        Assert.That(actions, Has.Count.EqualTo(1));
        Assert.That(actions[0].Title, Does.Contain("Extract to new method"));

        // Apply the refactoring
        var newDocument = await ApplyRefactoringAsync(actions[0], document);
        var newText = await newDocument.GetTextAsync();

        // Check that both methods exist
        var newSource = newText.ToString();
        Assert.That(newSource, Does.Contain("TestMethodPart"));
        Assert.That(newSource, Does.Contain("private void TestMethodPart()"));
    }
    [Test]
    public async Task TestExtractWithReturnStatement() {
        const string source = @"
class TestClass
{
    public int TestMethod()
    {
        int a = 5;
        int b = 10;
        return a + b;
    }
}";
        Document document = CreateDocument($"{nameof(TestExtractWithReturnStatement)}.cs", source);
        var tree = await document.GetSyntaxTreeAsync();
        var span = new TextSpan(source.IndexOf("return a + b;"), "return a + b;".Length);

        // Act
        var actions = await GetRefactoringActionsAsync(refactoringProvider, document, span);

        // Assert
        Assert.That(actions, Has.Count.EqualTo(1));

        // Apply the refactoring
        var newDocument = await ApplyRefactoringAsync(actions[0], document);
        var newText = await newDocument.GetTextAsync();

        // Check that new method has proper return type
        var newSource = newText.ToString();
        Assert.That(newSource, Does.Contain("private int TestMethodPart()"));
        Assert.That(newSource, Does.Contain("return TestMethodPart();"));
    }
    [Test]
    public async Task TestExtractWithParameters() {
        const string source = @"
class TestClass
{
    public int TestMethod(int x, string y)
    {
        int a = x + 5;
        string message = y + ""test"";
        return a;
    }
}";
        Document document = CreateDocument($"{nameof(TestExtractWithParameters)}.cs", source);
        var tree = await document.GetSyntaxTreeAsync();
        var span = new TextSpan(source.IndexOf("int a = x + 5;"), "int a = x + 5;".Length);

        // Act
        var actions = await GetRefactoringActionsAsync(refactoringProvider, document, span);

        // Assert
        Assert.That(actions, Has.Count.EqualTo(1));

        // Apply the refactoring
        var newDocument = await ApplyRefactoringAsync(actions[0], document);
        var newText = await newDocument.GetTextAsync();

        // Check that parameters are properly handled
        var newSource = newText.ToString();
        Assert.That(newSource, Does.Contain("private void TestMethodPart(int x)"));
        Assert.That(newSource, Does.Contain("TestMethodPart(x);"));
    }
    [Test]
    public async Task TestNoExtractWithoutSelection() {
        const string source = @"
class TestClass
{
    public int TestMethod()
    {
        int a = 5;
        int b = 10;
        return a + b;
    }
}";
        Document document = CreateDocument($"{nameof(TestNoExtractWithoutSelection)}.cs", source);
        var tree = await document.GetSyntaxTreeAsync();
        // Select position without statements
        var span = new TextSpan(source.IndexOf("class TestClass"), 1);

        // Act
        var actions = await GetRefactoringActionsAsync(refactoringProvider, document, span);

        // Assert
        Assert.That(actions, Is.Empty);
    }
}