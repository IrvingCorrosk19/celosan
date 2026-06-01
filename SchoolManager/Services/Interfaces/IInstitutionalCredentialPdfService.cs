namespace SchoolManager.Services.Interfaces;

public interface IInstitutionalCredentialPdfService
{
    Task<byte[]> GenerateCardPdfAsync(Guid staffUserId, Guid createdBy);
}
