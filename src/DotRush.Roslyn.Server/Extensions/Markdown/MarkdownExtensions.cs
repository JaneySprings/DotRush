using System.Text;

namespace DotRush.Roslyn.Server.Extensions;

public static class MarkdownExtensions {
    public static string Create(string content, string lang = "") {
        var sb = new StringBuilder();
        sb.AppendLine("```" + lang);
        sb.AppendLine(content);
        sb.AppendLine("```");
        return sb.ToString();
    }
}