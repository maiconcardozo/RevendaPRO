using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Storage
{
    /// <summary>
    /// Stores files through the S3 API.
    ///
    /// <b>The only file in the repository that references the AWS SDK</b>, and an architecture
    /// test keeps it that way. Nothing above this class knows S3 exists.
    ///
    /// The name carries no provider on purpose. MinIO, Cloudflare R2 and AWS S3 answer the
    /// same calls; which one is behind this is a matter of <see cref="StorageSettings"/>. See
    /// ADR-0004.
    /// </summary>
    public sealed class S3FileStorage : IFileStorage, IDisposable
    {
        private readonly StorageSettings _settings;
        private readonly IAmazonS3 _client;

        /// <summary>Creates the storage from the configured endpoint and credentials.</summary>
        /// <param name="settings">Endpoint, buckets and credentials.</param>
        public S3FileStorage(IOptions<StorageSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _settings = settings.Value;
            _client = CreateClient(_settings);
        }

        /// <inheritdoc/>
        public async Task<StoredFile> SaveAsync(
            Stream content,
            StorageRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(request);

            // The length has to be known before the request goes out, and a stream coming from
            // a multipart form is often not seekable. Buffering also lets the size be reported
            // back without asking the store afterwards.
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            // Read before sending: the SDK closes the stream it was handed, so asking the
            // buffer for its length afterwards throws.
            var size = buffer.Length;

            var put = new PutObjectRequest
            {
                BucketName = BucketFor(request.Visibility),
                Key = request.Key,
                InputStream = buffer,
                ContentType = request.ContentType
            };

            await _client.PutObjectAsync(put, cancellationToken).ConfigureAwait(false);

            return new StoredFile(request.Key, request.ContentType, size);
        }

        /// <inheritdoc/>
        public Uri GetUrl(string key, FileVisibility visibility, TimeSpan? expiresIn = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (visibility == FileVisibility.Public)
            {
                // A public file needs no signature, and signing it would be worse than
                // pointless: the address would expire and the CDN would cache a link that
                // stops working.
                var host = _settings.PublicUrl.TrimEnd('/');

                return new Uri($"{host}/{_settings.PublicBucket}/{key}");
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.PrivateBucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiresIn ?? _settings.PrivateUrlLifetime)
            };

            return new Uri(WithPublicHost(_client.GetPreSignedURL(request)));
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(
            string key,
            FileVisibility visibility,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            // S3 answers success for a key that was already gone, which is what makes deleting
            // safe to retry.
            await _client.DeleteObjectAsync(
                BucketFor(visibility), key, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> DeleteByPrefixAsync(
            string prefix,
            FileVisibility visibility,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

            var bucket = BucketFor(visibility);
            var removed = 0;
            string? continuationToken = null;

            do
            {
                var listed = await _client.ListObjectsV2Async(
                    new ListObjectsV2Request
                    {
                        BucketName = bucket,
                        Prefix = prefix,
                        ContinuationToken = continuationToken
                    },
                    cancellationToken).ConfigureAwait(false);

                if (listed.S3Objects.Count > 0)
                {
                    await _client.DeleteObjectsAsync(
                        new DeleteObjectsRequest
                        {
                            BucketName = bucket,
                            Objects = [.. listed.S3Objects.Select(o => new KeyVersion { Key = o.Key })]
                        },
                        cancellationToken).ConfigureAwait(false);

                    removed += listed.S3Objects.Count;
                }

                // A listing answers at most a thousand keys at a time. A vehicle gallery fits
                // in one page; the loop is what keeps a whole tenant from being half deleted.
                continuationToken = listed.IsTruncated == true ? listed.NextContinuationToken : null;
            }
            while (continuationToken is not null);

            return removed;
        }

        /// <inheritdoc/>
        public void Dispose() => _client.Dispose();

        private string BucketFor(FileVisibility visibility) =>
            visibility == FileVisibility.Public ? _settings.PublicBucket : _settings.PrivateBucket;

        /// <summary>
        /// The signature is built over the endpoint the API uses, which the browser cannot
        /// resolve when that endpoint is a container name. Swapping only the host keeps the
        /// signature valid, because it covers the path and the query, and never the authority.
        /// </summary>
        private string WithPublicHost(string url)
        {
            if (string.IsNullOrWhiteSpace(_settings.ServiceUrl) ||
                string.IsNullOrWhiteSpace(_settings.PublicUrl))
            {
                return url;
            }

            var service = new Uri(_settings.ServiceUrl);
            var signed = new Uri(url);

            if (!string.Equals(signed.Authority, service.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var host = new Uri(_settings.PublicUrl);

            return new UriBuilder(signed)
            {
                Scheme = host.Scheme,
                Host = host.Host,
                Port = host.IsDefaultPort ? -1 : host.Port
            }.Uri.ToString();
        }

        private static AmazonS3Client CreateClient(StorageSettings settings)
        {
            var config = new AmazonS3Config
            {
                // Bucket in the path instead of the host name. Required by MinIO, and by R2,
                // whose endpoint is per account rather than per bucket.
                ForcePathStyle = settings.ForcePathStyle
            };

            if (string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }
            else
            {
                config.ServiceURL = settings.ServiceUrl;
                config.AuthenticationRegion = settings.Region;
            }

            return new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
        }
    }
}
