using System.ComponentModel.DataAnnotations;

using SecuPass.Domain.Identity;

namespace SecuPass.Api.Features.Identity.CreateUser;

public sealed record CreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    // A20: exact names only. Client Viewer users need a client, which F02 adds.
    [Required]
    [AllowedValues(StaffRoles.Admin, StaffRoles.Staff, ErrorMessage = "Role must be Admin or Staff. Client Viewer users need a client.")]
    public string Role { get; init; } = string.Empty;
}