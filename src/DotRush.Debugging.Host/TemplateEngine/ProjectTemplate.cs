using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;

namespace DotRush.Debugging.Host.TemplateEngine;

public class ProjectTemplate {
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("shortName")] public string? ShortName { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("tags")] public string[]? Classifications { get; set; }

    public ProjectTemplate(ITemplateInfo templateInfo) {
        Name = templateInfo.Name;
        ShortName = templateInfo.ShortNameList.Count > 0 ? templateInfo.ShortNameList[0] : null;
        Author = templateInfo.Author;
        Description = templateInfo.Description;
        Classifications = templateInfo.Classifications?.ToArray();
    }
}