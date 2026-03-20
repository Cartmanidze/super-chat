namespace SuperChat.Contracts.Features.Operations;

public sealed class MessageIngestionFilterOptions
{
    public const string SectionName = "MessageIngestionFilter";

    public bool Enabled { get; set; } = true;

    public string[] AllowedMessageTypes { get; set; } = ["m.text", "m.notice"];

    public string[] InviteLinkFragments { get; set; } =
    [
        "t.me/+",
        "t.me/joinchat",
        "telegram.me/joinchat",
        "joinchat/"
    ];

    public int MaxAllowedUrls { get; set; } = 2;

    public int MinTextCharactersWhenLinksPresent { get; set; } = 5;
}
