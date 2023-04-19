using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Extensions;

public static class XmlConverter {
    public static void XmlTextToMarkdown(ISymbol symbol, StringBuilder builder) {
        builder.Append("```csharp\n");
        builder.Append(symbol.Kind.ToNamedString());
        builder.Append(" ");
        builder.Append(symbol.Name);
        builder.Append(" from ");
        builder.Append(symbol.ContainingAssembly.Name);
        builder.Append("\n```\n\n");

        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(xml))
            return;

        var summary = XElement.Parse($"<doc>{xml}</doc>").Element("summary")?.Value;
        if (string.IsNullOrEmpty(summary))
            return;

        builder.Append(summary.Trim());
    }
}
