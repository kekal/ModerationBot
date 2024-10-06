namespace OrgBot;

public class GroupSettings
{
    public bool BanUsers { get; set; }

    public TimeSpan SpamTimeWindow { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan? RestrictionDuration { get; set; } = TimeSpan.FromDays(1);

    public bool SilentMode { get; set; }

    public bool UseMute { get; set; } = true;
}