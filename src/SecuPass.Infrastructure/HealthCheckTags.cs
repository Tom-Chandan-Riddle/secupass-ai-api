namespace SecuPass.Infrastructure;

public static class HealthCheckTags
{
    // Checks with this tag run on /health/ready, never on the /health liveness probe.
    public const string Readiness = "ready";
}