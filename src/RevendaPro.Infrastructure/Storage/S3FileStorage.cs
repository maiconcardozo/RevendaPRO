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
        private readonly IAmazonS3 _signer;
        private readonly Protocol _signedUrlProtocol;

        /// <summary>Creates the storage from the configured endpoint and credentials.</summary>
        /// <param name="settings">Endpoint, buckets and credentials.</param>
        public S3FileStorage(IOptions<StorageSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _settings = settings.Value;
            _client = CreateClient(_settings, _settings.ServiceUrl);

            // A second client that never sends a request: it exists only to sign addresses,
            // and it signs them against the endpoint the browser will actually call.
            //
            // Signature version 4 puts the host inside the signature — the query carries
            // X-Amz-SignedHeaders=host — so an address signed for one endpoint and then
            // rewritten to another is refused with SignatureDoesNotMatch. Signing twice, once
            // per endpoint, is what keeps a container name working for the API and a reachable
            // address working for the browser.
            var signingEndpoint = NeedsASeparateSigner(_settings)
                ? _settings.PublicUrl
                : _settings.ServiceUrl;

            _signer = NeedsASeparateSigner(_settings)
                ? CreateClient(_settings, _settings.PublicUrl)
                : _client;

            // A signed address is https unless it is asked otherwise, whatever the endpoint
            // says: the SDK reads this from the request and never from the configuration. Left
            // alone against MinIO it hands out an https:// address for a server that speaks
            // only http, and the browser fails on the TLS handshake with a "wrong version
            // number" that points at nothing. Only local development lands on http; R2 and S3
            // are https.
            _signedUrlProtocol =
                signingEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    ? Protocol.HTTP
                    : Protocol.HTTPS;
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
                Protocol = _signedUrlProtocol,
                Expires = DateTime.UtcNow.Add(expiresIn ?? _settings.PrivateUrlLifetime)
            };

            return new Uri(_signer.GetPreSignedURL(request));
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
        public void Dispose()
        {
            if (!ReferenceEquals(_signer, _client))
            {
                _signer.Dispose();
            }

            _client.Dispose();
        }

        private string BucketFor(FileVisibility visibility) =>
            visibility == FileVisibility.Public ? _settings.PublicBucket : _settings.PrivateBucket;

        /// <summary>
        /// Whether the browser reaches the store at a different address than the API does.
        ///
        /// True in the compose stack, where the API talks to <c>minio:9000</c> — a name that
        /// resolves only inside the Docker network — and the browser talks to
        /// <c>localhost:9100</c>. False against Cloudflare R2 or AWS S3 with a single endpoint,
        /// where one client signs everything.
        /// </summary>
        /// <param name="settings">Endpoint configuration.</param>
        /// <returns>True when a second signing client is needed.</returns>
        private static bool NeedsASeparateSigner(StorageSettings settings) =>
            !string.IsNullOrWhiteSpace(settings.ServiceUrl) &&
            !string.IsNullOrWhiteSpace(settings.PublicUrl) &&
            !string.Equals(
                settings.ServiceUrl.TrimEnd('/'),
                settings.PublicUrl.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);

        private static AmazonS3Client CreateClient(StorageSettings settings, string serviceUrl)
        {
            var config = new AmazonS3Config
            {
                // Bucket in the path instead of the host name. Required by MinIO, and by R2,
                // whose endpoint is per account rather than per bucket.
                ForcePathStyle = settings.ForcePathStyle
            };

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }
            else
            {
                config.ServiceURL = serviceUrl;
                config.AuthenticationRegion = settings.Region;
            }

            return new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
        }
    }
}
