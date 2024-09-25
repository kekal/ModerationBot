using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

[assembly: InternalsVisibleTo("OrgBot.Tests")]

namespace OrgBot;

public class BotLogic(string botToken, long? ownerId)
{
    private const uint LogSize = 30;
    private const double ThrottlingTimeout = 2;
    internal readonly BotSettings Settings = new();
    private bool _engaged = true;
    internal readonly List<string> ActionLog = [];
    private TelegramLogger? Logger { get; set; }
    private readonly CancellationTokenSource _cts = new();


    public async Task RunAsync()
    {
        SetupLogger();

        try
        {
            if (Logger == null)
            {
                Console.Error.WriteLine("Failed to setup logging.");
                Environment.Exit(1);
            }

            using var client = new ThrottledTelegramBotClient(new TelegramBotWrapper(botToken), TimeSpan.FromSeconds(ThrottlingTimeout));

            await SetCommandsAsync(client);

            var me = await client.GetMeAsync(_cts.Token);
            await Logger.LogInformationAsync($"Start listening for @{me.Username} ({me.Id})");

            if (ownerId != null)
            {
                await client.SendContactAsync(ownerId, me.Id.ToString(), " Bot service ", null, "has been started", cancellationToken: _cts.Token);
            }

            var oldUpdates = await client.GetUpdatesAsync(offset: -1, limit: 0, timeout: 0, allowedUpdates: [UpdateType.Message], cancellationToken: _cts.Token);
            var updateOffset = oldUpdates.Length > 0 ? oldUpdates[^1].Id + 1 : -1;

            await Task.Delay(2000);

            while (!_cts.Token.IsCancellationRequested)
            {
                var updates = await client.GetUpdatesAsync(
                    offset: updateOffset,
                    limit: 100,
                    timeout: 60,
                    allowedUpdates: [UpdateType.Message],
                    cancellationToken: _cts.Token);

                foreach (var update in updates)
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    await HandleUpdateAsync(client, update, _cts.Token);

                    updateOffset = update.Id + 1;
                }
            }
        }
        catch (Exception e)
        {
            await Logger!.LogErrorAsync(e.ToString());
        }
    }

    internal void SetupLogger()
    {
        lock (ActionLog)
        {
            Logger = new TelegramLogger(ActionLog, LogSize);
        }
    }

    internal static async Task SetCommandsAsync(ThrottledTelegramBotClient client)
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

    internal async Task HandleUpdateAsync(ThrottledTelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message is not { } message || message.From == null || message.From.Id == client.BotId)
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
            await Logger!.LogErrorAsync(PrintApiError(ex));
  
            if (ex is { ErrorCode: 429, Parameters.RetryAfter: not null })
            {
                var retryAfter = ex.Parameters.RetryAfter.Value;
                await Logger.LogErrorAsync($"API rate limit exceeded. Retrying after {(int)(retryAfter * 1.1)} seconds.");
                await Task.Delay(retryAfter * 1100, cancellationToken);
            }
        }
        catch (Exception e)
        {
            await Logger!.LogErrorAsync($"Failed to handle message: {e}");
        }
    }

    internal async Task ProcessPrivateMessageAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (ownerId.HasValue && message.From?.Id != ownerId)
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

    internal async Task ProcessPrivateCommandAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
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
                await _cts.CancelAsync();
                Environment.Exit(42);  // Upgrade
                break;


            case "/exit":
                await client.SendTextMessageAsync(message.Chat.Id, "Bot is shutting down.", cancellationToken: cancellationToken);
                if (ownerId != null) await client.SendContactAsync(ownerId, client.BotId.ToString() ?? string.Empty, "Bot service ", null, "has been stopped", cancellationToken: cancellationToken);
                await _cts.CancelAsync();
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

    internal async Task ProcessGroupMessageAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.Entities?.Any(e => e.Type == MessageEntityType.BotCommand) == true)
        {
            await ProcessGroupCommandAsync(client, message, cancellationToken);
            return;
        }

        await HandleSpamAsync(client, message, cancellationToken);
    }

    internal async Task ProcessGroupCommandAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
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

    internal async Task HandleSpamAsync(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (!_engaged || message.From == null)
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

            await Logger!.LogInformationAsync($"Deleted message from {user} and {actionTaken} the user.");

                var chatId = message.Chat.Id.ToString();
                chatId = chatId.StartsWith("-100") ? chatId["-100".Length..] : chatId;
                await Logger.LogInformationAsync($"{actionTaken} user {user} in chat {message.Chat.Title}. Deleted message: https://t.me/c/{chatId}/{message.MessageId}");
        }
    }

    internal async Task CheckBotOwner(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (ownerId.HasValue)
        {
            var administrators = await client.GetChatAdministratorsAsync(message.Chat.Id, cancellationToken: cancellationToken);
            if (administrators.FirstOrDefault(admin => admin.Status == ChatMemberStatus.Creator)?.User.Id != ownerId)
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

    internal async Task<bool> IsUserAdminAsync(ThrottledTelegramBotClient client, Chat chat, long userId, CancellationToken cancellationToken)
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

    internal async Task<bool> IsReplyToLinkedChannelPost(ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
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