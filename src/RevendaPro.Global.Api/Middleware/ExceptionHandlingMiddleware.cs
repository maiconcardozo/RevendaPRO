using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RevendaPro.Global.Shared.Exceptions;

namespace RevendaPro.Global.Api.Middleware
{
    /// <summary>
    /// Translates an exception into ProblemDetails (RFC 7807).
    ///
    /// No unexpected exception leaks an internal detail to the client: only the mapped ones
    /// carry their own message, which is in Portuguese because the frontend displays it.
    /// </summary>
    public class ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        private static readonly JsonSerializerOptions SerializerOptions =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>Runs the pipeline and converts anything that escapes it.</summary>
        /// <param name="context">Current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await WriteAsync(context, exception).ConfigureAwait(false);
            }
        }

        private async Task WriteAsync(HttpContext context, Exception exception)
        {
            var (status, title, detail) = Translate(exception);

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled failure in {Method} {Path}.",
                    context.Request.Method, context.Request.Path);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

            if (exception is InputValidationException validation)
            {
                problem.Extensions["errors"] = validation.Errors;
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";

            await context.Response
                .WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions))
                .ConfigureAwait(false);
        }

        private static (int Status, string Title, string Detail) Translate(Exception exception) =>
            exception switch
            {
                InputValidationException e =>
                    (StatusCodes.Status400BadRequest, "Dados inválidos", e.Message),

                UnauthenticatedException e =>
                    (StatusCodes.Status401Unauthorized, "Autenticação necessária", e.Message),

                NotFoundException e =>
                    (StatusCodes.Status404NotFound, "Registro ausente", e.Message),

                BusinessRuleException e =>
                    (StatusCodes.Status422UnprocessableEntity, "Regra de negócio", e.Message),

                _ => (StatusCodes.Status500InternalServerError, "Falha inesperada",
                    "Falha inesperada. Tente novamente.")
            };
    }
}
