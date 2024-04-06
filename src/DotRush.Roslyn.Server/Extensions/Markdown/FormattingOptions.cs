namespace DotRush.Roslyn.Server.Extensions;

public class FormattingOptions {
    public FormattingOptions() {
        //just defaults
        NewLine = "\n";
    }

    public string NewLine { get; set; }
}