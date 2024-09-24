using Microsoft.Extensions.Logging;

namespace OrgBot.Tests;

[TestClass]
public class TelegramLoggerTests
{
    private List<string> _actionLog;
    private TelegramLogger _logger;
    private const uint LogSize = 5;

    [TestInitialize]
    public void Setup()
    {
        _actionLog = new List<string>();
        _logger = new TelegramLogger(_actionLog, LogSize);
    }

    [TestMethod]
    public async Task TestLogInformationAsync()
    {
        // Arrange
        var message = "Test information message";

        // Act
        await _logger.LogInformationAsync(message);

        // Assert
        Assert.AreEqual(1, _actionLog.Count);
        StringAssert.Contains(_actionLog[0], message);
    }

    [TestMethod]
    public async Task TestLogErrorAsync()
    {
        // Arrange
        var message = "Test error message";

        // Act
        await _logger.LogErrorAsync(message);

        // Assert
        Assert.AreEqual(1, _actionLog.Count);
        StringAssert.Contains(_actionLog[0], message);
    }

    [TestMethod]
    public async Task TestLogSizeLimit()
    {
        // Arrange
        for (int i = 1; i <= LogSize + 2; i++)
        {
            await _logger.LogInformationAsync($"Message {i}");
        }

        // Assert
        Assert.AreEqual(LogSize, (uint)_actionLog.Count);
        StringAssert.Contains(_actionLog[0], $"Message {3}");
        StringAssert.Contains(_actionLog[(int)LogSize - 1], $"Message {LogSize + 2}");
    }

    [TestMethod]
    public void TestLogMethod()
    {
        // Arrange
        var message = "Test log method message";

        // Act
        _logger.Log(LogLevel.Information, new EventId(), message, null, (s, _) => s);

        // Assert
        Assert.AreEqual(1, _actionLog.Count);
        StringAssert.Contains(_actionLog[0], message);
    }

    [TestMethod]
    public void TestIsEnabled()
    {
        // Act
        var isEnabled = _logger.IsEnabled(LogLevel.Information);

        // Assert
        Assert.IsTrue(isEnabled);
    }

    [TestMethod]
    public void TestBeginScope()
    {
        // Act
        var scope = _logger.BeginScope("Test Scope");

        // Assert
        Assert.IsNull(scope);
    }
}