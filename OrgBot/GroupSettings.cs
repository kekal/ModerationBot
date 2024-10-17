using System.Collections.Concurrent;

namespace OrgBot;

public class GroupSettings
{
    public bool CleanNonGroupUrl { get; set; }
    
    public bool BanUsers { get; set; }

    public TimeSpan SpamTimeWindow { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan? RestrictionDuration { get; init; } = TimeSpan.FromDays(1);

    public bool DisableJoining { get; init; } = false;

    public bool SilentMode { get; set; }

    public bool UseMute { get; set; } = true;

    public ConcurrentDictionary<long, UserState> ThrottledUsers { get; init; } = [];
}