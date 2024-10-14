using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TTBC = ThrottledTelegramBotClient;
using TTBCT = ThrottledTelegramBotClient.TestingEntities;

[assembly: InternalsVisibleTo("OrgBot.Tests")]

namespace OrgBot;

public class BotLogic(string botToken, long? ownerId, TTBCT.IApplicationLifetime applicationLifetime)
{
    internal const int GroupAnonymousAdminId = 1087968824;
    internal const int GenericTelegramId = 777000;
    internal const int UserAsTheChannelId = 136817688;

    private const double ThrottlingTimeout = 1;
    internal readonly BotSettings Settings = BotSettings.Load();
    internal readonly List<string> ActionLog = [];
    private TelegramLogger? Logger { get; set; }
    private readonly CancellationTokenSource _cts = new();


    public async Task RunAsync()
    {
        SetupLogger();

        TTBC.ThrottledTelegramBotClient? client = null;

        try
        {
            if (Logger == null)
            {
                Console.Error.WriteLine("Failed to setup logging.");
                applicationLifetime.Exit(1);
            }

            
            using (client = new TTBC.ThrottledTelegramBotClient(new TTBCT.TelegramBotWrapper(botToken), TimeSpan.FromSeconds(ThrottlingTimeout)))
            {
                await SetCommandsAsync(client);

                var me = await client.GetMeAsync(_cts.Token);
                await Logger!.LogInformationAsync($"Start listening for @{me.Username} ({me.Id})");

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
                        allowedUpdates: [UpdateType.Message, UpdateType.MyChatMember],
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
        }
        catch (ApiRequestException ex)
        {
            await LastChanceNotification(client, PrintApiError(ex));

            await Logger!.LogErrorAsync(PrintApiError(ex));
            if (ex is { ErrorCode: 429, Parameters.RetryAfter: { } retryAfter })
            {
                await Logger.LogErrorAsync(string.Format(Resource.API_rate_limit_exceeded, retryAfter));
                Console.WriteLine(Resource.Too_many_requests, retryAfter);
                await Task.Delay(TimeSpan.FromSeconds(retryAfter + 5));
            }

            applicationLifetime.Exit(24);
        }
        catch (Exception e)
        {
            var error = string.Join(" | ", e.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            await LastChanceNotification(client, error);
            await Logger!.LogErrorAsync(error);

            await Task.Delay(TimeSpan.FromMinutes(5));

            applicationLifetime.Exit(2);
        }
    }

    private async Task LastChanceNotification(TTBC.ThrottledTelegramBotClient? client, string text)
    {
        try
        {
            if (ownerId.HasValue && client != null)
            {
                await client.SendTextMessageAsync(ownerId, text, allowSendingWithoutReply: true, cancellationToken: _cts.Token);
            }
        }
        catch
        {
            // ignored
        }
    }

    internal void SetupLogger()
    {
        lock (ActionLog)
        {
            lock (Settings)
            {
                Logger = new TelegramLogger(ActionLog, Settings.LogSize);
            }
        }
    }

    internal async Task SetCommandsAsync(TTBC.ThrottledTelegramBotClient client)
    {
        var logSize = Settings.LogSize;

        var groupCommands = new[]
        {
            new BotCommand { Command = "ban", Description = Resource.Enable_banning },
            new BotCommand { Command = "no_restrict", Description = Resource.Disable_any_restricting },
            new BotCommand { Command = "mute", Description = Resource.Enable_muting },
            new BotCommand { Command = "set_spam_time", Description = Resource.Set_the_spam_time },
            new BotCommand { Command = "set_restriction_time", Description = Resource.Set_the_restriction_duration },
            new BotCommand { Command = "silent", Description = Resource.Toggle_silent },
            new BotCommand { Command = "help", Description = Resource.ShowHelp }
        };

        var privateCommands = new[]
        {
            new BotCommand { Command = "log", Description = string.Format(Resource.Show_the_last_actions, logSize) },
            new BotCommand { Command = "engage", Description = Resource.Start },
            new BotCommand { Command = "disengage", Description = Resource.Pause },
            new BotCommand { Command = "restart_service", Description = Resource.Restarting },
            new BotCommand { Command = "exit", Description = Resource.Stop },
            new BotCommand { Command = "help", Description = Resource.ShowHelp }
        };

        await client.SetMyCommandsAsync(groupCommands, new BotCommandScopeAllGroupChats());

        await client.SetMyCommandsAsync(privateCommands, new BotCommandScopeAllPrivateChats());
    }

    internal async Task HandleUpdateAsync(TTBC.ThrottledTelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update is { Type: UpdateType.MyChatMember, MyChatMember.NewChatMember: { User.Id: var botId, Status: ChatMemberStatus.Administrator } } && botId == client.BotId)
            {
                await CheckGroupOwnership(client, update.MyChatMember.Chat.Id, cancellationToken);
                return;
            }

            if (update.Type != UpdateType.Message || update.Message is not { From: not null } message || message.From.Id == client.BotId)
            {
                return;
            }

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
            await LastChanceNotification(client, PrintApiError(ex));
            await Logger!.LogErrorAsync(PrintApiError(ex));

            if (ex is { ErrorCode: 429, Parameters.RetryAfter: not null })
            {
                var retryAfter = ex.Parameters.RetryAfter.Value;
                await Logger.LogErrorAsync(string.Format(Resource.API_rate_limit_exceeded, retryAfter + 10));
                await Task.Delay((retryAfter + 10) * 1000, cancellationToken);
            }
        }
        catch (Exception e)
        {
            var error = string.Join(" | ", e.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            await LastChanceNotification(client, error);
            await Logger!.LogErrorAsync($"Failed to handle message: {error}");
        }
    }

    private async Task ProcessPrivateMessageAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (ownerId.HasValue && message.From?.Id != ownerId)
        {
            await client.SendTextMessageAsync(message.Chat.Id, Resource.not_bot_owner, cancellationToken: cancellationToken);
            await client.LeaveChatAsync(message.Chat.Id, cancellationToken: cancellationToken);
            return;
        }
        
        if (message.Entities?.Any(e => e.Type == MessageEntityType.BotCommand) == true)
        {
            await ProcessPrivateCommandAsync(client, message, cancellationToken);
        }
        else
        {
            await client.SendTextMessageAsync(message.Chat.Id, Resource.non_command_error, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessPrivateCommandAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var commandEntity = message.Entities!.First(e => e.Type == MessageEntityType.BotCommand);
        var commandText = message.Text!.Substring(commandEntity.Offset, commandEntity.Length);

        switch (commandText.Split('@')[0])
        {
            case "/log":
                var logContent = string.Join($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", ActionLog);
                await client.SendTextMessageAsync(message.Chat.Id, string.IsNullOrWhiteSpace(logContent) ? "No actions logged yet." : logContent, cancellationToken: cancellationToken);
                break;

            case "/engage":
                Settings.Engaged = true;
                await client.SendTextMessageAsync(message.Chat.Id, Resource.Bot_is_engaged, cancellationToken: cancellationToken);
                break;

            case "/disengage":
                Settings.Engaged = false;
                await client.SendTextMessageAsync(message.Chat.Id, Resource.Bot_disengaged, cancellationToken: cancellationToken);
                break;

            case "/restart_service":
                await client.SendTextMessageAsync(message.Chat.Id, Resource.Restarting, cancellationToken: cancellationToken);
                await _cts.CancelAsync();
                applicationLifetime.Exit(42);  // Upgrade
                break;


            case "/exit":
                await client.SendTextMessageAsync(message.Chat.Id, Resource.shutting_down, cancellationToken: cancellationToken);
                if (ownerId != null) await client.SendContactAsync(ownerId, client.BotId.ToString() ?? string.Empty, "Bot service ", null, "has been stopped", cancellationToken: cancellationToken);
                await _cts.CancelAsync();
                applicationLifetime.Exit(0); // Stop service
                break;

            case "/help":
                var logSize = Settings.LogSize;
                
                var privateHelpText =
                    $"Available commands:{Environment.NewLine}" +
                    $"/log - {string.Format(Resource.Show_the_last_actions, logSize)}{Environment.NewLine}" +
                    $"/engage - {Resource.Start}{Environment.NewLine}" +
                    $"/disengage - {Resource.Pause}{Environment.NewLine}" +
                    $"/restart_service - {Resource.Restarting}{Environment.NewLine}" +
                    $"/exit - {Resource.Stop}{Environment.NewLine}" +
                    $"/help - {Resource.ShowHelp}";

                await client.SendTextMessageAsync(message.Chat.Id, privateHelpText, cancellationToken: cancellationToken);
                break;

            default:
                await client.SendTextMessageAsync(message.Chat.Id, Resource.UnknownCommand, cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task ProcessGroupMessageAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.Entities?.Any(e => e.Type == MessageEntityType.BotCommand) == true)
        {
            await ProcessGroupCommandAsync(client, message, cancellationToken);
        }
        else
        {
            await HandleSpamAsync(client, message, cancellationToken);
        }
    }

    private async Task ProcessGroupCommandAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup || message.From == null)
        {
            return;
        }

        var status = await GetChatMemberStatus(client, message.Chat, message.From.Id, cancellationToken);

        if (status is not ChatMemberStatus.Administrator and not ChatMemberStatus.Creator)
        {
            return;
        }

        if ((status == ChatMemberStatus.Creator && message.From.Id == ownerId) || await CheckGroupOwnership(client, message.Chat.Id, cancellationToken))
        {
            var commandEntity = message.Entities!.First(e => e.Type == MessageEntityType.BotCommand);
            var commandText = message.Text!.Substring(commandEntity.Offset, commandEntity.Length);
            var args = message.Text[(commandEntity.Offset + commandEntity.Length)..].Trim();

            switch (commandText.Split('@')[0])
            {
                case "/ban":
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.BanUsers), true);
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.UseMute), false);

                    await client.SendTextMessageAsync(message.Chat.Id, Resource.users_will_be_banned, cancellationToken: cancellationToken);
                    break;

                case "/no_restrict":
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.BanUsers), false);
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.UseMute), false);

                    await client.SendTextMessageAsync(message.Chat.Id, Resource.users_will_not_be_restricted, cancellationToken: cancellationToken);
                    break;

                case "/mute":
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.UseMute), true);
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.BanUsers), false);

                    await client.SendTextMessageAsync(message.Chat.Id, Resource.users_will_be_muted, cancellationToken: cancellationToken);
                    break;

                case "/set_spam_time":
                    if (byte.TryParse(args, out var seconds) && seconds > 0)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.SpamTimeWindow), TimeSpan.FromSeconds(seconds));

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
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.RestrictionDuration), (TimeSpan?)null);

                        await client.SendTextMessageAsync(message.Chat.Id, Resource.Restriction_forever, cancellationToken: cancellationToken);
                    }
                    else if (uint.TryParse(args, out var restrictionMinutes) && restrictionMinutes > 0)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.RestrictionDuration), TimeSpan.FromMinutes(restrictionMinutes));

                        await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.Restriction_duration, restrictionMinutes), cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await client.SendTextMessageAsync(message.Chat.Id, Resource.Invalid_restriction_duration, cancellationToken: cancellationToken);
                    }

                    break;

                case "/silent":
                    bool silentMode;
                    lock (Settings)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.SilentMode), !Settings.GetGroupSettings(message.Chat.Id).SilentMode);
                        silentMode = Settings.GetGroupSettings(message.Chat.Id).SilentMode;
                    }

                    await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.Silent_mode, silentMode ? "enabled" : "disabled"), cancellationToken: cancellationToken);
                    break;

                case "/help":
                    var helpText =
                        $"Available commands:{Environment.NewLine}" +
                        $"/ban - {Resource.Enable_banning}{Environment.NewLine}" +
                        $"/no_restrict - {Resource.Disable_any_restricting}{Environment.NewLine}" +
                        $"/mute - {Resource.Enable_muting}{Environment.NewLine}" +
                        $"/set_spam_time <seconds> - {Resource.Set_the_spam_time}{Environment.NewLine}" +
                        $"/set_restriction_time <minutes or 0> - {Resource.Set_the_restriction_duration}{Environment.NewLine}" +
                        $"/silent - {Resource.Toggle_silent}{Environment.NewLine}" +
                        $"/help - Show this help message";
                    await client.SendTextMessageAsync(message.Chat.Id, helpText, cancellationToken: cancellationToken);
                    break;

                default:
                    await client.SendTextMessageAsync(message.Chat.Id, Resource.UnknownCommand, cancellationToken: cancellationToken);
                    break;
            }
        }
    }

    private async Task HandleSpamAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var spamTimeWindow = Settings.GetGroupSettings(message.Chat.Id).SpamTimeWindow;
        var engaged = Settings.Engaged;

        if (!engaged || message.From == null)
        {
            return;
        }

        if (!IsReplyToLinkedChannelPost(message))
        {
            return;
        }

        if (message.Entities?.Any(e => e.Type is MessageEntityType.Url or MessageEntityType.TextLink) is true)
        {
            var status = await GetChatMemberStatus(client, message.Chat, message.From.Id, cancellationToken);
            if (status is not ChatMemberStatus.Administrator and not ChatMemberStatus.Creator and not ChatMemberStatus.Member)
            {
                await ElaborateSpam(client, message, cancellationToken);

                if (!Settings.GetGroupSettings(message.Chat.Id).SilentMode)
                {
                        await client.SendTextMessageAsync(
                            message.Chat.Id,
                            "Non-group members are not allowed to post links.",
                            disableNotification: true,
                            replyToMessageId: message.ReplyToMessage?.MessageId,
                            allowSendingWithoutReply: true,
                            cancellationToken: cancellationToken);
                }

                return;
            }
        }

        if (message.Date - message.ReplyToMessage!.Date < spamTimeWindow)
        {
            await ElaborateSpam(client, message, cancellationToken);
        }
    }

    private async Task ElaborateSpam(TTBC.ThrottledTelegramBotClient client, Message  message, CancellationToken cancellationToken)
    {
        var restrictionDuration = Settings.GetGroupSettings(message.Chat.Id).RestrictionDuration;
        var banUsers = Settings.GetGroupSettings(message.Chat.Id).BanUsers;
        var useMute = Settings.GetGroupSettings(message.Chat.Id).UseMute;
        var silentMode = Settings.GetGroupSettings(message.Chat.Id).SilentMode;

        var user = $"{message.From!.FirstName} {message.From.LastName} @{message.From.Username} ({message.From.Id}){(message.From.IsBot ? " (bot)" : string.Empty)}";

        await client.DeleteMessageAsync(message.Chat.Id, message.MessageId, cancellationToken);

        var actionTaken = string.Empty;

        if (message.From.Id == UserAsTheChannelId && message.SenderChat?.Id is { } senderChatId && (banUsers || useMute))
        {
            var chat = await client.GetChatAsync(senderChatId, cancellationToken);
            user = $"@{chat.Username} ({chat.Id}) (channel)";
            await client.BanChatSenderChatAsync(message.Chat.Id, senderChatId, cancellationToken: cancellationToken);
            actionTaken = "muted";
        }
        else
        {
            if (banUsers)
            {
                await client.BanChatMemberAsync(
                    message.Chat.Id,
                    message.From.Id,
                    restrictionDuration.HasValue ? DateTime.UtcNow + restrictionDuration.Value : null,
                    revokeMessages: false,
                    cancellationToken);
                actionTaken = "banned";
            }
            else if (useMute)
            {
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

                actionTaken = "muted";
            }
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

    private async Task<bool> CheckGroupOwnership(TTBC.ThrottledTelegramBotClient client, long chatId, CancellationToken cancellationToken)
    {
        if (ownerId.HasValue)
        {
            var administrators = await client.GetChatAdministratorsAsync(chatId, cancellationToken: cancellationToken);
            if (administrators.Length == 0)
            {
                throw new RequestException(Resource.not_an_Admin);
            }

            if (administrators.FirstOrDefault(admin => admin.Status == ChatMemberStatus.Creator)?.User.Id != ownerId)
            {
                await client.SendTextMessageAsync(
                    chatId,
                    Resource.chat_does_not_belong_owner,
                    disableNotification: true,
                    cancellationToken: cancellationToken);

                await client.LeaveChatAsync(chatId, cancellationToken: cancellationToken);
                return false;
            }
        }
        return true;
    }

    private async Task<ChatMemberStatus> GetChatMemberStatus(TTBC.ThrottledTelegramBotClient client, Chat chat, long userId, CancellationToken cancellationToken)
    {
        var chatMember = await client.GetChatMemberAsync(chat.Id, userId, cancellationToken);
        var isAdmin = chatMember is ChatMemberAdministrator { CanRestrictMembers: true };
        var isOwner = chatMember is ChatMemberOwner;
        var isGroupBot = chatMember.User.Id == GroupAnonymousAdminId;
        var isMember = chatMember is ChatMemberMember;
        
        if (isOwner)
        {
            return ChatMemberStatus.Creator;
        }

        if (isAdmin || isGroupBot)
        {
            return ChatMemberStatus.Administrator;
        }

        if (isMember)
        {
            return ChatMemberStatus.Member;
        }

        return ChatMemberStatus.Left;
    }

    private static string PrintApiError(ApiRequestException ex)
    {
        StringBuilder sb = new();

        sb.Append($"ErrorCode: {ex.ErrorCode}").Append(" | ");
        if (ex.Parameters?.MigrateToChatId != null)
        {
            sb.AppendLine($"MigrateToChatId: {ex.Parameters.MigrateToChatId}").Append(" | ");
        }

        if (ex.Parameters?.RetryAfter != null)
        {
            sb.AppendLine($"RetryAfter: {ex.Parameters.RetryAfter}").Append(" | ");
        }

        sb.AppendLine(ex.Message).Append(" | ");

        return $"API Error: {Environment.NewLine}{sb}";
    }

    private static bool IsReplyToLinkedChannelPost(Message message)
    {
        return message.ReplyToMessage?.From?.Id == GenericTelegramId;
    }
}