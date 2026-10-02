namespace Themisquo.AspNetCore
{
    /// <summary>
    /// How query and command endpoints choose between plain JSON and the registered <see cref="IResultWriter"/>s.
    /// </summary>
    /// <remarks>
    /// The request's Accept header is matched in order of preference (its <c>q</c> values) against the writers' media
    /// types and <c>application/json</c>. A wildcard such as <c>*/*</c>, a missing Accept header, or one that matches
    /// nothing, gets the default: the writer for <see cref="DefaultMediaType"/>, or else plain JSON.
    /// </remarks>
    public class ThemisquoResultWriterOptions
    {
        /// <summary>The media type of the writer to use by default; <c>null</c> for plain JSON.</summary>
        public string? DefaultMediaType { get; set; }
    }
}
