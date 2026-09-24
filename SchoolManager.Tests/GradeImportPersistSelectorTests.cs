using SchoolManager.Dtos;
using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class GradeImportPersistSelectorTests
{
    [Fact]
    public void Partition_persists_valid_rows_and_omits_errors()
    {
        var ops = new[]
        {
            "NUEVO",
            "ACTUALIZAR",
            "SIN CAMBIOS",
            "ERROR",
            "ERROR"
        };

        var split = GradeImportPersistSelector.Partition(ops, GradeImportPersistSelector.IsPersistableStatus);

        Assert.Equal(3, split.Persistable.Count);
        Assert.Equal(2, split.Omitted.Count);
        Assert.All(split.Omitted, s => Assert.Equal(GradeImportStatus.Error, s));
    }

    [Fact]
    public void Partition_does_not_guess_or_rewrite_error_rows()
    {
        var error = new GradeImportOperationDto
        {
            Status = GradeImportStatus.Error,
            ErrorCode = GradeImportErrorCodes.SsaNotFound,
            Message = "Matrícula válida encontrada, pero el estudiante no está inscrito en CIENCIAS NATURALES.",
            GradeName = "7",
            GroupName = "7-A"
        };

        var split = GradeImportPersistSelector.Partition(
            new[] { error },
            op => GradeImportPersistSelector.IsPersistableStatus(op.Status));

        Assert.Empty(split.Persistable);
        Assert.Same(error, split.Omitted[0]);
        Assert.Equal(GradeImportErrorCodes.SsaNotFound, split.Omitted[0].ErrorCode);
    }

    [Fact]
    public void Valid_and_error_mix_can_confirm_partial()
    {
        var statuses = Enumerable.Repeat(GradeImportStatus.Nuevo, 8)
            .Concat(Enumerable.Repeat(GradeImportStatus.Error, 4))
            .ToList();

        var split = GradeImportPersistSelector.Partition(statuses, GradeImportPersistSelector.IsPersistableStatus);

        Assert.Equal(8, split.Persistable.Count);
        Assert.Equal(4, split.Omitted.Count);
    }
}
