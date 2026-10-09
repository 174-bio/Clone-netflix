using Microsoft.Extensions.Logging;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace NetflixClone.Services;

public sealed class VideoUploadException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record VideoStorageUsage(long UsedBytes, long MaxBytes);

public sealed class VideoUploadService : IDisposable
{
    private const int BufferSize = 64 * 1024;
    private const long DefaultMaxStorageBytes = 9_000_000_000;
    private readonly string _uploadDirectory;
    private readonly long _maxFileSize;
    private readonly long _maxStorageBytes;
    private readonly ILogger<VideoUploadService> _logger;
    private readonly string _provider;
    private readonly string? _bucketName;
    private readonly string? _publicBaseUrl;
    private readonly AmazonS3Client? _s3Client;

    public VideoUploadService(IConfiguration configuration, ILogger<VideoUploadService> logger)
    {
        _logger = logger;
        _maxFileSize = configuration.GetValue<long?>("Uploads:MaxFileSizeBytes") ?? 1_073_741_824;
        if (_maxFileSize <= 0)
        {
            throw new InvalidOperationException("Uploads:MaxFileSizeBytes deve ser maior que zero.");
        }
        _maxStorageBytes = configuration.GetValue<long?>("Uploads:MaxStorageBytes") ?? DefaultMaxStorageBytes;
        if (_maxStorageBytes <= 0)
        {
            throw new InvalidOperationException("Uploads:MaxStorageBytes deve ser maior que zero.");
        }

        _provider = configuration["Uploads:Provider"] ?? "local";
        if (_provider is not "local" and not "s3")
        {
            throw new InvalidOperationException($"Provedor de uploads não suportado: {_provider}.");
        }

        if (_provider == "s3")
        {
            var endpoint = RequiredSetting(configuration, "Uploads:S3Endpoint");
            var accessKeyId = RequiredSetting(configuration, "Uploads:S3AccessKeyId");
            var secretAccessKey = RequiredSetting(configuration, "Uploads:S3SecretAccessKey");
            _bucketName = RequiredSetting(configuration, "Uploads:S3Bucket");
            _publicBaseUrl = RequiredSetting(configuration, "Uploads:PublicBaseUrl").TrimEnd('/');

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
                || endpointUri.Scheme != Uri.UriSchemeHttps
                || !Uri.TryCreate(_publicBaseUrl, UriKind.Absolute, out var publicUri)
                || publicUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "Uploads:S3Endpoint e Uploads:PublicBaseUrl precisam ser URLs HTTPS válidas.");
            }

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint.TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = configuration["Uploads:S3Region"] ?? "auto"
            };
            _s3Client = new AmazonS3Client(
                new BasicAWSCredentials(accessKeyId, secretAccessKey),
                s3Config);
            _uploadDirectory = Path.Combine(Path.GetTempPath(), "cinestream-upload-buffer");
        }
        else
        {
            var directory = configuration["Uploads:Directory"] ?? "Data/uploads";
            _uploadDirectory = ResolveDirectory(directory);
        }

        Directory.CreateDirectory(_uploadDirectory);
    }

    public long MaxFileSize => _maxFileSize;
    public long MaxStorageBytes => _maxStorageBytes;
    public string UploadDirectory => _uploadDirectory;

    public async Task<VideoStorageUsage> GetStorageUsageAsync(CancellationToken cancellationToken = default)
    {
        long storedBytes = 0;
        if (_s3Client == null)
        {
            foreach (var path in Directory.EnumerateFiles(_uploadDirectory))
            {
                if (IsValidFileName(Path.GetFileName(path)))
                {
                    storedBytes = checked(storedBytes + new FileInfo(path).Length);
                }
            }

            return new VideoStorageUsage(storedBytes, _maxStorageBytes);
        }

        string? continuationToken = null;
        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                ContinuationToken = continuationToken
            }, cancellationToken);

            foreach (var item in response.S3Objects)
            {
                if (item.Size is not long size || size < 0)
                {
                    throw new InvalidDataException("O armazenamento remoto retornou um tamanho de objeto inválido.");
                }

                storedBytes = checked(storedBytes + size);
            }

            continuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                    ?? throw new InvalidDataException("A listagem de objetos retornou uma página incompleta.")
                : null;
        } while (continuationToken != null);

        return new VideoStorageUsage(storedBytes, _maxStorageBytes);
    }

    private static string ResolveDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = "Data/uploads";
        }

        if (Path.IsPathRooted(directory))
        {
            return directory;
        }

        var basePath = AppContext.BaseDirectory;
        var candidate = Path.Combine(basePath, directory);
        var projectRoot = ResolveProjectRoot(basePath);

        if (!string.IsNullOrEmpty(projectRoot))
        {
            candidate = Path.Combine(projectRoot, directory);
        }

        return candidate;
    }

    private static string? ResolveProjectRoot(string basePath)
    {
        var current = new DirectoryInfo(basePath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NetflixClone.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        current = new DirectoryInfo(basePath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "appsettings.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    public async Task<string> SaveAsync(
        Stream source,
        string? originalFileName,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(Path.GetFileName(originalFileName ?? string.Empty)).ToLowerInvariant();
        if (extension is not ".mp4" and not ".webm")
        {
            throw new VideoUploadException("Envie um vídeo no formato MP4 ou WebM.", StatusCodes.Status400BadRequest);
        }

        if (contentLength > _maxFileSize)
        {
            throw new VideoUploadException("O vídeo excede o limite de tamanho configurado.", StatusCodes.Status413PayloadTooLarge);
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var finalPath = Path.Combine(_uploadDirectory, fileName);
        var temporaryPath = Path.Combine(_uploadDirectory, $"{Guid.NewGuid():N}.uploading");
        var header = new byte[12];
        var headerLength = 0;
        long totalBytes = 0;

        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > _maxFileSize)
                    {
                        throw new VideoUploadException("O vídeo excede o limite de tamanho configurado.", StatusCodes.Status413PayloadTooLarge);
                    }

                    var headerBytes = Math.Min(header.Length - headerLength, bytesRead);
                    if (headerBytes > 0)
                    {
                        buffer.AsSpan(0, headerBytes).CopyTo(header.AsSpan(headerLength));
                        headerLength += headerBytes;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }
            }

            if (totalBytes == 0 || !HasValidSignature(extension, header, headerLength))
            {
                throw new VideoUploadException("O arquivo não corresponde a um vídeo MP4 ou WebM válido.", StatusCodes.Status400BadRequest);
            }

            await EnsureStorageCapacityAsync(totalBytes, cancellationToken);
            if (_s3Client == null)
            {
                File.Move(temporaryPath, finalPath);
            }
            else
            {
                await using var uploadStream = new FileStream(
                    temporaryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await _s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = uploadStream,
                    ContentType = extension == ".webm" ? "video/webm" : "video/mp4"
                }, cancellationToken);
            }

            _logger.LogInformation("Vídeo enviado: {FileName}, {SizeBytes} bytes.", fileName, totalBytes);
            return _publicBaseUrl == null
                ? $"/uploads/{fileName}"
                : $"{_publicBaseUrl}/{Uri.EscapeDataString(fileName)}";
        }
        catch (Exception ex) when (ex is not VideoUploadException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao gravar um vídeo enviado.");
            throw;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public bool IsManagedVideoUrl(string? videoUrl)
    {
        if (!TryGetManagedFileName(videoUrl, out var fileName))
        {
            return false;
        }

        return _provider == "s3"
            || (TryGetManagedPath(fileName, out var path) && File.Exists(path));
    }

    public async Task DeleteManagedVideoAsync(string? videoUrl, CancellationToken cancellationToken = default)
    {
        if (!TryGetManagedFileName(videoUrl, out var fileName))
        {
            return;
        }

        if (_s3Client != null)
        {
            await _s3Client.DeleteObjectAsync(_bucketName, fileName, cancellationToken);
        }
        else if (TryGetManagedPath(fileName, out var path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string? GetManagedVideoPath(string fileName)
    {
        return _provider == "local" && TryGetManagedPath(fileName, out var path) && File.Exists(path)
            ? path
            : null;
    }

    public void Dispose() => _s3Client?.Dispose();

    private bool TryGetManagedFileName(string? videoUrl, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            return false;
        }

        if (videoUrl.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            fileName = videoUrl["/uploads/".Length..];
        }
        else if (_publicBaseUrl != null
            && Uri.TryCreate(videoUrl, UriKind.Absolute, out var videoUri)
            && Uri.TryCreate(_publicBaseUrl, UriKind.Absolute, out var baseUri)
            && string.Equals(
                videoUri.GetLeftPart(UriPartial.Authority),
                baseUri.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
        {
            var basePath = baseUri.AbsolutePath.TrimEnd('/');
            if (!videoUri.AbsolutePath.StartsWith(basePath + "/", StringComparison.Ordinal))
            {
                return false;
            }

            fileName = Uri.UnescapeDataString(videoUri.AbsolutePath[(basePath.Length + 1)..]);
        }

        return IsValidFileName(fileName);
    }

    private bool TryGetManagedPath(string fileName, out string path)
    {
        path = string.Empty;
        if (_provider != "local" || !IsValidFileName(fileName))
        {
            return false;
        }

        path = Path.Combine(_uploadDirectory, fileName);
        return true;
    }

    private static bool IsValidFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (Path.GetFileName(fileName) != fileName
            || (extension is not ".mp4" and not ".webm")
            || !Guid.TryParseExact(stem, "N", out _))
        {
            return false;
        }

        return true;
    }

    private static string RequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"A configuração obrigatória '{key}' não foi definida.");
        }

        return value;
    }

    private async Task EnsureStorageCapacityAsync(long incomingBytes, CancellationToken cancellationToken)
    {
        var usage = await GetStorageUsageAsync(cancellationToken);
        if (incomingBytes > usage.MaxBytes - usage.UsedBytes)
        {
            throw new VideoUploadException(
                "O armazenamento de vídeos atingiu o limite configurado. Remova vídeos antigos ou aumente a franquia.",
                StatusCodes.Status507InsufficientStorage);
        }
    }

    private static bool HasValidSignature(string extension, byte[] header, int length)
    {
        if (extension == ".mp4")
        {
            return length >= 8
                && header[4] == (byte)'f'
                && header[5] == (byte)'t'
                && header[6] == (byte)'y'
                && header[7] == (byte)'p';
        }

        return extension == ".webm"
            && length >= 4
            && header[0] == 0x1A
            && header[1] == 0x45
            && header[2] == 0xDF
            && header[3] == 0xA3;
    }
}
