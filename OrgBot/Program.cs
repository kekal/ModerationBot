﻿using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot;

public static class Program
{
    private const uint LogSize = 30;
    private const double Timeout = 1;
    private static readonly BotSettings Settings = new();
    private static long? _ownerId;
    private static bool _engaged = true;
    private static readonly List<string> ActionLog = [];
    private static readonly CancellationTokenSource Cts = new();
    private static TelegramLogger Logger { get; } = new(ActionLog);


    public static async Task Main()
    {
        try
        {
            var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            var botOwner = Environment.GetEnvironmentVariable("OWNER");

            if (botToken == null)
            {
                await Logger.LogErrorAsync("BOT_TOKEN env variable is not specified");
                return;
            }

            if (long.TryParse(botOwner, out var ownerId))
            {
                _ownerId = ownerId;
                await Logger.LogInformationAsync($"Bot will work only for chats created by the user {_ownerId}");
            }

            var client = new ThrottledTelegramBotClient(botToken, null, TimeSpan.FromSeconds(Timeout))
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            await SetCommandsAsync(client);

            var me = await client.GetMeAsync(Cts.Token);
            await Logger.LogInformationAsync($"Start listening for @{me.Username} ({me.Id})");

            await client.SendContactAsync(ownerId, me.Id.ToString(), " Bot service ", null, "has been started", cancellationToken: Cts.Token);

            var updateOffset = 0;
            while (!Cts.Token.IsCancellationRequested)
            {
                var updates = await client.GetUpdatesAsync(
                    offset: updateOffset,
                    limit: 100,
                    timeout: 60,
                    allowedUpdates: [UpdateType.Message],
                    cancellationToken: Cts.Token);

                foreach (var update in updates)
                {
                    if (Cts.Token.IsCancellationRequested)
                        break;

                    await HandleUpdateAsync(client, update, Cts.Token);

                    updateOffset = update.Id + 1;
                    await Task.Delay(2000, Cts.Token);
                }
            }
        }
        catch (Exception e)
        {
            await Logger.LogErrorAsync(e.ToString());
        }
    }

    private static async Task SetCommandsAsync(ThrottledTelegramBotClient client)
    {
        var groupCommands = new[]
        {
            new BotCommand { Command = "ban", Description = "Enable banning users when deleting spam" },
            new BotCommand { Command = "no_restrict", Description = "Disable restricting users when deleting spam" },
            new BotCommand { Command = "mute", Description = "Mute users instead of banning" },
            new BotCommand { Command = "set_spam_time", Description = "Set the spam time window in seconds" },
            new BotCommand { Command = "set_restriction_time", Description = "Set the restriction duration in minutes or '0' for infinite" },
            new BotCommand { Command = "silent", Description = "Toggle silent mode (no messages on spam actions)" },
            new BotCommand { Command = "help", Description = "Show available commands" }
        };

        var privateCommands = new[]
        {
            new BotCommand { Command = "log", Description = $"Show the last {LogSize} actions" },
            new BotCommand { Command = "engage", Description = "Start processing updates" },
            new BotCommand { Command = "disengage", Description = "Stop processing updates" },
            new BotCommand { Command = "restart_service", Description = "Restarting the service" },
            new BotCommand { Command = "exit", Description = "Stop the bot" },
            new BotCommand { Command = "help", Description = "Show available commands" }
        };

        await client.SetMyCommandsAsync(groupCommands, new BotCommandScopeAllGroupChats());

        await client.SetMyCommandsAsync(privateCommands, new BotCommandScopeAllPrivateChats());
    }

    private static async Task HandleUpdateAsync(ThrottledTelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        if (!_engaged || update.Type != UpdateType.Message || update.Message is not { } message || message.From == null || message.From.Id == client.BotId)
        {
            return;
        }

        try
        {
            if (message.Chat.Type == ChatType.Private)
            {
                await ProcessPrivateMessageAsync(client, message, cancellationToken);
            }
            else
            {
                await ProcessGroupMessageAsync(client, message, cancellationToken);
            }
        }
        catch (ApiRequestException ex)
        {
            await Logger.LogErrorAsync(PrintApiError(ex));
  
            if (ex.ErrorCode == 429)
            {
                await Logger.LogErrorAsync("Api is overloaded. Set timeout 30 seconds.");
                await Task.Delay(35000, cancellationToken);
            }
        }
        catch (Exception e)
        {
            await Logger.LogErrorAsync($"Failed to handle message: {e}");
        }
    }

