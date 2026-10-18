using System.ComponentModel.DataAnnotations;

namespace SecuPass.Api.Features.Identity.Refresh;

public sealed record RefreshRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}