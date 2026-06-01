using SchoolManager.Dtos;
using SchoolManager.Models;

namespace SchoolManager.Services.Interfaces;

public interface IInstitutionalCredentialImageService
{
    byte[] GenerateFrontPng(StaffCardRenderDto dto, SchoolIdCardSetting settings);

    byte[] GenerateBackPng(StaffCardRenderDto dto, SchoolIdCardSetting settings);

    (float WidthMm, float HeightMm) GetPortraitCardMmDimensions();
}
