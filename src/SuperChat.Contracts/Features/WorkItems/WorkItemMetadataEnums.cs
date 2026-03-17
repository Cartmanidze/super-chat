using System.Text.Json.Serialization;

namespace SuperChat.Contracts.ViewModels;

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemType>))]
public enum WorkItemType
{
    Request,
    Event,
    Obligation
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

[JsonConverter(typeof(JsonStringEnumConverter<ObligationStatus>))]
public enum ObligationStatus
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

    public static WorkItemStatus ToWorkItemStatus(this ObligationStatus status)
    {
        return status switch
        {
            ObligationStatus.ToDo => WorkItemStatus.ToDo,
            ObligationStatus.InProgress => WorkItemStatus.InProgress,
            ObligationStatus.Done => WorkItemStatus.Done,
            ObligationStatus.NotRelevant => WorkItemStatus.NotRelevant,
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

    public static ObligationStatus? ToObligationStatus(this WorkItemStatus? status)
    {
        return status switch
        {
            WorkItemStatus.ToDo => ObligationStatus.ToDo,
            WorkItemStatus.InProgress => ObligationStatus.InProgress,
            WorkItemStatus.Done => ObligationStatus.Done,
            WorkItemStatus.NotRelevant => ObligationStatus.NotRelevant,
            _ => null
        };
    }
}
