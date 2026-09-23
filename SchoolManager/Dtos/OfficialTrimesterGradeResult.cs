using System;
using SchoolManager.Helpers;

namespace SchoolManager.Dtos;

public class OfficialTrimesterGradeResult
{
    public decimal? Score { get; set; }

    public string Status { get; set; } = ImportedTrimesterGradeStatus.Graded;

    public bool IsImported { get; set; }

    public string Origin { get; set; } = OfficialTrimesterGradeOrigin.None;

    public Guid? ImportedGradeId { get; set; }

    public string Display => OfficialGradeMark.Display(Score, Status, IsImported);
}

public static class OfficialTrimesterGradeOrigin
{
    public const string Imported = "Imported";
    public const string Activities = "Activities";
    public const string None = "None";
}