    private static async Task ProcessPrivateMessageAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (_ownerId.HasValue && message.From?.Id != _ownerId)
        {
            await client.SendTextMessageAsync(message.Chat.Id, "You are not the bot owner.", cancellationToken: cancellationToken);
            await client.LeaveChatAsync(message.Chat.Id, cancellationToken: cancellationToken);
            return;
        }
        
        if (message.Entities?.Any(e => e.Type == MessageEntityType.BotCommand) == true)
        {
            await ProcessPrivateCommandAsync(client, message, cancellationToken);
        }
        else
        {
            await client.SendTextMessageAsync(message.Chat.Id, "Please use commands to interact with the bot. Use /help to see available commands.", cancellationToken: cancellationToken);
        }
    }

    private static async Task ProcessPrivateCommandAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var commandEntity = message.Entities!.First(e => e.Type == MessageEntityType.BotCommand);
        var commandText = message.Text!.Substring(commandEntity.Offset, commandEntity.Length);

        switch (commandText.Split('@')[0])
        {
            case "/log":
                string logContent;
                lock (ActionLog)
                {
                    logContent = string.Join("\n", ActionLog);
                }
                await client.SendTextMessageAsync(message.Chat.Id, string.IsNullOrWhiteSpace(logContent) ? "No actions logged yet." : logContent, cancellationToken: cancellationToken);
                break;

            case "/engage":
                _engaged = true;
                await client.SendTextMessageAsync(message.Chat.Id, "Bot is now engaged and processing updates.", cancellationToken: cancellationToken);
                break;

            case "/disengage":
                _engaged = false;
                await client.SendTextMessageAsync(message.Chat.Id, "Bot is now disengaged and will not process updates.", cancellationToken: cancellationToken);
                break;

            case "/restart_service":
                await client.SendTextMessageAsync(message.Chat.Id, "Restarting the service.", cancellationToken: cancellationToken);
                Environment.Exit(42);  // Upgrade
                break;


            case "/exit":
                await client.SendTextMessageAsync(message.Chat.Id, "Bot is shutting down.", cancellationToken: cancellationToken);
                if (_ownerId != null) await client.SendContactAsync(_ownerId, client.BotId.ToString() ?? string.Empty, "Bot service ", null, "has been stopped", cancellationToken: cancellationToken);
                Environment.Exit(0); // Stop service
                break;

            case "/help":
                const string privateHelpText = "Available commands:\n" +
                                               "/log - Show the last 30 actions\n" +
                                               "/engage - Start processing updates\n" +
                                               "/disengage - Stop processing updates\n" +
                                               "/restart_service - Restarting the service\n" +
                                               "/exit - Stop the bot\n" +
                                               "/help - Show this help message";
                await client.SendTextMessageAsync(message.Chat.Id, privateHelpText, cancellationToken: cancellationToken);
                break;

            default:
                await client.SendTextMessageAsync(message.Chat.Id, "Unknown command.", cancellationToken: cancellationToken);
                break;
        }
    }

    private static async Task ProcessGroupMessageAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.Entities?.Any(e => e.Type == MessageEntityType.BotCommand) == true)
        {
            await ProcessGroupCommandAsync(client, message, cancellationToken);
            return;
        }

        await HandleSpamAsync(client, message, cancellationToken);
    }

    private static async Task ProcessGroupCommandAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup)
        {
            return;
        }

        if (message.From == null || !await IsUserAdminAsync(client, message.Chat, message.From.Id, cancellationToken))
        {
            return;
        }

        await CheckBotOwner(client, message, cancellationToken);

        var commandEntity = message.Entities!.First(e => e.Type == MessageEntityType.BotCommand);
        var commandText = message.Text!.Substring(commandEntity.Offset, commandEntity.Length);
        var args = message.Text[(commandEntity.Offset + commandEntity.Length)..].Trim();

        switch (commandText.Split('@')[0])
        {
            case "/ban":
                lock (Settings)
                {
                    Settings.BanUsers = true;
                    Settings.UseMute = false;
                }
                await client.SendTextMessageAsync(message.Chat.Id, "Spamming users will be banned.", cancellationToken: cancellationToken);
                break;

            case "/no_restrict":
                lock (Settings)
                {
                    Settings.BanUsers = false;
                    Settings.UseMute = false;
                }
                await client.SendTextMessageAsync(message.Chat.Id, "Spamming users will not be restricted.", cancellationToken: cancellationToken);
                break;

            case "/mute":
                lock (Settings)
                {
                    Settings.UseMute = true;
                    Settings.BanUsers = false;
                }
                await client.SendTextMessageAsync(message.Chat.Id, "Spamming users will be muted.", cancellationToken: cancellationToken);
                break;

            case "/set_spam_time":
                if (byte.TryParse(args, out var seconds) && seconds > 0)
                {
                    lock (Settings)
                    {
                        Settings.SpamTimeWindow = TimeSpan.FromSeconds(seconds);
                    }

                    await client.SendTextMessageAsync(message.Chat.Id, $"Spam time window set to {seconds} seconds.", cancellationToken: cancellationToken);
                }
                else
                {
                    await client.SendTextMessageAsync(message.Chat.Id, $"Invalid time specified. Please provide a positive integer in seconds <= {byte.MaxValue}.", cancellationToken: cancellationToken);
                }
                break;

            case "/set_restriction_time":
                if (args.Trim().Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    lock (Settings)
                    {
                        Settings.RestrictionDuration = null;
                    }
                    await client.SendTextMessageAsync(message.Chat.Id, "Restriction duration set to forever.", cancellationToken: cancellationToken);
                }
                else if (uint.TryParse(args, out var restrictionMinutes) && restrictionMinutes > 0)
                {
                    lock (Settings)
                    {
                        Settings.RestrictionDuration = TimeSpan.FromMinutes(restrictionMinutes);
                    }
                    await client.SendTextMessageAsync(message.Chat.Id, $"Restriction duration set to {restrictionMinutes} minutes.", cancellationToken: cancellationToken);
                }
                else
                {
                    await client.SendTextMessageAsync(message.Chat.Id, "Invalid restriction duration specified. Please provide '0' for infinite or a positive integer in minutes.", cancellationToken: cancellationToken);
                }
                break;

            case "/silent":
                bool silentMode;
                lock (Settings)
                {
                    Settings.SilentMode = !Settings.SilentMode;
                    silentMode = Settings.SilentMode;
                }
                await client.SendTextMessageAsync(message.Chat.Id, $"Silent mode is now {(silentMode ? "enabled" : "disabled")}.", cancellationToken: cancellationToken);
                break;

            case "/help":
                const string helpText = "Available commands:\n" +
                                        "/ban - Enable banning users when deleting spam\n" +
                                        "/no_restrict - Disable restricting users when deleting spam\n" +
                                        "/mute - Mute users instead of banning\n" +
                                        "/set_spam_time <seconds> - Set the spam time window in seconds\n" +
                                        "/set_restriction_time <minutes or 0> - Set the restriction duration in minutes or '0' for infinite\n" +
                                        "/silent - Toggle silent mode (no messages on spam actions)\n" +
                                        "/help - Show this help message";
                await client.SendTextMessageAsync(message.Chat.Id, helpText, cancellationToken: cancellationToken);
                break;

            default:
                await client.SendTextMessageAsync(message.Chat.Id, "Unknown command.", cancellationToken: cancellationToken);
                break;
        }
    }

    private static async Task HandleSpamAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.From == null)
        {
            return;
        }

        if (!await IsReplyToLinkedChannelPost(client, message, cancellationToken))
        {
            return;
        }

        TimeSpan spamTimeWindow;
        bool banUsers;
        bool useMute;
        TimeSpan? restrictionDuration;
        bool silentMode;

        lock (Settings)
        {
            spamTimeWindow = Settings.SpamTimeWindow;
            banUsers = Settings.BanUsers;
            useMute = Settings.UseMute;
            restrictionDuration = Settings.RestrictionDuration;
            silentMode = Settings.SilentMode;
        }

        if (message.Date - message.ReplyToMessage!.Date < spamTimeWindow)
        {
            await CheckBotOwner(client, message, cancellationToken);

            var user = $"{message.From.FirstName} {message.From.LastName} @{message.From.Username} ({message.From.Id}){(message.From.IsBot ? " (bot)" : string.Empty)}";

            await client.DeleteMessageAsync(message.Chat.Id, message.MessageId, cancellationToken);

            var actionTaken = string.Empty;

            if (banUsers)
            {
                if (message.From?.IsBot is true && message.SenderChat?.Id is { } senderChatId)
                {
                    await client.BanChatSenderChatAsync(message.Chat.Id, senderChatId, cancellationToken: cancellationToken);
                }
                else
                {
                    await client.BanChatMemberAsync(
                        message.Chat.Id,
                        message.From!.Id,
                        restrictionDuration.HasValue ? DateTime.UtcNow + restrictionDuration.Value : null,
                        revokeMessages: false,
                        cancellationToken);
                }
                actionTaken = "banned";
            }
            else if (useMute)
            {
                actionTaken = "muted";
                var permissions = new ChatPermissions
                {
                    CanAddWebPagePreviews = false,
                    CanChangeInfo = false,
                    CanInviteUsers = false,
                    CanManageTopics = false,
                    CanPinMessages = false,
                    CanSendAudios = false,
                    CanSendDocuments = false,
                    CanSendMessages = false,
                    CanSendOtherMessages = false,
                    CanSendPhotos = false,
                    CanSendPolls = false,
                    CanSendVideoNotes = false,
                    CanSendVideos = false,
                    CanSendVoiceNotes = false
                };

                await client.RestrictChatMemberAsync(
                    message.Chat.Id,
                    userId: message.From.Id,
                    permissions,
                    untilDate: restrictionDuration.HasValue ? DateTime.UtcNow + restrictionDuration : null,
                    cancellationToken: cancellationToken);
            }

            if (!silentMode)
            {
                if (!string.IsNullOrWhiteSpace(actionTaken))
                {
                    await client.SendTextMessageAsync(
                        message.Chat.Id,
                        $"User {user} has been {actionTaken} for {(restrictionDuration.HasValue ? $"{restrictionDuration.Value.TotalHours} hours" : "forever")}.",
                        disableNotification: true,
                        replyToMessageId: message.MessageId,
                        allowSendingWithoutReply: true,
                        cancellationToken: cancellationToken);
                }
            }

            await Logger.LogInformationAsync($"Deleted message from {user} and {actionTaken} the user.");

                var chatId = message.Chat.Id.ToString();
                chatId = chatId.StartsWith("-100") ? chatId["-100".Length..] : chatId;
                await Logger.LogInformationAsync($"{actionTaken} user {user} in chat {message.Chat.Title}. Deleted message: https://t.me/c/{chatId}/{message.MessageId}");

                lock (ActionLog)
                {
                    if (ActionLog.Count > LogSize)
                    {
                        ActionLog.RemoveAt(0);
                    }
                }
        }
    }

    private static async Task CheckBotOwner(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (_ownerId.HasValue)
        {
            var administrators = await client.GetChatAdministratorsAsync(message.Chat.Id, cancellationToken: cancellationToken);
            if (administrators.FirstOrDefault(admin => admin.Status == ChatMemberStatus.Creator)?.User.Id != _ownerId)
            {
                await client.SendTextMessageAsync(
                    message.Chat.Id,
                    "This chat does not belong to bot owner.",
                    disableNotification: true,
                    cancellationToken: cancellationToken);

                await client.LeaveChatAsync(message.Chat.Id, cancellationToken: cancellationToken);
            }
        }
    }

    private static async Task<bool> IsUserAdminAsync(ThrottledTelegramBotClient client, Chat chat, long userId, CancellationToken cancellationToken)
    {
        var chatMember = await client.GetChatMemberAsync(chat.Id, userId, cancellationToken);
        return chatMember.Status == ChatMemberStatus.Creator || chatMember is ChatMemberAdministrator { CanRestrictMembers: true };
    }

    private static string PrintApiError(ApiRequestException ex)
    {
        StringBuilder sb = new();

        sb.AppendLine($"ErrorCode: {ex.ErrorCode}");
        if (ex.Parameters?.MigrateToChatId != null)
        {
            sb.AppendLine($"MigrateToChatId: {ex.Parameters.MigrateToChatId}");
        }

        if (ex.Parameters?.RetryAfter != null)
        {
            sb.AppendLine($"RetryAfter: {ex.Parameters.RetryAfter}");
        }

        sb.AppendLine(ex.Message);

        return $"API Error: \n{sb}";
    }

    private static async Task<bool> IsReplyToLinkedChannelPost(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.ReplyToMessage?.SenderChat is not { Type: ChatType.Channel })
        {
            return false;
        }

        var discussionGroup = await client.GetChatAsync(message.Chat.Id, cancellationToken);
        var channelId = discussionGroup.LinkedChatId;

        return message.ReplyToMessage.SenderChat.Id == channelId;
    }
}



