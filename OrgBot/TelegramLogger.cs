using Microsoft.Extensions.Logging;

namespace OrgBot;

public class TelegramLogger(IList<string> actionList, uint logSize) : ILogger
{
    private IList<string> ActionLog { get; } = actionList;
    private uint LogSize { get; } = logSize;
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