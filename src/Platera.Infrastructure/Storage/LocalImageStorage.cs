using Microsoft.Extensions.Options;
using Platera.Application.Images;

namespace Platera.Infrastructure.Storage;

public class StorageOptions
{
    public const string Section = "Storage";
    /// <summary>Absolute folder served at /uploads. The API host fills this from its web root.</summary>
    public string UploadsPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads");
}

/// <summary>Stores images on local disk. Swap for a blob-storage IImageStorage without touching callers.</summary>
public class LocalImageStorage : IImageStorage
{
    private readonly string _root;
    public LocalImageStorage(IOptions<StorageOptions> options)
    {
        _root = options.Value.UploadsPath;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var name = $"{Guid.NewGuid():N}{extension}";
        await using var file = File.Create(Path.Combine(_root, name));
        await content.CopyToAsync(file, ct);
        return $"/uploads/{name}";
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        // Only files we created; never seed assets or arbitrary paths.
        if (url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            var name = Path.GetFileName(url);
            var path = Path.Combine(_root, name);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }
}
