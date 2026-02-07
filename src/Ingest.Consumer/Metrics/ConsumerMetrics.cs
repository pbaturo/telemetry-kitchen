using Prometheus;
using static Prometheus.Metrics;

namespace Ingest.Consumer.Metrics;

public class ConsumerMetrics
{
    public Counter EventsConsumed { get; } = CreateCounter(
        "tk_events_consumed_total", "Total number of events consumed from RabbitMQ");

    public Counter EventsProcessed { get; } = CreateCounter(
        "tk_events_processed_total", "Total number of events successfully processed");

    public Counter EventsFailed { get; } = CreateCounter(
        "tk_events_failed_total", "Total number of events that failed processing");

    public Counter DuplicateEvents { get; } = CreateCounter(
        "tk_duplicate_events_total", "Total number of duplicate events detected");

    public Histogram ProcessingDuration { get; } = CreateHistogram(
        "tk_event_processing_duration_ms", "Duration of event processing in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 10) });

    public Histogram DbWriteDuration { get; } = CreateHistogram(
        "tk_db_write_duration_ms", "Duration of database writes in milliseconds",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 10) });

    public Gauge ConsumerLag { get; } = CreateGauge(
        "tk_consumer_lag", "Approximate number of messages in queue waiting to be consumed");
}
