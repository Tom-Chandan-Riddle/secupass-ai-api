using System.ComponentModel.DataAnnotations;

namespace SecuPass.Api.Features.Identity.DevelopmentAdmin;

// A17: appsettings.Development.json turns the seed on. Without the section, as in Production, it is off.
public sealed class DevelopmentAdminOptions : IValidatableObject
{
    public const string SectionName = "DevelopmentAdmin";

    public bool Enabled { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Email) || !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("A valid email is required when the Development Admin seed is enabled.", [nameof(Email)]);
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("A password is required when the Development Admin seed is enabled.", [nameof(Password)]);
        }
    }
}