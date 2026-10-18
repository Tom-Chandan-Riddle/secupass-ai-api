using Microsoft.EntityFrameworkCore;

namespace SecuPass.Infrastructure.Persistence;

public sealed class SecuPassDbContext(DbContextOptions<SecuPassDbContext> options) : DbContext(options);