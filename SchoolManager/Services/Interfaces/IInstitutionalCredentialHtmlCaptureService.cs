namespace SchoolManager.Services.Interfaces;

public interface IInstitutionalCredentialHtmlCaptureService
{
    Task<byte[]> GenerateFromUrl(string url);
}
