using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Themisquo.AspNetCore;

public sealed class ThemisquoExceptionHandlerOptions
{
    private readonly Dictionary<Type, Func<Exception, ProblemDetails>> mappings = [];

    public ThemisquoExceptionHandlerOptions()
    {
        Map<ValidationException>(StatusCodes.Status400BadRequest, ex => new HttpValidationProblemDetails(
            ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        {
            Title = "One or more validation errors occurred.",
        });
    }

    public ThemisquoExceptionHandlerOptions Map<TException>(int statusCode, Func<TException, ProblemDetails>? problemDetailsFactory = null)
        where TException : Exception
    {
        problemDetailsFactory ??= ex => new ProblemDetails { Title = ex.Message };
        mappings[typeof(TException)] = exception =>
        {
            var problemDetails = problemDetailsFactory((TException)exception);
            problemDetails.Status ??= statusCode;
            return problemDetails;
        };
        return this;
    }

    public bool TryResolve(Exception exception, out ProblemDetails problemDetails)
    {
        if (mappings.TryGetValue(exception.GetType(), out var factory))
        {
            problemDetails = factory(exception);
            return true;
        }
        problemDetails = null!;
        return false;
    }
}
