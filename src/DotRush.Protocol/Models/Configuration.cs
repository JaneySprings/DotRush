using System.Text.Json;

namespace DotRush.Protocol.Models;

public class DidChangeConfigurationParams {
    public JsonElement Settings { get; set; }
}
