using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness only: no registered check runs here, so /health never touches a dependency.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

app.Run();