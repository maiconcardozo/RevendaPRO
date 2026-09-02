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
            if (!_settings.CreateBucketsOnStartup)
            {
                return;
            }

            using var client = CreateClient();

            await EnsureBucketAsync(client, _settings.PublicBucket, cancellationToken)
                .ConfigureAwait(false);

            await EnsureBucketAsync(client, _settings.PrivateBucket, cancellationToken)
                .ConfigureAwait(false);
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
