using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using SecuPass.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStructuredLogging(builder.Configuration);
builder.Services.AddErrorHandling();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLogging();
app.UseErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Liveness only: no registered check runs here, so /health never touches a dependency.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

app.Run();