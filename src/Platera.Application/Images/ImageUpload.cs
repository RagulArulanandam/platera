using Platera.Application.Common;

namespace Platera.Application.Images;

public interface IImageStorage
{
    /// <summary>Stores the image and returns a public relative URL (e.g. /uploads/abc.jpg).</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}

public record ImageUploadResult(string Url);

public interface IImageUploadService
{
    Task<ImageUploadResult> UploadAsync(Stream content, long length, CancellationToken ct = default);
}

public class ImageUploadService : IImageUploadService
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private readonly IImageStorage _storage;
    public ImageUploadService(IImageStorage storage) { _storage = storage; }

    public async Task<ImageUploadResult> UploadAsync(Stream content, long length, CancellationToken ct = default)
    {
        if (length <= 0) throw new BadRequestException("The file is empty.");
        if (length > MaxBytes) throw new BadRequestException("Images must be 5 MB or smaller.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBytes) throw new BadRequestException("Images must be 5 MB or smaller.");

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var ext = Detect(bytes) ?? throw new BadRequestException("Only JPEG, PNG, GIF or WebP images are allowed.");

        buffer.Position = 0;
        return new ImageUploadResult(await _storage.SaveAsync(buffer, ext, ct));
    }

    /// <summary>Detects the type from magic bytes, never from the client-supplied name or content type.</summary>
    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
        if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38) return ".gif";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return ".webp";
        return null;
    }
}
