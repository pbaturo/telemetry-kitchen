using Prometheus;

namespace Gateway.Poller.Metrics;

public class MetricsCollector
{
    public Counter PollsTotal { get; } = Prometheus.Metrics.CreateCounter(
        "tk_polls_total", "Total number of station polls attempted");

    public Counter PollsFailed { get; } = Prometheus.Metrics.CreateCounter(
        "tk_polls_failed_total", "Total number of failed station polls");

    public Counter PollsFailedHttp5xx { get; } = Prometheus.Metrics.CreateCounter(
        "tk_polls_failed_http_5xx_total", "Total number of transient upstream 5xx responses");

    public Counter PollsFailedHttp429 { get; } = Prometheus.Metrics.CreateCounter(
        "tk_polls_failed_http_429_total", "Total number of upstream 429 responses");

    public Counter PollRetries { get; } = Prometheus.Metrics.CreateCounter(
        "tk_polls_retries_total", "Total number of HTTP retry attempts for transient failures");

    public Counter EventsPublished { get; } = Prometheus.Metrics.CreateCounter(
        "tk_events_published_total", "Total number of events published to RabbitMQ");

    public Histogram PollDuration { get; } = Prometheus.Metrics.CreateHistogram(
        "tk_poll_duration_ms", "Duration of HTTP polls in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(10, 2, 10) });

    public Histogram PublishDuration { get; } = Prometheus.Metrics.CreateHistogram(
        "tk_publish_duration_ms", "Duration of RabbitMQ publishes in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 10) });

    public Gauge LastSuccessTime { get; } = Prometheus.Metrics.CreateGauge(
        "tk_last_success_unixtime", "Unix timestamp of last successful poll",
        new GaugeConfiguration { LabelNames = new[] { "sensorId" } });
}
