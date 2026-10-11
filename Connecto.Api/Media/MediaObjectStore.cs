using Amazon.S3;
using Amazon.S3.Model;

namespace Connecto.Api.Media;

public interface IMediaObjectStore
{
    bool IsAvailable { get; }
    Task<(string Url, Dictionary<string, string> Fields)> UploadGrantAsync(string key, string contentType, DateTime expiresAt);
    Task<(Stream Body, long Length)> ReadAsync(string key, CancellationToken ct);
    Task WriteAsync(string key, Stream body, string contentType, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed class S3MediaObjectStore(IConfiguration config) : IMediaObjectStore, IDisposable
{
    private readonly Lazy<IAmazonS3> client = new(() => new AmazonS3Client());
    private IAmazonS3 S3 => client.Value;
    public void Dispose() { if (client.IsValueCreated) client.Value.Dispose(); }
    public bool IsAvailable => !string.IsNullOrWhiteSpace(config["Media:Bucket"]);
    private string Bucket => config["Media:Bucket"] ?? throw new InvalidOperationException("Media:Bucket is required");

    public async Task<(string Url, Dictionary<string, string> Fields)> UploadGrantAsync(string key, string contentType, DateTime expiresAt)
    {
        var response = await S3.CreatePresignedPostAsync(new CreatePresignedPostRequest
        {
            BucketName = Bucket, Key = key, Expires = expiresAt,
            Fields = new Dictionary<string, string> { ["Content-Type"] = contentType },
            Conditions = [S3PostCondition.ExactMatch("key", key),
                S3PostCondition.ExactMatch("Content-Type", contentType),
                S3PostCondition.ContentLengthRange(1, 5 * 1024 * 1024)]
        });
        return (response.Url, response.Fields);
    }

    public async Task<(Stream Body, long Length)> ReadAsync(string key, CancellationToken ct)
    {
        var result = await S3.GetObjectAsync(Bucket, key, ct);
        return (new OwnedResponseStream(result), result.ContentLength);
    }

    public async Task WriteAsync(string key, Stream body, string contentType, CancellationToken ct)
    {
        await S3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket, Key = key, InputStream = body, ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct) => S3.DeleteObjectAsync(Bucket, key, ct);

    private sealed class OwnedResponseStream(GetObjectResponse response) : Stream
    {
        private readonly Stream source = response.ResponseStream;
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => source.Position = value; }
        public override void Flush() => source.Flush();
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => source.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) response.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { response.Dispose(); await base.DisposeAsync(); }
    }
}
