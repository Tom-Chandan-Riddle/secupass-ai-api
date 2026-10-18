using System.ComponentModel.DataAnnotations;

namespace SecuPass.Api.Features.Identity.Login;

public sealed record LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}