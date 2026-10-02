using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Themisquo.AspNetCore.Siren
{
    /// <summary>A Siren entity document (<c>application/vnd.siren+json</c>).</summary>
    public record SirenEntity(
        [property: JsonPropertyName("class"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Class,
        [property: JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Properties,
        [property: JsonPropertyName("entities"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenSubEntity[]? Entities,
        [property: JsonPropertyName("links"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenLink[]? Links);

    /// <summary>An embedded Siren entity, related to its parent through <see cref="Rel"/>.</summary>
    public record SirenSubEntity(
        [property: JsonPropertyName("rel")] string[] Rel,
        [property: JsonPropertyName("class"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Class,
        [property: JsonPropertyName("properties"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Properties,
        [property: JsonPropertyName("entities"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenSubEntity[]? Entities,
        [property: JsonPropertyName("links"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SirenLink[]? Links);

    /// <summary>A Siren link to another resource.</summary>
    public record SirenLink(
        [property: JsonPropertyName("rel")] string[] Rel,
        [property: JsonPropertyName("href")] string Href);
}
