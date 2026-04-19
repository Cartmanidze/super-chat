namespace SuperChat.Contracts.Features.Messaging;

public interface IChatSourceAdapter
{
    ChatSourceKind Source { get; }
}
