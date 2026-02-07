using Prometheus;

namespace Gateway.Services;

public class MetricsService
{
    private static readonly Counter PollsTotal = Metrics.CreateCounter(
        "telemetry_gateway_polls_total",
        "Total number of sensor polls",
        new CounterConfiguration { LabelNames = new[] { "sensor_type", "status" } });

    private static readonly Counter ErrorsTotal = Metrics.CreateCounter(
        "telemetry_gateway_errors_total",
        "Total number of errors",
        new CounterConfiguration { LabelNames = new[] { "error_type" } });

    private static readonly Gauge QueueDepth = Metrics.CreateGauge(
        "telemetry_gateway_queue_depth",
        "Current queue depth",
        new GaugeConfiguration { LabelNames = new[] { "queue" } });

    private static readonly Histogram PollDuration = Metrics.CreateHistogram(
        "telemetry_gateway_poll_duration_seconds",
        "Duration of sensor polls in seconds",
        new HistogramConfiguration { LabelNames = new[] { "sensor_type" } });

    public void RecordPoll(string sensorType, bool success)
    {
        PollsTotal.WithLabels(sensorType, success ? "success" : "failure").Inc();
    }

    public void RecordError(string errorType)
    {
        ErrorsTotal.WithLabels(errorType).Inc();
    }

    public void SetQueueDepth(string queue, double depth)
    {
        QueueDepth.WithLabels(queue).Set(depth);
    }

    public IDisposable MeasurePollDuration(string sensorType)
    {
        return PollDuration.WithLabels(sensorType).NewTimer();
    }
}
