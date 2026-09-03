using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Infrastructure.Storage
{
    /// <summary>
    /// Creates the buckets when they are missing, and only where that is wanted.
    ///
    /// In local development the MinIO container is born empty, and asking a person to click
    /// through a console before the first upload is a step that gets forgotten. In production
    /// the buckets are provisioned once, with their own access policy, and the application has
    /// no business creating them — hence <see cref="StorageSettings.CreateBucketsOnStartup"/>,
    /// which is off by default.
    ///
    /// Idempotent: starting twice creates nothing a second time.
    /// </summary>
    public class StorageInitializer(
        IOptions<StorageSettings> settings,
        ILogger<StorageInitializer> logger)
    {
        private readonly StorageSettings _settings = settings.Value;

        /// <summary>Runs the provisioning.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.CreateBucketsOnStartup && !_settings.KeepFileVersions)
            {
                return;
            }

            using var client = CreateClient();

            if (_settings.CreateBucketsOnStartup)
            {
                await EnsureBucketAsync(client, _settings.PublicBucket, cancellationToken)
                    .ConfigureAwait(false);

                await EnsureBucketAsync(client, _settings.PrivateBucket, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_settings.KeepFileVersions)
            {
                await KeepVersionsAsync(client, _settings.PrivateBucket, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Turns versioning on for the private bucket, so that deleting an object leaves its
        /// previous version behind instead of nothing (RNF-11). Durability protects against a
        /// disk failing; it protects against nothing when a wrong DELETE runs on every replica
        /// at once. Versions are what make that DELETE reversible.
        ///
        /// Idempotent: enabling twice is a no-op. Refused by a token without the permission,
        /// in which case the startup warns and carries on.
        /// </summary>
        private async Task KeepVersionsAsync(
            IAmazonS3 client,
            string bucket,
            CancellationToken cancellationToken)
        {
            try
            {
                var current = await client.GetBucketVersioningAsync(
                    new GetBucketVersioningRequest { BucketName = bucket }, cancellationToken)
                    .ConfigureAwait(false);

                if (current.VersioningConfig?.Status == VersionStatus.Enabled)
                {
                    return;
                }

                await client.PutBucketVersioningAsync(
                    new PutBucketVersioningRequest
                    {
                        BucketName = bucket,
                        VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled }
                    },
                    cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Versioning enabled on bucket \"{Bucket}\".", bucket);
            }
            catch (AmazonS3Exception exception)
            {
                logger.LogWarning(exception,
                    "Could not enable versioning on bucket \"{Bucket}\". A deleted file will be unrecoverable until it is enabled by hand.",
                    bucket);
            }
        }

        private async Task EnsureBucketAsync(
            IAmazonS3 client,
            string bucket,
            CancellationToken cancellationToken)
        {
            var existing = await client.ListBucketsAsync(cancellationToken).ConfigureAwait(false);

            if (existing.Buckets?.Any(b =>
                    string.Equals(b.BucketName, bucket, StringComparison.Ordinal)) == true)
            {
                return;
            }

            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = bucket }, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Bucket \"{Bucket}\" created.", bucket);

            if (string.Equals(bucket, _settings.PublicBucket, StringComparison.Ordinal))
            {
                await AllowAnonymousReadAsync(client, bucket, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Lets anyone read the public bucket, which is what makes the word "public" true in
        /// development. In production the same effect comes from the CDN in front of the
        /// bucket, and the policy is set once when the bucket is provisioned.
        ///
        /// Read only: listing stays closed, so nobody can enumerate the gallery of a tenant by
        /// asking the store for everything it holds.
        /// </summary>
        private static Task AllowAnonymousReadAsync(
            IAmazonS3 client,
            string bucket,
            CancellationToken cancellationToken) =>
            client.PutBucketPolicyAsync(
                new PutBucketPolicyRequest
                {
                    BucketName = bucket,
                    Policy = $$"""
                    {
                      "Version": "2012-10-17",
                      "Statement": [
                        {
                          "Effect": "Allow",
                          "Principal": "*",
                          "Action": "s3:GetObject",
                          "Resource": "arn:aws:s3:::{{bucket}}/*"
                        }
                      ]
                    }
                    """
                },
                cancellationToken);

        /// <summary>
        /// Built here instead of reusing the storage service, because provisioning is a
        /// separate concern from reading and writing files, and the service exposes no bucket
        /// operations on purpose.
        /// </summary>
        private AmazonS3Client CreateClient()
        {
            var config = new AmazonS3Config
            {
                ForcePathStyle = _settings.ForcePathStyle,
                ServiceURL = _settings.ServiceUrl,
                AuthenticationRegion = _settings.Region
            };

            return new AmazonS3Client(
                new Amazon.Runtime.BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey),
                config);
        }
    }
}
