namespace Rooby.Api.Validation;

/// <summary>SPEC §3.2, §11.5: PublishUri is either an https webhook against a configured host
/// allow-list (SSRF guard) or a file:// path confined to a configured export root.</summary>
public static class PublishUriValidator
{
    public static bool IsValid(string? publishUri, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(publishUri))
        {
            return true;
        }

        if (!Uri.TryCreate(publishUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            var allowedHosts = configuration.GetSection("Publish:AllowedHosts").Get<string[]>() ?? [];
            return allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        }

        if (uri.Scheme == "file")
        {
            var exportRoot = configuration["Publish:ExportRoot"];
            if (string.IsNullOrEmpty(exportRoot))
            {
                return false;
            }

            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exportRoot));
            var fullPath = Path.GetFullPath(uri.LocalPath);
            return fullPath == normalizedRoot || fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        return false;
    }
}
