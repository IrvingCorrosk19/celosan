using System;

namespace SchoolManager.Dtos;

public class OfficialTrimesterGradeResult
{
    public decimal? Score { get; set; }

    public bool IsImported { get; set; }

    public string Origin { get; set; } = OfficialTrimesterGradeOrigin.None;

    public Guid? ImportedGradeId { get; set; }
}

public static class OfficialTrimesterGradeOrigin
{
    public const string Imported = "Imported";
    public const string Activities = "Activities";
    public const string None = "None";
}
