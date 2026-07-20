namespace DotRush.Roslyn.Server.Extensions;

public class MarkdownFormattingOptions {
    public MarkdownFormattingOptions() {
        //just defaults
        NewLine = "\n";
    }

    public string NewLine { get; set; }
}