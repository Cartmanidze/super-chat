namespace SuperChat.Contracts.Features.Operations;

public sealed class PipelineMessagingOptions
{
    public const string SectionName = "PipelineMessaging";

    public bool Enabled { get; set; } = true;

    public string InputQueueName { get; set; } = "superchat-pipeline";

    public string TransportTableName { get; set; } = "rebus_messages";

    public int Workers { get; set; } = 1;

    public int MaxParallelism { get; set; } = 1;
}
