using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>A Siren entity document (<c>application/vnd.siren+json</c>).</summary>
    public record SirenEntity(
        [property: JsonPropertyName("class"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Class,
        [property: JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Properties,
        [property: JsonPropertyName("entities"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenSubEntity[]? Entities,
        [property: JsonPropertyName("links"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenLink[]? Links,
        [property: JsonPropertyName("actions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenAction[]? Actions = null);

    /// <summary>An embedded Siren entity, related to its parent through <see cref="Rel"/>.</summary>
    public record SirenSubEntity(
        [property: JsonPropertyName("rel")] string[] Rel,
        [property: JsonPropertyName("class"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Class,
        [property: JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Properties,
        [property: JsonPropertyName("entities"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenSubEntity[]? Entities,
        [property: JsonPropertyName("links"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenLink[]? Links,
        [property: JsonPropertyName("actions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenAction[]? Actions = null);

    /// <summary>A Siren link to another resource.</summary>
    public record SirenLink(
        [property: JsonPropertyName("rel")] string[] Rel,
        [property: JsonPropertyName("href")] string Href);

    /// <summary>A Siren action: a command that can be sent to the entity.</summary>
    public record SirenAction(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("href")] string Href,
        [property: JsonPropertyName("type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Type,
        [property: JsonPropertyName("fields"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenField[]? Fields,
        [property: JsonPropertyName("title"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null);

    /// <summary>An input field of a <see cref="SirenAction"/>; <see cref="Type"/> is an HTML input type.</summary>
    public record SirenField(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type);
}
