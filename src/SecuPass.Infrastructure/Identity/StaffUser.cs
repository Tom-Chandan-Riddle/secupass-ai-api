using Microsoft.AspNetCore.Identity;

namespace SecuPass.Infrastructure.Identity;

// The user name is always the email, so login can look users up by email.
public sealed class StaffUser : IdentityUser<Guid>;