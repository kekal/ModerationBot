using Microsoft.Extensions.Logging;

namespace OrgBot;

/// <inheritdoc cref="Logger{T}"/>>
public class TelegramLogger : ILogger
{
    private List<string> ActionLog { get; }
    private uint LogSize { get; }
    private readonly string _logFilePath = Path.Combine(".", "data", "bot_log.txt");

    /// <inheritdoc cref="Logger{T}"/>>
    public TelegramLogger(List<string> actionList, uint logSize)
    {
        ActionLog = actionList;
        LogSize = logSize;

        var logDirectory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }

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

        try
        {
            await File.AppendAllLinesAsync(_logFilePath, [formattedMessage]);
        }
        catch (SystemException ex)
        {
            var ioMessage = $"Error: Unable to write to the [{_logFilePath}]. Access denied.{Environment.NewLine}{ex.Message}";
            
            AddToActionLog(ioMessage);
            await Console.Error.WriteLineAsync(ioMessage);
        }
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