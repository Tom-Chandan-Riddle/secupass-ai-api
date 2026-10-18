using System.Collections.Concurrent;

using Serilog.Core;
using Serilog.Events;

namespace SecuPass.IntegrationTests.Support;

public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public IReadOnlyList<LogEvent> AllEvents => [.. _events];

    public IReadOnlyList<LogEvent> EventsWithCorrelationId(string correlationId) =>
        [.. _events.Where(logEvent => PropertyText(logEvent, "CorrelationId") == correlationId)];

    public static string? PropertyText(LogEvent logEvent, string propertyName) =>
        logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue { Value: var scalar }
            ? scalar?.ToString()
            : null;
}