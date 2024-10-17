using Telegram.Bot.Types;

namespace OrgBot;

public class UserState
{
    public long Id { get; set; }

    public ulong ThrottleTime { get; init; }

    public ChatPermissions DefaultPermissions { get; init; } = new();
}