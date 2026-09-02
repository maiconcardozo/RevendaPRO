using Microsoft.Extensions.Options;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Api.Middleware
{
    /// <summary>
    /// Refuses an upload that is too large by reading the header, before a single byte of the
    /// body is read.
    ///
    /// Without it the transport limit is what stops the request, and it stops it by resetting
    /// the connection: the person sees a network error, with no sentence explaining what to do
    /// and no size to aim for. Answering from <c>Content-Length</c> costs nothing — the number
    /// arrives in the first frame — and turns the same refusal into a message.
    ///
    /// The limit itself lives in <see cref="StorageSettings.MaxUploadSizeInBytes"/>, which
    /// RNF-09 asks to be configurable. The margin covers the multipart envelope that travels
    /// around the file: field names, boundaries and headers.
    /// </summary>
    public class UploadSizeMiddleware(RequestDelegate next, IOptions<StorageSettings> settings)
    {
        private const long Envelope = 1 * 1024 * 1024;

        /// <summary>Checks the announced size and runs the pipeline.</summary>
        /// <param name="context">Current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var limit = settings.Value.MaxUploadSizeInBytes;

            if (context.Request.ContentLength > limit + Envelope)
            {
                var megabytes = limit / (1024d * 1024d);

                throw new PayloadTooLargeException($"Envie um arquivo de até {megabytes:0.#} MB.");
            }

            return next(context);
        }
    }
}
