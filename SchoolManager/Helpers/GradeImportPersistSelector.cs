using SchoolManager.Dtos;

namespace SchoolManager.Helpers;

public static class GradeImportPersistSelector
{
    public static bool IsPersistableStatus(string? status) =>
        status is GradeImportStatus.Nuevo or GradeImportStatus.Actualizar or GradeImportStatus.SinCambios;

    public static (List<T> Persistable, List<T> Omitted) Partition<T>(
        IReadOnlyList<T> operations,
        Func<T, bool> canPersist)
    {
        var persistable = new List<T>();
        var omitted = new List<T>();
        foreach (var op in operations)
        {
            if (canPersist(op))
                persistable.Add(op);
            else
                omitted.Add(op);
        }

        return (persistable, omitted);
    }
}
