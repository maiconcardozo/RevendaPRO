namespace RevendaPro.Shared.Exceptions
{
    /// <summary>A business rule was not met. The API translates it to HTTP 422.</summary>
    public class BusinessRuleException(string message) : Exception(message);

    /// <summary>The resource does not exist. The API translates it to HTTP 404.</summary>
    public class NotFoundException(string message) : Exception(message);

    /// <summary>The credentials are invalid. The API translates it to HTTP 401.</summary>
    public class UnauthenticatedException(string message) : Exception(message);

    /// <summary>
    /// The uploaded file is larger than allowed. The API translates it to HTTP 413.
    ///
    /// Apart from the business rules, because it says nothing about the vehicle and everything
    /// about the request: the same answer has to come out whether the file was stopped at the
    /// transport or by the controller, and 413 is the code that says so.
    /// </summary>
    public class PayloadTooLargeException(string message) : Exception(message);
}

namespace RevendaPro.Shared.Exceptions
{
    /// <summary>Input failed validation. The API translates it to HTTP 400 with the field errors.</summary>
    public class InputValidationException(IReadOnlyDictionary<string, string[]> errors)
        : Exception("Os dados informados são inválidos.")
    {
        /// <summary>Error messages grouped by field.</summary>
        public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
    }
}
