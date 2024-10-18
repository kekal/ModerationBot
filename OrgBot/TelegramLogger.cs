using System.Text;
using Microsoft.Extensions.Logging;

namespace OrgBot;

/// <inheritdoc cref="Logger{T}"/>>
public class TelegramLogger : ILogger
{
    private readonly Action<string>? _notificationHandler;

    private int LogSize { get; }
    private readonly string _logFilePath = Path.Combine(".", "data", "bot_log.txt");

    /// <inheritdoc cref="Logger{T}"/>>
    public TelegramLogger(int logSize, Action<string>? notificationHandler = null)
    {
        _notificationHandler = notificationHandler;
        LogSize = logSize;

        var logDirectory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public async Task LogInformationAsync(string message, bool notify = true) => await LogAsync(LogLevel.Information, message, notify);
    public async Task LogErrorAsync(string message, bool notify = true) => await LogAsync(LogLevel.Error, message, notify);

    private async Task LogAsync(LogLevel logLevel, string message, bool notify)
    {
        var formattedMessage = $"[{DateTime.UtcNow}] {message}";
        try
        {
            if (notify && _notificationHandler != null)
            {
                _notificationHandler(formattedMessage);
            }

            if (logLevel == LogLevel.Error)
            {
                await Console.Error.WriteLineAsync(formattedMessage);
            }
            else
            {
                await Console.Out.WriteLineAsync(formattedMessage);
            }

            await File.AppendAllLinesAsync(_logFilePath, [formattedMessage]);
        }
        catch (SystemException ex)
        {
            var ioMessage = $"[{DateTime.UtcNow}] Error: Unable to write to the [{_logFilePath}]. Access denied.{Environment.NewLine}{ex.Message}";
            try
            {
                if (notify && _notificationHandler != null)
                {
                    _notificationHandler(ioMessage);
                }

                await Console.Error.WriteLineAsync(ioMessage);
            }
            catch
            {
                // ignored
            }
        }
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _ = LogAsync(logLevel, state?.ToString() ?? string.Empty, true);
    }

    public string GetLog()
    {
        const int maxLength = 4096;
        var lines = File.ReadLines(_logFilePath).Reverse();
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trim = line.Trim();
            if (sb.Length + trim.Length + Environment.NewLine.Length > maxLength)
                break;

            sb.Insert(0, trim + Environment.NewLine);
        }

        return sb.ToString();
    }
}