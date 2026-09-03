namespace RevendaPro.Shared.Settings
{
    /// <summary>
    /// Where files are stored. <b>The only place in the repository that knows which provider
    /// is in use</b> — and it knows it as values, never as code.
    ///
    /// MinIO, Cloudflare R2 and AWS S3 speak the same API. Moving between them is changing
    /// what is below, with nothing recompiled:
    ///
    /// <code>
    /// | Setting        | MinIO local            | Cloudflare R2                       | AWS S3          |
    /// |----------------|------------------------|-------------------------------------|-----------------|
    /// | ServiceUrl     | http://minio:9000      | https://&lt;id&gt;.r2.cloudflarestorage.com | empty           |
    /// | PublicUrl      | http://localhost:9100  | the CDN domain                      | the bucket host |
    /// | Region         | us-east-1              | auto                                | the real region |
    /// | ForcePathStyle | true                   | true                                | false           |
    /// </code>
    ///
    /// See ADR-0004.
    /// </summary>
    public class StorageSettings
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "Storage";

        /// <summary>
        /// Endpoint the API talks to. Empty lets the SDK resolve the real AWS endpoint from
        /// <see cref="Region"/>.
        /// </summary>
        public string ServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint the <b>browser</b> talks to, which is a different address.
        ///
        /// Kept apart from <see cref="ServiceUrl"/> on purpose: with MinIO in a container the
        /// API reaches it at <c>minio:9000</c>, a name that resolves only inside the Docker
        /// network. An address built with it simply fails in the browser.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;

        /// <summary>Region. Cloudflare R2 expects <c>auto</c>.</summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Puts the bucket in the path instead of the host name. Required by MinIO and by R2;
        /// false against AWS S3, which prefers the bucket as a subdomain.
        /// </summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>Bucket for what is meant to be seen: vehicle photos.</summary>
        public string PublicBucket { get; set; } = "revendapro-public";

        /// <summary>Bucket for what carries personal data: documents.</summary>
        public string PrivateBucket { get; set; } = "revendapro-private";

        /// <summary>Access key. Comes from the environment, never from a file in the repository.</summary>
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>Secret key. Comes from the environment, never from a file in the repository.</summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Creates the buckets on startup when they are missing. True for local development,
        /// where the MinIO container is born empty; false in production, where the buckets are
        /// provisioned once and the application has no business creating them.
        /// </summary>
        public bool CreateBucketsOnStartup { get; set; }

        /// <summary>
        /// Keeps every version of every file in the private bucket, so a deleted object is
        /// recovered from its previous version (RNF-11).
        ///
        /// Turned on at startup, idempotently, wherever the credentials allow it: MinIO and
        /// Cloudflare R2 both speak the S3 versioning call. Where they refuse — a token without
        /// that permission — the startup logs a warning and goes on, because a store that
        /// works without versions beats one that refuses to start.
        /// </summary>
        public bool KeepFileVersions { get; set; } = true;

        /// <summary>How long a signed address for a private file stays valid.</summary>
        public TimeSpan PrivateUrlLifetime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Largest accepted upload, in bytes.
        ///
        /// A setting, and not a constant in the code, because RNF-09 asks for it: the photo of
        /// a new phone weighs far more than one from an old phone, and that number moves with
        /// time while nothing else does.
        /// </summary>
        public long MaxUploadSizeInBytes { get; set; } = 12 * 1024 * 1024;
    }
}
