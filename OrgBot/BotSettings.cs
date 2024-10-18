using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Telegram.Bot.Types;
using File = System.IO.File;

[assembly: InternalsVisibleTo("OrgBot.BotSettingsTests")]

namespace OrgBot;

public class BotSettings
{
    private static readonly string SettingsFilePath;

    static BotSettings()
    {
        var settingsPath = Environment.GetEnvironmentVariable("SETTINGS_PATH");
        SettingsFilePath = !string.IsNullOrWhiteSpace(settingsPath) ? settingsPath : Path.Combine(".", "data", @"botsettings.json");
    }

    private bool _engaged = true;
    private int _logSize = 200;
    private static JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public ConcurrentDictionary<long, GroupSettings> GroupSettings { get; init; } = [];

    public bool Engaged
    {
        get => _engaged;
        set
        {
            _engaged = value;
            Save();
        }
    }

    public int LogSize
    {
        get => _logSize;
        set
        {
            _logSize = value;
            Save();
        }
    }

    public UserState GetUserState(long groupId, long? userId)
    {
        lock (this)
        {
            if (userId is { } id && GetGroupSettings(groupId).ThrottledUsers.TryGetValue(id, out var user))
            {
                return user;
            }

            return new UserState();
        }
    }

    public void SetUserState(long groupId, long userId, ulong time, ChatPermissions permissions)
    {
        lock (this)
        {
            GetGroupSettings(groupId).ThrottledUsers[userId] = new UserState
            {
                Id = userId,
                DefaultPermissions = permissions,
                ThrottleTime = time
            };

            Save();
        }
    }

    public GroupSettings GetGroupSettings(long groupId)
    {
        lock (this)
        {
            if (!GroupSettings.TryGetValue(groupId, out var settings))
            {
                settings = new GroupSettings();
                GroupSettings[groupId] = settings;
            }

            return settings;
        }
    }

    public void SetGroupSettings<T>(long groupId, string setting, T value)
    {
        lock (this)
        {
            var groupSettings = GetGroupSettings(groupId);

            var property = typeof(GroupSettings).GetProperty(setting);
            if (property == null || !property.CanWrite)
            {
                throw new ArgumentException($"Setting '{setting}' is not valid or cannot be set.");
            }

            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (typeof(T) != underlyingType && typeof(T) != propertyType)
            {
                throw new ArgumentException($"The value type {typeof(T).Name} is not compatible with the setting '{setting}' which expects {propertyType.Name}.");
            }

            property.SetValue(groupSettings, value);
            GroupSettings[groupId] = groupSettings;

            Save();
        }
    }

    internal void Save()
    {
        lock (this)
        {
            var json = JsonSerializer.Serialize(this, _jsonSerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
    }

    public static BotSettings Load()
    {
        lock (SettingsFilePath)
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                try
                {
                    if (JsonSerializer.Deserialize<BotSettings>(json) is { } settings)
                    {
                        return settings;
                    }
                }
                catch (JsonException)
                {
                    Console.Error.WriteLine("Settings corrupted and will be reset.");
                    try
                    {
                        File.Delete(SettingsFilePath);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return new BotSettings();
        }
    }
}