public class ThrottledTelegramBotClient : TelegramBotClient
{
    private readonly TelegramBotClient _client;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _delayBetweenRequests;


    public ThrottledTelegramBotClient(TelegramBotClientOptions options, HttpClient? httpClient, TimeSpan delayBetweenRequests) : base(options, httpClient)
    {
        _delayBetweenRequests = delayBetweenRequests;
        _client = this;
    }

    public ThrottledTelegramBotClient(string token, HttpClient? httpClient, TimeSpan delayBetweenRequests) : base(token, httpClient)
    {
        _delayBetweenRequests = delayBetweenRequests;
        _client = this;
    }

    private async Task<TResult> ExecuteWithDelayAsync<TResult>(Func<Task<TResult>> apiCall)
    {
        await _semaphore.WaitAsync();
        try
        {
            var result = await apiCall();
            await Task.Delay(_delayBetweenRequests);
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ExecuteWithDelayAsync(Func<Task> apiCall)
    {
        await _semaphore.WaitAsync();
        try
        {
            await apiCall();
            await Task.Delay(_delayBetweenRequests);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<User> GetMeAsync(CancellationToken cancellationToken = default) => 
        ExecuteWithDelayAsync(() => _client.GetMeAsync(cancellationToken));

    public Task SetMyCommandsAsync(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default, CancellationToken cancellationToken = default) => 
        ExecuteWithDelayAsync(() => _client.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken));

    public Task<Message> SendContactAsync(ChatId chatId, string phoneNumber, string firstName, int? messageThreadId = default, string? lastName = default, string? vCard = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.SendContactAsync(chatId, phoneNumber, firstName, messageThreadId, lastName, vCard, disableNotification, protectContent, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken));

    public Task LeaveChatAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.LeaveChatAsync(chatId, cancellationToken));

    public Task DeleteMessageAsync(ChatId chatId, int messageId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.DeleteMessageAsync(chatId, messageId, cancellationToken));

    public Task BanChatSenderChatAsync(ChatId chatId, long senderChatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken));

    public Task BanChatMemberAsync(ChatId chatId, long userId, DateTime? untilDate = null, bool revokeMessages = false, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken));

