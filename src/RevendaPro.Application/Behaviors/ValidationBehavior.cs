using FluentValidation;
using MediatR;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Behaviors
{
    /// <summary>
    /// Runs the FluentValidation validators before any handler, so no use case repeats the
    /// validation call.
    /// </summary>
    /// <typeparam name="TRequest">Request being handled.</typeparam>
    /// <typeparam name="TResponse">Response produced by the handler.</typeparam>
    public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        /// <inheritdoc/>
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(next);

            if (!validators.Any())
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }

            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken)))
                .ConfigureAwait(false);

            var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

            if (failures.Count != 0)
            {
                throw new InputValidationException(failures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray()));
            }

            return await next(cancellationToken).ConfigureAwait(false);
        }
    }
}
