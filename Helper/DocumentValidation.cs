namespace CarCareTracker.Helper;

public static class DocumentValidation
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public static string Validate(string fileName, string contentType, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new ArgumentException("Document must have a name and contain at most 10 MiB");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var valid = extension switch
        {
            ".pdf" => contentType == "application/pdf" && bytes.AsSpan().StartsWith("%PDF-"u8),
            ".jpg" or ".jpeg" => contentType == "image/jpeg" && bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            ".png" => contentType == "image/png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            _ => false
        };
        if (!valid) throw new ArgumentException("Document extension, contentType and signature must match PDF, JPG or PNG");
        return extension;
    }
}
