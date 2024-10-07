using Microsoft.Extensions.Logging;

namespace OrgBot;

public class TelegramLogger(List<string> actionList, uint logSize) : ILogger
{
    private List<string> ActionLog { get; } = actionList;
    private uint LogSize { get; } = logSize;
    private readonly string _logFilePath = Path.Combine(".", "data", "bot_log.txt");

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public async Task LogInformationAsync(string message) => await LogAsync(LogLevel.Information, message);
    public async Task LogErrorAsync(string message) => await LogAsync(LogLevel.Error, message);

    private async Task LogAsync(LogLevel logLevel, string message)
    {
        var formattedMessage = $"[{DateTime.UtcNow}] {message}";

        AddToActionLog(formattedMessage);

        if (logLevel == LogLevel.Error)
            await Console.Error.WriteLineAsync(formattedMessage);
        else
            await Console.Out.WriteLineAsync(formattedMessage);

        await File.AppendAllLinesAsync(_logFilePath, [formattedMessage]);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _ = LogAsync(logLevel, state?.ToString() ?? string.Empty);
    }

    private void AddToActionLog(string message)
    {
        lock (ActionLog)
        {
            ActionLog.Add(message);
            if (ActionLog.Count > LogSize)
            {
                ActionLog.RemoveAt(0);
            }
        }
    }
}