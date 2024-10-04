﻿using System.Reflection;
using System.Text.Json;

namespace OrgBot;

public class BotSettings
{
    private static readonly string SettingsFilePath;

    static BotSettings()
    {
        var settingsPath = Environment.GetEnvironmentVariable("SETTINGS_PATH");
        SettingsFilePath = !string.IsNullOrWhiteSpace(settingsPath) ? settingsPath : "botsettings.json";
    }

    private bool _engaged = true;
    private uint _logSize;

    public Dictionary<long, GroupSettings> GroupSettings { get; init; } = [];

    public bool Engaged
    {
        get => _engaged;
        set
        {
            _engaged = value;
            Save();
        }
    }

    public uint LogSize
    {
        get => _logSize;
        set
        {
            _logSize = value;
            Save();
        }
    }

    public GroupSettings GetGroupSettings(long groupId)
    {
        if (!GroupSettings.TryGetValue(groupId, out var settings))
        {
            settings = new GroupSettings();
            GroupSettings[groupId] = settings;
        }
        return settings;
    }

    public void SetGroupSettings<T>(long groupId, string setting, T value)
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

    private void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    public static BotSettings Load()
    {
        if (File.Exists(SettingsFilePath))
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<BotSettings>(json) ?? new BotSettings();
        }

        return new BotSettings();
    }
}