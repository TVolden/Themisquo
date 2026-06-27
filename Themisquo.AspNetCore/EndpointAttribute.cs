namespace Themisquo.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EndpointAttribute(string Url, string? Method = null) : Attribute
    {
        public string Url { get; } = Url;
        public string? Method { get; } = Method;
    }
}
