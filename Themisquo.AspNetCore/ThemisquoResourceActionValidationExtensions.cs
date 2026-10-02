using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Checks the actions declared on the mapped query endpoints, so a declaration that can never produce an action is
    /// caught at startup instead of silently missing from responses.
    /// </summary>
    public static class ThemisquoResourceActionValidationExtensions
    {
        private static readonly string[] HttpMethodNames =
        [
            HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete,
            HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace, HttpMethods.Connect,
        ];

        /// <summary>
        /// Verifies, for each query mapped on <paramref name="endpoints"/>, that:
        /// <list type="bullet">
        /// <item>every declared command is mapped as a command endpoint, and declares no <see cref="DeclaredActionAttribute.Fields"/>;</item>
        /// <item>every <see cref="ResourceAttribute.Context"/> and <see cref="ResourceAttribute.ItemContext"/> matches a
        /// mapped command's <see cref="ActionAttribute.Context"/>;</item>
        /// <item><see cref="ItemActionAttribute"/>s, <see cref="ResourceAttribute.ItemContext"/> and
        /// <see cref="ResourceAttribute.AutoItemActions"/> are only on list queries;</item>
        /// <item>every external action's method is an HTTP method.</item>
        /// </list>
        /// Call it after mapping the endpoints.
        /// </summary>
        /// <exception cref="InvalidResourceActionException">Thrown when one or more declarations are invalid.</exception>
        public static IEndpointRouteBuilder ValidateResourceActions(this IEndpointRouteBuilder endpoints)
        {
            var mapped = endpoints.DataSources.SelectMany(dataSource => dataSource.Endpoints).ToList();
            var queryTypes = mapped
                .Select(endpoint => endpoint.Metadata.GetMetadata<QueryEndpointMetadata>())
                .OfType<QueryEndpointMetadata>()
                .Select(metadata => metadata.QueryType)
                .Distinct();
            var commandTypes = mapped
                .Select(endpoint => endpoint.Metadata.GetMetadata<CommandEndpointMetadata>())
                .OfType<CommandEndpointMetadata>()
                .Select(metadata => metadata.CommandType)
                .ToHashSet();
            var contexts = commandTypes
                .Select(commandType => ResourceConventions.GetAction(commandType)?.Context)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            var errors = new List<string>();
            foreach (var queryType in queryTypes)
            {
                var resultType = ResourceConventions.GetQueryResultType(queryType);
                var isList = resultType is not null && ResourceConventions.GetCollectionElementType(resultType) is not null;
                var resource = ResourceConventions.GetResource(queryType);
                var itemActions = ResourceConventions.GetDeclaredActions(queryType, forItems: true);

                foreach (var declaration in ResourceConventions.GetDeclaredActions(queryType, forItems: false).Concat(itemActions))
                {
                    if (declaration.CommandType is { } commandType)
                    {
                        if (!commandTypes.Contains(commandType))
                        {
                            errors.Add($"Query '{queryType.Name}' declares command '{commandType.Name}' as an action, but it isn't mapped as an endpoint.");
                        }

                        if (declaration.Fields is not null)
                        {
                            errors.Add($"Query '{queryType.Name}' sets Fields on its action for command '{commandType.Name}'; a command's fields come from its properties.");
                        }
                    }
                    else if (!HttpMethodNames.Contains(declaration.Method, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"Query '{queryType.Name}' declares action '{declaration.Name}' with '{declaration.Method}', which isn't an HTTP method.");
                    }
                }

                if (!isList)
                {
                    if (itemActions.Count > 0)
                    {
                        errors.Add($"Query '{queryType.Name}' declares item actions, but doesn't return a list.");
                    }

                    if (resource?.ItemContext is not null)
                    {
                        errors.Add($"Query '{queryType.Name}' sets ItemContext, but doesn't return a list.");
                    }

                    if (resource?.AutoItemActions == false)
                    {
                        errors.Add($"Query '{queryType.Name}' sets AutoItemActions, but doesn't return a list.");
                    }
                }

                foreach (var (property, context) in new[] { ("Context", resource?.Context), ("ItemContext", resource?.ItemContext) })
                {
                    if (context is not null && !contexts.Contains(context))
                    {
                        errors.Add($"Query '{queryType.Name}' sets {property} '{context}', but no mapped command has [Action(Context = \"{context}\")].");
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidResourceActionException(errors);
            }

            return endpoints;
        }
    }
}
