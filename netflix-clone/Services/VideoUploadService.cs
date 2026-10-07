using Microsoft.Extensions.Logging;

namespace NetflixClone.Services;

public sealed class VideoUploadException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class VideoUploadService
{
    private const int BufferSize = 64 * 1024;
    private readonly string _uploadDirectory;
    private readonly long _maxFileSize;
    private readonly ILogger<VideoUploadService> _logger;

    public VideoUploadService(IConfiguration configuration, ILogger<VideoUploadService> logger)
    {
        _logger = logger;
        _maxFileSize = configuration.GetValue<long?>("Uploads:MaxFileSizeBytes") ?? 1_073_741_824;
        if (_maxFileSize <= 0)
        {
            throw new InvalidOperationException("Uploads:MaxFileSizeBytes deve ser maior que zero.");
        }

        var directory = configuration["Uploads:Directory"] ?? "Data/uploads";
        _uploadDirectory = ResolveDirectory(directory);
        Directory.CreateDirectory(_uploadDirectory);
    }

    public long MaxFileSize => _maxFileSize;
    public string UploadDirectory => _uploadDirectory;

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

            File.Move(temporaryPath, finalPath);
            _logger.LogInformation("Vídeo enviado: {FileName}, {SizeBytes} bytes.", fileName, totalBytes);
            return $"/uploads/{fileName}";
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
        if (string.IsNullOrWhiteSpace(videoUrl) || !videoUrl.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return false;
        }

        var fileName = videoUrl["/uploads/".Length..];
        return TryGetManagedPath(fileName, out var path) && File.Exists(path);
    }

    public void DeleteManagedVideo(string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl) || !videoUrl.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return;
        }

        var fileName = videoUrl["/uploads/".Length..];
        if (TryGetManagedPath(fileName, out var path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string? GetManagedVideoPath(string fileName)
    {
        return TryGetManagedPath(fileName, out var path) && File.Exists(path) ? path : null;
    }

    private bool TryGetManagedPath(string fileName, out string path)
    {
        path = string.Empty;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (Path.GetFileName(fileName) != fileName
            || (extension is not ".mp4" and not ".webm")
            || !Guid.TryParseExact(stem, "N", out _))
        {
            return false;
        }

        path = Path.Combine(_uploadDirectory, fileName);
        return true;
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
