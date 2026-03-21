using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Shared.Presentation;

public static class ResolutionTracePresentation
{
    public static string? ToResolutionNote(this WorkItemRecord item)
    {
        if (string.IsNullOrWhiteSpace(item.ResolutionSource))
        {
            return null;
        }

        return item.ResolutionSource switch
        {
            WorkItemResolutionState.AutoAiReply => BuildAiNote("AI закрыл как ответ получен", item.ResolutionTrace),
            WorkItemResolutionState.AutoAiCompletion => BuildAiNote("AI закрыл как выполненное", item.ResolutionTrace),
            WorkItemResolutionState.AutoAiMeetingCompletion => BuildAiNote("AI закрыл как завершенную встречу", item.ResolutionTrace),
            WorkItemResolutionState.AutoReply => "Авто закрыто после ответа",
            WorkItemResolutionState.AutoCompletion => "Авто закрыто по явному подтверждению",
            WorkItemResolutionState.AutoMeetingCompletion => "Авто закрыто по сигналу после встречи",
            _ => null
        };
    }

    private static string BuildAiNote(string label, ResolutionTrace? trace)
    {
        if (trace?.Confidence is null)
        {
            return label;
        }

        var percent = (int)Math.Round(Math.Clamp(trace.Confidence.Value, 0d, 1d) * 100d, MidpointRounding.AwayFromZero);
        return $"{label} · {percent}%";
    }
}
