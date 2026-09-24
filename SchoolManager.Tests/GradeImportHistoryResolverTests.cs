using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class GradeImportHistoryResolverTests
{
    private sealed record Enroll(string Id, bool Active, DateTime? End);

    private sealed record Subject(string Id, bool Active);

    [Fact]
    public void Prefers_single_active_enrollment()
    {
        var rows = new[]
        {
            new Enroll("old", false, new DateTime(2026, 9, 21)),
            new Enroll("current", true, null)
        };

        var result = GradeImportHistoryResolver.SelectEnrollment(rows, x => x.Active, x => x.End);

        Assert.Null(result.ErrorCode);
        Assert.Equal("current", result.Selected!.Id);
    }

    [Fact]
    public void Historical_picks_latest_end_date()
    {
        var rows = new[]
        {
            new Enroll("apr", false, new DateTime(2026, 4, 30)),
            new Enroll("sep", false, new DateTime(2026, 9, 21))
        };

        var result = GradeImportHistoryResolver.SelectEnrollment(rows, x => x.Active, x => x.End);

        Assert.Null(result.ErrorCode);
        Assert.Equal("sep", result.Selected!.Id);
    }

    [Fact]
    public void Same_end_date_is_ambiguous()
    {
        var day = new DateTime(2026, 9, 21);
        var rows = new[]
        {
            new Enroll("a", false, day),
            new Enroll("b", false, day)
        };

        var result = GradeImportHistoryResolver.SelectEnrollment(rows, x => x.Active, x => x.End);

        Assert.Null(result.Selected);
        Assert.Equal(GradeImportErrorCodes.EnrollmentAmbiguous, result.ErrorCode);
    }

    [Fact]
    public void Two_active_enrollments_are_ambiguous()
    {
        var rows = new[]
        {
            new Enroll("a", true, null),
            new Enroll("b", true, null)
        };

        var result = GradeImportHistoryResolver.SelectEnrollment(rows, x => x.Active, x => x.End);

        Assert.Equal(GradeImportErrorCodes.EnrollmentAmbiguous, result.ErrorCode);
    }

    [Fact]
    public void Empty_candidates_is_missing_enrollment()
    {
        var result = GradeImportHistoryResolver.SelectEnrollment(
            Array.Empty<Enroll>(), x => x.Active, x => x.End);

        Assert.Equal(GradeImportErrorCodes.EnrollmentNotFound, result.ErrorCode);
    }

    [Fact]
    public void Active_enrollment_requires_active_subject()
    {
        var hits = new[] { new Subject("hist", false) };

        var result = GradeImportHistoryResolver.SelectSubject(hits, enrollmentIsActive: true, x => x.Active);

        Assert.Equal(GradeImportErrorCodes.SsaInactive, result.ErrorCode);
    }

    [Fact]
    public void Historical_enrollment_allows_inactive_subject()
    {
        var hits = new[] { new Subject("hist", false) };

        var result = GradeImportHistoryResolver.SelectSubject(hits, enrollmentIsActive: false, x => x.Active);

        Assert.Null(result.ErrorCode);
        Assert.Equal("hist", result.Selected!.Id);
    }

    [Fact]
    public void Historical_enrollment_without_subject_is_error()
    {
        var result = GradeImportHistoryResolver.SelectSubject(
            Array.Empty<Subject>(), enrollmentIsActive: false, x => x.Active);

        Assert.Equal(GradeImportErrorCodes.SsaNotFound, result.ErrorCode);
    }
}
