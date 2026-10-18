using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.Api.Features.Identity.Login;

public sealed partial class LoginHandler(SignInManager<StaffUser> signInManager, ILogger<LoginHandler> logger)
{
    private const string LoginRejectedTitle = "Invalid email or password";

    public async Task<Results<EmptyHttpResult, ProblemHttpResult>> HandleAsync(LoginRequest request)
    {
        // The user name is always the email (see StaffUser).
        var user = await signInManager.UserManager.FindByNameAsync(request.Email);
        if (user is null)
        {
            return Rejected(LoginRejection.UnknownEmail, userId: null);
        }

        // D9: an inactive user can't log in, even with the right password.
        if (!user.IsActive)
        {
            return Rejected(LoginRejection.Inactive, user.Id);
        }

        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        // A16: failed attempts count towards lockout.
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Rejected(RejectionFor(result), user.Id);
        }

        LogLoginSucceeded(logger, user.Id);
        // The bearer token handler has already written the token response.
        return TypedResults.Empty;
    }

    // A16: every rejection gets the same response, so it reveals neither whether the account exists nor its lockout.
    private ProblemHttpResult Rejected(LoginRejection rejection, Guid? userId)
    {
        LogLoginRejected(logger, rejection, userId);
        return TypedResults.Problem(title: LoginRejectedTitle, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static LoginRejection RejectionFor(SignInResult result) => result switch
    {
        { IsLockedOut: true } => LoginRejection.LockedOut,
        { IsNotAllowed: true } => LoginRejection.NotAllowed,
        _ => LoginRejection.WrongPassword,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Staff user {UserId} logged in")]
    private static partial void LogLoginSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login rejected: {Rejection} for user {UserId}")]
    private static partial void LogLoginRejected(ILogger logger, LoginRejection rejection, Guid? userId);

    private enum LoginRejection
    {
        UnknownEmail,
        Inactive,
        WrongPassword,
        LockedOut,
        NotAllowed,
    }
}