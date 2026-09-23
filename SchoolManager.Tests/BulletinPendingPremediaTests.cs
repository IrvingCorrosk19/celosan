using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class BulletinPendingPremediaTests
{
    [Fact]
    public void Media_student_refuerzo_premedia_is_pending()
    {
        Assert.True(BulletinPendingPremedia.IsPending(11, 9, true, EnrollmentTypeConstants.Refuerzo, false));
        Assert.True(BulletinPendingPremedia.IsPending(10, 9, true, EnrollmentTypeConstants.Libre, true));
    }

    [Fact]
    public void Inactive_or_historical_premedia_is_not_pending()
    {
        Assert.False(BulletinPendingPremedia.IsPending(11, 9, false, EnrollmentTypeConstants.Refuerzo, false));
        Assert.False(BulletinPendingPremedia.IsPending(11, 9, true, EnrollmentTypeConstants.Nocturno, true));
    }

    [Fact]
    public void Premedia_student_does_not_get_pending_section()
    {
        Assert.False(BulletinPendingPremedia.IsPending(9, 8, true, EnrollmentTypeConstants.Refuerzo, false));
    }

    [Fact]
    public void Media_carryover_of_media_grade_is_not_premedia_pending()
    {
        Assert.False(BulletinPendingPremedia.IsPending(11, 10, true, EnrollmentTypeConstants.Refuerzo, false));
    }
}
