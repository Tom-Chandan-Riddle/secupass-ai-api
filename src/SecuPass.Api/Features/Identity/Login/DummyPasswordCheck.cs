using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.Api.Features.Identity.Login;

// A28: does the work of a real password check when there is no real check to do, so rejecting an unknown,
// inactive or locked-out account takes as long as rejecting a wrong password.
public sealed class DummyPasswordCheck
{
    private static readonly StaffUser NoUser = new();

    private readonly PasswordHasher<StaffUser> _passwordHasher;
    private readonly string _dummyPasswordHash;

    // Built from the app's hasher options, so the dummy hash costs as many iterations as real ones.
    public DummyPasswordCheck(IOptions<PasswordHasherOptions> passwordHasherOptions)
    {
        _passwordHasher = new PasswordHasher<StaffUser>(passwordHasherOptions);
        _dummyPasswordHash = _passwordHasher.HashPassword(NoUser, Guid.NewGuid().ToString());
    }

    public void VerifyAgainstDummyHash(string password) =>
        _ = _passwordHasher.VerifyHashedPassword(NoUser, _dummyPasswordHash, password);
}