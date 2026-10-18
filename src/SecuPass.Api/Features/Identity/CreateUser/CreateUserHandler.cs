using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Api.Features.Identity.CreateUser;

public sealed partial class CreateUserHandler(
    UserManager<StaffUser> userManager,
    SecuPassDbContext dbContext,
    ILogger<CreateUserHandler> logger)
{
    private const string DuplicateEmailTitle = "A user with this email already exists";

    public async Task<Results<Created<CreateUserResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        CreateUserRequest request, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var actorId = userManager.GetUserId(actor);

        // One transaction, so a failed role assignment can't leave a user without a role.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new StaffUser { UserName = request.Email, Email = request.Email };
        IdentityResult creation;
        try
        {
            creation = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolationOn(StaffUser.UserNameIndexName))
        {
            // Another create with the same email won the race between Identity's duplicate check and the insert.
            return DuplicateEmail(actorId);
        }

        if (!creation.Succeeded)
        {
            return RejectedByIdentity(creation, actorId);
        }

        ThrowIfFailed(await userManager.AddToRoleAsync(user, request.Role));
        await transaction.CommitAsync(cancellationToken);

        LogUserCreated(logger, user.Id, request.Role, actorId);
        // A21: no Location header, because no endpoint gets a user by ID yet.
        return TypedResults.Created((string?)null, new CreateUserResponse(user.Id, request.Email, request.Role));
    }

    private ProblemHttpResult DuplicateEmail(string? actorId)
    {
        LogCreateRejected(logger, CreateUserRejection.DuplicateEmail, actorId);
        return TypedResults.Problem(title: DuplicateEmailTitle, statusCode: StatusCodes.Status409Conflict);
    }

    // A16: Identity's own reasons, keyed by request property like the built-in validation errors.
    private Results<Created<CreateUserResponse>, ValidationProblem, ProblemHttpResult> RejectedByIdentity(
        IdentityResult result, string? actorId)
    {
        if (result.Errors.Any(error => IsDuplicate(error.Code)))
        {
            return DuplicateEmail(actorId);
        }

        LogCreateRejected(logger, CreateUserRejection.IdentityRules, actorId);
        var fieldErrors = result.Errors
            .GroupBy(error => FieldFor(error.Code), error => error.Description)
            .ToDictionary(field => field.Key, field => field.ToArray());
        return TypedResults.ValidationProblem(fieldErrors);
    }

    // The user name is the email, so Identity's user validator reports a taken email (in any case) as DuplicateUserName.
    private static bool IsDuplicate(string errorCode) => errorCode == nameof(IdentityErrorDescriber.DuplicateUserName);

    private static string FieldFor(string errorCode) => errorCode switch
    {
        _ when errorCode.StartsWith("Password", StringComparison.Ordinal) => nameof(CreateUserRequest.Password),
        _ => throw new InvalidOperationException($"Unexpected Identity error {errorCode} while creating a user."),
    };

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var reasons = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"The new user's role could not be assigned: {reasons}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Staff user {UserId} created with role {Role} by {ActorId}")]
    private static partial void LogUserCreated(ILogger logger, Guid userId, string role, string? actorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User creation rejected: {Rejection}, requested by {ActorId}")]
    private static partial void LogCreateRejected(ILogger logger, CreateUserRejection rejection, string? actorId);

    private enum CreateUserRejection
    {
        DuplicateEmail,
        IdentityRules,
    }
}