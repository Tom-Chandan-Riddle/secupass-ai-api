using System.ComponentModel.DataAnnotations;

using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;

namespace SecuPass.Api.Features.Identity.CreateUser;

public sealed record CreateUserRequest
{
    // A27: no whitespace anywhere, so a padded address can't become a second account.
    [Required]
    [EmailAddress]
    [MaxLength(StaffUser.EmailMaxLength)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Email can't contain spaces.")]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    // A20: exact names only. Client Viewer users need a client, which F02 adds.
    [Required]
    [AllowedValues(StaffRoles.Admin, StaffRoles.Staff, ErrorMessage = "Role must be Admin or Staff. Client Viewer users need a client.")]
    public string Role { get; init; } = string.Empty;
}