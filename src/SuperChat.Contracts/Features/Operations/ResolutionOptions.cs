namespace SuperChat.Contracts.Features.Operations;

public sealed class ResolutionOptions
{
    public const string SectionName = "Resolution";

    public bool Enabled { get; set; } = true;

    public bool UseLlm { get; set; } = true;

    public double MinConfidence { get; set; } = 0.7d;

    public int MaxMessagesPerCandidate { get; set; } = 8;

    public int MaxCandidatesPerRequest { get; set; } = 8;

    public int MaxOutputTokens { get; set; } = 700;

    public int AutoResolutionCooldownMinutes { get; set; } = 5;

    public bool ScheduleDeferredConversationPass { get; set; } = true;

    public int DeferredConversationDelayMinutes { get; set; } = 15;

    public int MeetingGracePeriodMinutes { get; set; } = 30;

    public bool EnableDueMeetingsSweep { get; set; } = true;

    public int DueMeetingsSweepMinutes { get; set; } = 10;
}
