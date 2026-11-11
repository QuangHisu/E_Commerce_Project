namespace E_Commerce_Project.Services;

public sealed class ImageStorage(IWebHostEnvironment env)
{
    public async Task<string> Save(IFormFile? file, string? existing)
    {
        if (file == null)
            return existing ?? throw new InvalidOperationException("A product picture is required.");
        if (file.Length < 8 || file.Length > 2 * 1024 * 1024)
            throw new InvalidOperationException("Use a PNG or JPEG up to 2 MB.");
        using var stream = file.OpenReadStream();
        var header = new byte[8];
        await stream.ReadExactlyAsync(header);
        var png = header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpg = header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!png && !jpg)
            throw new InvalidOperationException("Only PNG and JPEG images are accepted.");
        var name = Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg");
        var dir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        await using var target = File.Create(Path.Combine(dir, name));
        await target.WriteAsync(header);
        await stream.CopyToAsync(target);
        return "/uploads/" + name;
    }
}
