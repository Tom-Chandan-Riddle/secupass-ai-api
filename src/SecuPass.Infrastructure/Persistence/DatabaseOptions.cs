using System.ComponentModel.DataAnnotations;

namespace SecuPass.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}