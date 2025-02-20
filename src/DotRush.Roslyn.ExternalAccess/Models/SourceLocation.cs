namespace DotRush.Roslyn.ExternalAccess.Models;

public class SourceLocation {
    public string? FileName { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}