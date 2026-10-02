namespace Themisquo.AspNetCore
{
    /// <summary>
    /// A writer for endpoint responses in a specific format. Several can be registered: the one whose
    /// <see cref="MediaType"/> the request's Accept header prefers is used (see <see cref="ThemisquoResultWriterOptions"/>).
    /// </summary>
    public interface IResultWriter
    {
        /// <summary>The media type the writer produces, such as <c>application/vnd.siren+json</c>.</summary>
        string MediaType { get; }
    }
}
