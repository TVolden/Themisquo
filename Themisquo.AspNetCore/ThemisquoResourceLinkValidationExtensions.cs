using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Checks the links declared on the mapped query endpoints, so a declaration that can never produce a link is caught
    /// at startup instead of silently missing from responses.
    /// </summary>
    public static class ThemisquoResourceLinkValidationExtensions
    {
        /// <summary>
        /// Verifies, for each query mapped on <paramref name="endpoints"/>, that:
        /// <list type="bullet">
        /// <item>every query linked with <see cref="ResourceLinkAttribute{TQuery}"/> or <see cref="ItemLinkAttribute{TQuery}"/>
        /// is mapped as a GET query endpoint;</item>
        /// <item><see cref="ItemLinkAttribute"/>s and <see cref="ResourceAttribute.AutoItemLinks"/> are only on list queries.</item>
        /// </list>
        /// Call it after mapping the endpoints.
        /// </summary>
        /// <exception cref="InvalidResourceLinkException">Thrown when one or more declarations are invalid.</exception>
        public static IEndpointRouteBuilder ValidateResourceLinks(this IEndpointRouteBuilder endpoints)
        {
            var queries = endpoints.DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .Select(endpoint => endpoint.Metadata.GetMetadata<QueryEndpointMetadata>())
                .OfType<QueryEndpointMetadata>()
                .ToList();
            var getQueryTypes = queries
                .Where(metadata => HttpMethods.IsGet(metadata.HttpMethod))
                .Select(metadata => metadata.QueryType)
                .ToHashSet();

            var errors = new List<string>();
            foreach (var queryType in queries.Select(metadata => metadata.QueryType).Distinct())
            {
                var resultType = ResourceConventions.GetQueryResultType(queryType);
                var isList = resultType is not null && ResourceConventions.GetCollectionElementType(resultType) is not null;
                var itemLinks = ResourceConventions.GetDeclaredLinks(queryType, forItems: true);

                foreach (var declaration in ResourceConventions.GetDeclaredLinks(queryType, forItems: false).Concat(itemLinks))
                {
                    if (declaration.QueryType is { } linkedQueryType && !getQueryTypes.Contains(linkedQueryType))
                    {
                        errors.Add($"Query '{queryType.Name}' links to '{linkedQueryType.Name}', but it isn't mapped as a GET query endpoint.");
                    }
                }

                if (!isList)
                {
                    if (itemLinks.Count > 0)
                    {
                        errors.Add($"Query '{queryType.Name}' declares item links, but doesn't return a list.");
                    }

                    if (ResourceConventions.GetResource(queryType)?.AutoItemLinks == false)
                    {
                        errors.Add($"Query '{queryType.Name}' sets AutoItemLinks, but doesn't return a list.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidResourceLinkException(errors);
            }

            return endpoints;
        }
    }
}
