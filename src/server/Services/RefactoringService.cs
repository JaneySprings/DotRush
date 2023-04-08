using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace dotRush.Server.Services;

public class RefactoringService {
    public static RefactoringService Instance { get; private set; } = null!;

    private RefactoringService() {}

    public static void Initialize() {
        var service = new RefactoringService();
        Instance = service;
    }

    public List<Command> GetCodeActions(Document document, LanguageServer.Parameters.Range range) {
        var commands = new List<Command>();

        return commands;
    }
}