using Microsoft.Extensions.Logging;

namespace OrgBot.Tests;

[TestClass]
public class TelegramLoggerTests
{
    private List<string> _actionLog = null!;
    private TelegramLogger _logger = null!;
    private const int LogSize = 5;

    [TestInitialize]
    public void Setup()
    {
        _actionLog = [];
        _logger = new TelegramLogger(message => _actionLog.Add(message));
    }

    [TestMethod]
    public async Task TestLogInformationAsync()
    {
        // Arrange
        const string message = "Test information message";

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
        const string message = "Test error message";

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
        for (var i = 1; i <= LogSize; i++)
        {
            await _logger.LogInformationAsync($"Message {i}");
        }

        // Assert
        Assert.AreEqual(LogSize, _actionLog.Count);
        StringAssert.Contains(_actionLog[2], "Message 3");
        StringAssert.Contains(_actionLog[LogSize - 1], $"Message {LogSize}");
    }

    [TestMethod]
    public void TestLogMethod()
    {
        // Arrange
        const string message = "Test log method message";

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