namespace SchoolManager.Models;

public class InstitutionalCredentialCard
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string CardNumber { get; set; } = null!;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public string Status { get; set; } = "active";

    public bool IsPrinted { get; set; }

    public DateTime? PrintedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
