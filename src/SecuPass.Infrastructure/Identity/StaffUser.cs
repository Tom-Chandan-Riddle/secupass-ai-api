using Microsoft.AspNetCore.Identity;

namespace SecuPass.Infrastructure.Identity;

// The user name is always the email, so login can look users up by email.
public sealed class StaffUser : IdentityUser<Guid>
{
    // Identity's column length for the user name and email.
    public const int EmailMaxLength = 256;

    // Identity's unique index on the normalised user name, which is the email.
    public const string UserNameIndexName = "UserNameIndex";

    // D9: an inactive user can't log in, and their tokens stop counting on the next request (A23).
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}