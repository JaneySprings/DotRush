using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotRush.Common.Extensions;

namespace DotRush.Roslyn.Server.Extensions;

public static partial class MarkdownExtensions {
    public static string CreateDocumentation(string member, string lang = "") {
        return CreateDocumentation(member, null, lang);
    }
    public static string CreateDocumentation(string member, string? documentation, string lang) {
        var sb = new StringBuilder();
        sb.AppendLine("```" + lang);
        sb.AppendLine(member);
        sb.AppendLine("```");

        if (!string.IsNullOrEmpty(documentation))
            sb.AppendLine(CreateFromXml(documentation));

        return sb.ToString();
    }

    private static string CreateFromXml(string content) {
        var sb = new StringBuilder();

        try {
            var root = XDocument.Parse(content).Root;
            if (root == null)
                return string.Empty;

            var memberElement = root.Name == "member" ? root : root.Element("member");
            if (memberElement == null)
                return string.Empty;

            var summaryElement = memberElement.Element("summary");
            if (summaryElement != null)
                sb.AppendLine(ProcessInlineContent(summaryElement)).AppendLine();

            var typeParamElements = memberElement.Elements("typeparam");
            if (typeParamElements.Any()) {
                sb.AppendLine($"**Type Parameters:**").AppendLine();
                typeParamElements.ForEach(element => {
                    var paramName = element.Attribute("name")?.Value ?? "T";
                    sb.AppendLine($"`{paramName}` — {ProcessInlineContent(element)}").AppendLine();
                });
                sb.AppendLine();
            }

            var paramElements = memberElement.Elements("param");
            if (paramElements.Any()) {
                sb.AppendLine($"**Parameters:**").AppendLine();
                paramElements.ForEach(element => {
                    var paramName = element.Attribute("name")?.Value ?? "parameter";
                    sb.AppendLine($"`{paramName}` — {ProcessInlineContent(element)}").AppendLine();
                });
                sb.AppendLine();
            }

            var exceptionElements = memberElement.Elements("exception");
            if (exceptionElements.Any()) {
                sb.AppendLine($"**Exceptions:**").AppendLine();
                exceptionElements.ForEach(element => {
                    var cref = element.Attribute("cref")?.Value ?? "Exception";
                    sb.AppendLine($"`{TrimMemberName(cref)}` — {ProcessInlineContent(element)}").AppendLine();
                });
                sb.AppendLine();
            }

            // case "example":
            //     sb.AppendLine(ProcessInlineContent(element));
            //     break;
            // case "value":
            //     sb.AppendLine(ProcessInlineContent(element));
            //     break;

            var returnsElement = memberElement.Element("returns");
            if (returnsElement != null) {
                sb.AppendLine("**Returns:**").AppendLine();
                sb.AppendLine(ProcessInlineContent(returnsElement)).AppendLine();
            }

            var remarksElement = memberElement.Element("remarks");
            if (remarksElement != null) {
                sb.AppendLine("**Remarks:**").AppendLine();
                sb.AppendLine(ProcessInlineContent(remarksElement)).AppendLine();
            }
        } catch {
            return string.Empty;
        }

        return sb.ToString().Trim();
    }
    private static string ProcessInlineContent(XElement element) {
        var sb = new StringBuilder();

        foreach (var node in element.Nodes()) {
            if (node is XText textNode) {
                sb.Append(textNode.Value.Trim());
            }
            else if (node is XElement childElement) {
                switch (childElement.Name.LocalName.ToLower()) {
                    case "see":
                        var cref = childElement.Attribute("cref")?.Value;
                        if (!string.IsNullOrEmpty(cref))
                            sb.Append($"`{TrimMemberName(cref)}`");
                        else
                            sb.Append(childElement.Value);
                        break;

                    case "paramref":
                        var paramName = childElement.Attribute("name")?.Value;
                        sb.Append($"`{paramName}`");
                        break;

                    case "typeparamref":
                        var typeParamName = childElement.Attribute("name")?.Value;
                        sb.Append($"`{typeParamName}`");
                        break;

                    case "c":
                        sb.Append($"`{childElement.Value}`");
                        break;

                    case "code":
                        sb.AppendLine();
                        sb.AppendLine("```");
                        sb.AppendLine(childElement.Value);
                        sb.AppendLine("```");
                        break;

                    case "para":
                        sb.AppendLine();
                        sb.AppendLine(ProcessInlineContent(childElement));
                        break;

                    case "br":
                        sb.AppendLine();
                        sb.AppendLine();
                        break;

                    default:
                        sb.Append(ProcessInlineContent(childElement));
                        break;
                }
            }
        }

        return sb.ToString().Trim();
    }
    private static string TrimMemberName(string fullName) {
        if (string.IsNullOrEmpty(fullName))
            return string.Empty;

        if (fullName.Length > 2 && fullName[1] == ':')
            fullName = fullName.Substring(2);

        return AccessExpressionRegex().Replace(fullName, string.Empty);
    }

    [GeneratedRegex(@"(\w+\.)+")]
    private static partial Regex AccessExpressionRegex();
}