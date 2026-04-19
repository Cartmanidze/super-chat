using Prometheus;

namespace SuperChat.Infrastructure.Diagnostics;

public static class SuperChatMetrics
{
    public static readonly Counter NormalizedMessagesStoredTotal = Metrics.CreateCounter(
        "superchat_normalized_messages_stored_total",
        "Total number of normalized messages stored for downstream processing.",
        new CounterConfiguration
        {
            LabelNames = ["source"]
        });

    public static readonly Counter NormalizedMessagesDuplicateTotal = Metrics.CreateCounter(
        "superchat_normalized_messages_duplicate_total",
        "Total number of duplicate normalized messages skipped when receiving.",
        new CounterConfiguration
        {
            LabelNames = ["source"]
        });

    public static readonly Counter NormalizedMessagesByPathTotal = Metrics.CreateCounter(
        "superchat_normalized_messages_by_path_total",
        "Normalized messages split by ingestion path and result (stored vs duplicate).",
        new CounterConfiguration
        {
            LabelNames = ["path", "result"]
        });

    public static readonly Counter PipelineDispatchTotal = Metrics.CreateCounter(
        "superchat_pipeline_dispatch_total",
        "Total number of pipeline commands dispatched after a message is received.",
        new CounterConfiguration
        {
            LabelNames = ["scheduler", "command"]
        });

    public static readonly Counter PipelineDispatchSkippedTotal = Metrics.CreateCounter(
        "superchat_pipeline_dispatch_skipped_total",
        "Total number of pipeline commands skipped or dropped.",
        new CounterConfiguration
        {
            LabelNames = ["scheduler", "reason"]
        });

    public static readonly Counter PipelineCommandsTotal = Metrics.CreateCounter(
        "superchat_pipeline_commands_total",
        "Total number of pipeline commands handled.",
        new CounterConfiguration
        {
            LabelNames = ["command", "result"]
        });

    public static readonly Gauge PipelineCommandsInProgress = Metrics.CreateGauge(
        "superchat_pipeline_commands_in_progress",
        "Current number of pipeline commands in progress.",
        new GaugeConfiguration
        {
            LabelNames = ["command"]
        });

    public static readonly Histogram PipelineCommandDurationSeconds = Metrics.CreateHistogram(
        "superchat_pipeline_command_duration_seconds",
        "Duration of pipeline command handling.",
        new HistogramConfiguration
        {
            LabelNames = ["command", "result"],
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 10)
        });

    public static void Initialize()
    {
        _ = NormalizedMessagesStoredTotal;
        _ = NormalizedMessagesDuplicateTotal;
        _ = NormalizedMessagesByPathTotal;
        _ = PipelineDispatchTotal;
        _ = PipelineDispatchSkippedTotal;
        _ = PipelineCommandsTotal;
        _ = PipelineCommandsInProgress;
        _ = PipelineCommandDurationSeconds;
    }
}
