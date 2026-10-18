namespace SecuPass.Api.Features.Identity.CreateUser;

public sealed record CreateUserResponse(Guid UserId, string Email, string Role);