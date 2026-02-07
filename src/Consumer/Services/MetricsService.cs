using Prometheus;

namespace Consumer.Services;

public class MetricsService
{
    private static readonly Counter MessagesProcessed = Metrics.CreateCounter(
        "telemetry_consumer_messages_processed_total",
        "Total number of messages processed",
        new CounterConfiguration { LabelNames = new[] { "queue", "status" } });

    private static readonly Counter ErrorsTotal = Metrics.CreateCounter(
        "telemetry_consumer_errors_total",
        "Total number of errors",
        new CounterConfiguration { LabelNames = new[] { "error_type" } });

    private static readonly Histogram DbWriteDuration = Metrics.CreateHistogram(
        "telemetry_consumer_db_write_duration_seconds",
        "Duration of database writes in seconds",
        new HistogramConfiguration { LabelNames = new[] { "table" } });

    private static readonly Gauge QueueDepth = Metrics.CreateGauge(
        "telemetry_consumer_queue_depth",
        "Current queue depth",
        new GaugeConfiguration { LabelNames = new[] { "queue" } });

    public void RecordMessageProcessed(string queue, bool success)
    {
        MessagesProcessed.WithLabels(queue, success ? "success" : "failure").Inc();
    }

    public void RecordError(string errorType)
    {
        ErrorsTotal.WithLabels(errorType).Inc();
    }

    public IDisposable MeasureDbWrite(string table)
    {
        return DbWriteDuration.WithLabels(table).NewTimer();
    }

    public void SetQueueDepth(string queue, double depth)
    {
        QueueDepth.WithLabels(queue).Set(depth);
    }
}