    public Task RestrictChatMemberAsync(ChatId chatId, long userId, ChatPermissions permissions, bool? useIndependentChatPermissions = default, DateTime? untilDate = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.RestrictChatMemberAsync(chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken));

    public Task<ChatMember[]> GetChatAdministratorsAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.GetChatAdministratorsAsync(chatId, cancellationToken));

    public Task<Chat> GetChatAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => _client.GetChatAsync(chatId, cancellationToken));
}


public class BotSettings
{
    public bool BanUsers { get; set; }
    public TimeSpan SpamTimeWindow { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan? RestrictionDuration { get; set; } = TimeSpan.FromDays(1); // null for permanent ban/mute
    public bool SilentMode { get; set; }
    public bool UseMute { get; set; } = true;
}





internal class TelegramLogger(IList<string> actionList) : ILogger
{
    private IList<string> ActionLog { get; set; } = actionList;
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    public bool IsEnabled(LogLevel logLevel) => true;


    public async Task LogInformationAsync(string message) => await LogAsync(LogLevel.Information, message);
    public async Task LogErrorAsync(string message) => await LogAsync(LogLevel.Error, message);

    private async Task LogAsync(LogLevel logLevel, string message)
    {
        var formattedMessage = $"[{DateTime.UtcNow}] {message}";
        ActionLog.Add(formattedMessage);

        if (logLevel == LogLevel.Error)
            await Console.Error.WriteLineAsync(formattedMessage);
        else
            await Console.Out.WriteLineAsync(formattedMessage);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogAsync(logLevel, state?.ToString() ?? string.Empty).Wait();
    }
}
