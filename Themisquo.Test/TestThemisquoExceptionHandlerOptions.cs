using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Themisquo.AspNetCore;

namespace Themisquo.Test
{
    [TestClass]
    public class TestThemisquoExceptionHandlerOptions
    {
        [TestMethod]
        public void TryResolve_FluentValidationValidationException_MapsTo400WithFieldErrors()
        {
            // Given
            var options = new ThemisquoExceptionHandlerOptions();
            var exception = new ValidationException(
            [
                new ValidationFailure("Name", "Project name is required."),
                new ValidationFailure("Name", "Project name must not exceed 100 characters."),
                new ValidationFailure("Players", "Number of players must be at least 1."),
            ]);

            // When
            var resolved = options.TryResolve(exception, out var problemDetails);

            // Then
            Assert.IsTrue(resolved);
            Assert.AreEqual(StatusCodes.Status400BadRequest, problemDetails.Status);
            var validationProblemDetails = (HttpValidationProblemDetails)problemDetails;
            CollectionAssert.AreEqual(
                new[] { "Project name is required.", "Project name must not exceed 100 characters." },
                validationProblemDetails.Errors["Name"]);
            CollectionAssert.AreEqual(
                new[] { "Number of players must be at least 1." },
                validationProblemDetails.Errors["Players"]);
        }

        [TestMethod]
        public void Map_CustomException_UsesRegisteredStatusCodeAndFactory()
        {
            // Given
            var options = new ThemisquoExceptionHandlerOptions();
            options.Map<NotFoundExceptionStub>(StatusCodes.Status404NotFound, ex => new ProblemDetails { Title = ex.Message });

            // When
            var resolved = options.TryResolve(new NotFoundExceptionStub("missing"), out var problemDetails);

            // Then
            Assert.IsTrue(resolved);
            Assert.AreEqual(StatusCodes.Status404NotFound, problemDetails.Status);
            Assert.AreEqual("missing", problemDetails.Title);
        }

        [TestMethod]
        public void TryResolve_UnmappedException_ReturnsFalse()
        {
            // Given
            var options = new ThemisquoExceptionHandlerOptions();

            // When
            var resolved = options.TryResolve(new InvalidOperationException(), out _);

            // Then
            Assert.IsFalse(resolved);
        }

        private sealed class NotFoundExceptionStub(string message) : Exception(message);
    }
}
