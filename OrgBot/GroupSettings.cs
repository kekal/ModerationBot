namespace OrgBot;

public class GroupSettings
{
    private bool _banUsers;
    private TimeSpan _spamTimeWindow = TimeSpan.FromSeconds(10);
    private TimeSpan? _restrictionDuration = TimeSpan.FromDays(1);
    private bool _silentMode;
    private bool _useMute = true;

    public bool BanUsers
    {
        get => _banUsers;
        set
        {
            _banUsers = value;
        }
    }

    public TimeSpan SpamTimeWindow
    {
        get => _spamTimeWindow;
        set
        {
            _spamTimeWindow = value;
        }
    }

    public TimeSpan? RestrictionDuration
    {
        get => _restrictionDuration;
        set
        {
            _restrictionDuration = value;
        }
    }

    public bool SilentMode
    {
        get => _silentMode;
        set
        {
            _silentMode = value;
        }
    }

    public bool UseMute
    {
        get => _useMute;
        set
        {
            _useMute = value;
        }
    }
}