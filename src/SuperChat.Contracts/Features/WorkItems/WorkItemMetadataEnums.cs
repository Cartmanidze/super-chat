using System.Text.Json.Serialization;

namespace SuperChat.Contracts.Features.WorkItems;

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemType>))]
public enum WorkItemType
{
    Request,
    Event,
    ActionItem
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemStatus>))]
public enum WorkItemStatus
{
    AwaitingResponse,
    Answered,
    Missed,
    PendingConfirmation,
    Confirmed,
    Rescheduled,
    Cancelled,
    Completed,
    ToDo,
    InProgress,
    Done,
    NotRelevant
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemPriority>))]
public enum WorkItemPriority
{
    Normal,
    Important
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemOwner>))]
public enum WorkItemOwner
{
    Me,
    Contact,
    Both
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemOrigin>))]
public enum WorkItemOrigin
{
    Promise,
    Request,
    DetectedFromChat
}

[JsonConverter(typeof(JsonStringEnumConverter<AiReviewState>))]
public enum AiReviewState
{
    NeedsReview,
    Confirmed,
    Rejected
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemSource>))]
public enum WorkItemSource
{
    Telegram,
    Chat,
    Contact
}

[JsonConverter(typeof(JsonStringEnumConverter<MeetingJoinProvider>))]
public enum MeetingJoinProvider
{
    GoogleMeet,
    Zoom,
    MicrosoftTeams,
    Webex,
    JitsiMeet,
    Whereby,
    YandexTelemost,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter<RequestStatus>))]
public enum RequestStatus
{
    AwaitingResponse,
    Answered,
    Missed
}

[JsonConverter(typeof(JsonStringEnumConverter<EventStatus>))]
public enum EventStatus
{
    PendingConfirmation,
    Confirmed,
    Rescheduled,
    Cancelled,
    Completed
}

[JsonConverter(typeof(JsonStringEnumConverter<ActionItemStatus>))]
public enum ActionItemStatus
{
    ToDo,
    InProgress,
    Done,
    NotRelevant
}

public static class WorkItemStatusConversions
{
    public static WorkItemStatus ToWorkItemStatus(this RequestStatus status)
    {
        return status switch
        {
            RequestStatus.AwaitingResponse => WorkItemStatus.AwaitingResponse,
            RequestStatus.Answered => WorkItemStatus.Answered,
            RequestStatus.Missed => WorkItemStatus.Missed,
            _ => WorkItemStatus.AwaitingResponse
        };
    }

    public static WorkItemStatus ToWorkItemStatus(this EventStatus status)
    {
        return status switch
        {
            EventStatus.PendingConfirmation => WorkItemStatus.PendingConfirmation,
            EventStatus.Confirmed => WorkItemStatus.Confirmed,
            EventStatus.Rescheduled => WorkItemStatus.Rescheduled,
            EventStatus.Cancelled => WorkItemStatus.Cancelled,
            EventStatus.Completed => WorkItemStatus.Completed,
            _ => WorkItemStatus.PendingConfirmation
        };
    }

    public static WorkItemStatus ToWorkItemStatus(this ActionItemStatus status)
    {
        return status switch
        {
            ActionItemStatus.ToDo => WorkItemStatus.ToDo,
            ActionItemStatus.InProgress => WorkItemStatus.InProgress,
            ActionItemStatus.Done => WorkItemStatus.Done,
            ActionItemStatus.NotRelevant => WorkItemStatus.NotRelevant,
            _ => WorkItemStatus.ToDo
        };
    }

    public static RequestStatus? ToRequestStatus(this WorkItemStatus? status)
    {
        return status switch
        {
            WorkItemStatus.AwaitingResponse => RequestStatus.AwaitingResponse,
            WorkItemStatus.Answered => RequestStatus.Answered,
            WorkItemStatus.Missed => RequestStatus.Missed,
            _ => null
        };
    }

    public static EventStatus? ToEventStatus(this WorkItemStatus? status)
    {
        return status switch
        {
            WorkItemStatus.PendingConfirmation => EventStatus.PendingConfirmation,
            WorkItemStatus.Confirmed => EventStatus.Confirmed,
            WorkItemStatus.Rescheduled => EventStatus.Rescheduled,
            WorkItemStatus.Cancelled => EventStatus.Cancelled,
            WorkItemStatus.Completed => EventStatus.Completed,
            _ => null
        };
    }

    public static ActionItemStatus? ToActionItemStatus(this WorkItemStatus? status)
    {
        return status switch
        {
            WorkItemStatus.ToDo => ActionItemStatus.ToDo,
            WorkItemStatus.InProgress => ActionItemStatus.InProgress,
            WorkItemStatus.Done => ActionItemStatus.Done,
            WorkItemStatus.NotRelevant => ActionItemStatus.NotRelevant,
            _ => null
        };
    }
}
