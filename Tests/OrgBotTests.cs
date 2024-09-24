using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OrgBot.Tests;

[TestClass]
public class BotSettingsTests
{
    [TestMethod]
    public void TestDefaultValues()
    {
        // Arrange
        var settings = new BotSettings();

        // Assert
        Assert.IsFalse(settings.BanUsers);
        Assert.IsTrue(settings.UseMute);
        Assert.AreEqual(TimeSpan.FromSeconds(10), settings.SpamTimeWindow);
        Assert.AreEqual(TimeSpan.FromDays(1), settings.RestrictionDuration);
        Assert.IsFalse(settings.SilentMode);
    }

    [TestMethod]
    public void TestPropertySetters()
    {
        // Arrange
        var settings = new BotSettings();

        // Act
        settings.BanUsers = true;
        settings.UseMute = false;
        settings.SpamTimeWindow = TimeSpan.FromSeconds(30);
        settings.RestrictionDuration = TimeSpan.FromHours(5);
        settings.SilentMode = true;

        // Assert
        Assert.IsTrue(settings.BanUsers);
        Assert.IsFalse(settings.UseMute);
        Assert.AreEqual(TimeSpan.FromSeconds(30), settings.SpamTimeWindow);
        Assert.AreEqual(TimeSpan.FromHours(5), settings.RestrictionDuration);
        Assert.IsTrue(settings.SilentMode);
    }

    [TestMethod]
    public void TestRestrictionDurationNull()
    {
        // Arrange
        var settings = new BotSettings();

        // Act
        settings.RestrictionDuration = null;

        // Assert
        Assert.IsNull(settings.RestrictionDuration);
    }
}