namespace OrgBot;

public class BotSettings
{
    public bool BanUsers { get; set; }
    public TimeSpan SpamTimeWindow { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan? RestrictionDuration { get; set; } = TimeSpan.FromDays(1); // null for permanent ban/mute
    public bool SilentMode { get; set; }
    public bool UseMute { get; set; } = true;
}