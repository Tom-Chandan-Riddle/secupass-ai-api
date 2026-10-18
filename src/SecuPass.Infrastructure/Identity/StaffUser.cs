using Microsoft.AspNetCore.Identity;

namespace SecuPass.Infrastructure.Identity;

// The user name is always the email, so login can look users up by email.
public sealed class StaffUser : IdentityUser<Guid>
{
    // D9: an inactive user can't log in, and their tokens stop counting on the next request (A23).
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}