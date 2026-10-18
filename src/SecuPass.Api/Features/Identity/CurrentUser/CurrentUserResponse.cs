namespace SecuPass.Api.Features.Identity.CurrentUser;

public sealed record CurrentUserResponse(Guid UserId, string Email, string Role);