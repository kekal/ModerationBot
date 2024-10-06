namespace OrgBot.Tests;

[TestClass]
public class BotSettingsTests
{
    private const string TestSettingsFilePath = "test_botsettings.json";

    [TestInitialize]
    public void TestInitialize()
    {
        Environment.SetEnvironmentVariable("SETTINGS_PATH", TestSettingsFilePath);

        if (File.Exists(TestSettingsFilePath))
        {
            File.Delete(TestSettingsFilePath);
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (File.Exists(TestSettingsFilePath))
        {
            File.Delete(TestSettingsFilePath);
        }

        Environment.SetEnvironmentVariable("SETTINGS_PATH", null);
    }

    [TestMethod]
    public void TestDefaultValues()
    {
        // Arrange
        var settings = new BotSettings();

        // Assert
        Assert.IsTrue(settings.Engaged);
        Assert.AreEqual(0u, settings.LogSize);
        Assert.IsNotNull(settings.GroupSettings);
        Assert.AreEqual(0, settings.GroupSettings.Count);
    }

    [TestMethod]
    public void TestEngagedProperty()
    {
        // Arrange
        var settings = new BotSettings
        {
            Engaged = false
        };

        // Assert
        Assert.IsFalse(settings.Engaged);
    }

    [TestMethod]
    public void TestLogSizeProperty()
    {
        // Arrange
        var settings = new BotSettings
        {
            LogSize = 50
        };

        // Assert
        Assert.AreEqual(50u, settings.LogSize);
    }

    [TestMethod]
    public void TestGetGroupSettings_CreatesNewIfNotExists()
    {
        // Arrange
        var settings = new BotSettings();
        const long groupId = 12345;

        // Act
        var groupSettings = settings.GetGroupSettings(groupId);

        // Assert
        Assert.IsNotNull(groupSettings);
        Assert.AreEqual(1, settings.GroupSettings.Count);
        Assert.IsTrue(settings.GroupSettings.ContainsKey(groupId));
    }

    [TestMethod]
    public void TestSetGroupSettings_ValidProperty()
    {
        // Arrange
        var settings = new BotSettings();
        const long groupId = 12345;

        // Act
        settings.SetGroupSettings(groupId, "BanUsers", true);

        // Assert
        var groupSettings = settings.GetGroupSettings(groupId);
        Assert.IsTrue(groupSettings.BanUsers);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestSetGroupSettings_InvalidProperty()
    {
        // Arrange
        var settings = new BotSettings();
        const long groupId = 12345;

        // Act
        settings.SetGroupSettings(groupId, "NonExistentProperty", true);
    }

    [TestMethod]
    public void TestSaveAndLoad()
    {
        // Arrange
        var settings = new BotSettings
        {
            Engaged = false,
            LogSize = 30
        };
        const long groupId = 12345;
        settings.SetGroupSettings(groupId, "BanUsers", true);

        // Act
        var loadedSettings = BotSettings.Load();

        // Assert
        Assert.IsFalse(loadedSettings.Engaged);
        Assert.AreEqual(30u, loadedSettings.LogSize);
        var groupSettings = loadedSettings.GetGroupSettings(groupId);
        Assert.IsTrue(groupSettings.BanUsers);
    }

    [TestMethod]
    public void TestGroupSettingsSerialization()
    {
        // Arrange
        var settings = BotSettings.Load();
        const long groupId = 12345;
        var groupSettings = settings.GetGroupSettings(groupId);
        groupSettings.BanUsers = true;
        groupSettings.UseMute = false;
        settings.Save();

        // Act
        var loadedSettings = BotSettings.Load();
        var loadedGroupSettings = loadedSettings.GetGroupSettings(groupId);

        // Assert
        Assert.IsTrue(loadedGroupSettings.BanUsers);
        Assert.IsFalse(loadedGroupSettings.UseMute);
    }
}