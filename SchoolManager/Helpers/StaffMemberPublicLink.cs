using SchoolManager.Services.Security;

namespace SchoolManager.Helpers;

/// <summary>
/// Enlaces firmados para el QR de perfil público del personal institucional.
/// </summary>
public static class StaffMemberPublicLink
{
    /// <summary>URL absoluta a la página pública del miembro (?t=token firmado).</summary>
    public static string? BuildPublicUrl(string? siteBaseUrl, string staffQrTokenRaw, IQrSignatureService signatureService)
    {
        if (string.IsNullOrWhiteSpace(siteBaseUrl) || string.IsNullOrWhiteSpace(staffQrTokenRaw))
            return null;

        var signed = signatureService.GenerateSignedToken(staffQrTokenRaw.Trim());
        var q = Uri.EscapeDataString(signed);
        return $"{siteBaseUrl.TrimEnd('/')}/InstitutionalCredential/member?t={q}";
    }

    /// <summary>Ruta pública alternativa con token de BD en la URL (sin firma HMAC en query).</summary>
    public static string? BuildMemberPathUrl(string? siteBaseUrl, string staffQrTokenRaw)
    {
        if (string.IsNullOrWhiteSpace(siteBaseUrl) || string.IsNullOrWhiteSpace(staffQrTokenRaw))
            return null;

        return $"{siteBaseUrl.TrimEnd('/')}/InstitutionalCredential/member/{Uri.EscapeDataString(staffQrTokenRaw.Trim())}";
    }

    public static bool TryResolveRawTokenFromSignedQuery(
        string? signedQueryToken,
        IQrSignatureService signatureService,
        out string? rawToken)
    {
        rawToken = null;
        if (string.IsNullOrWhiteSpace(signedQueryToken))
            return false;
        if (!signatureService.ValidateSignedToken(signedQueryToken))
            return false;
        rawToken = signatureService.ExtractTokenFromSigned(signedQueryToken);
        return !string.IsNullOrWhiteSpace(rawToken);
    }
}
