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
    private TelegramLogger Logger { get; set; } = null!;
    private readonly CancellationTokenSource _cts = new();


    public async Task RunAsync()
    {
        TTBC.ThrottledTelegramBotClient? client;

        try
        {
            using (client = new TTBC.ThrottledTelegramBotClient(new TTBCT.TelegramBotWrapper(botToken), TimeSpan.FromSeconds(ThrottlingTimeout)))
            {
                SetupLogger(async notification =>
                {
                    if (ownerId != null)
                    {
                        await client.SendTextMessageAsync(ownerId, notification, allowSendingWithoutReply: true, cancellationToken: _cts.Token);
                    }
                });

                if (Logger == null)
                {
                    Console.Error.WriteLine("Failed to setup logging.");
                    applicationLifetime.Exit(1);
                }

                await SetCommandsAsync(client);

                var me = await client.GetMeAsync(_cts.Token);
                await Logger!.LogInformationAsync(string.Format(Resource.Welcome, me.Username, me.Id));

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
            await Logger.LogErrorAsync(PrintApiError(ex), false);
            if (ex is { ErrorCode: 429, Parameters.RetryAfter: { } retryAfter })
            {
                await Logger.LogErrorAsync(string.Format(Resource.API_rate_limit_exceeded, retryAfter), false);
                Console.WriteLine(Resource.Too_many_requests, retryAfter);
                await Task.Delay(TimeSpan.FromSeconds(retryAfter + 5));
            }

            applicationLifetime.Exit(24);
        }
        catch (Exception e)
        {
            var error = string.Join(" | ", e.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            await Logger.LogErrorAsync(error);

            await Task.Delay(TimeSpan.FromMinutes(1));

            applicationLifetime.Exit(2);
        }
    }

    internal void SetupLogger(Action<string>? notification)
    {
        lock (Settings)
        {
            Logger = new TelegramLogger(Settings.LogSize, notification);
        }
    }

    internal async Task SetCommandsAsync(TTBC.ThrottledTelegramBotClient client)
    {
        var logSize = Settings.LogSize;

        var groupCommands = new[]
        {
            new BotCommand { Command = "ban", Description = Resource.Enable_banning },
            new BotCommand { Command = "throttle_user", Description = Resource.throttled_user },
            new BotCommand { Command = "free_user", Description = Resource.free_user },
            new BotCommand { Command = "no_restrict", Description = Resource.Disable_any_restricting },
            new BotCommand { Command = "clean_non_group_url_messages", Description = Resource.Clean_non_group_url },
            new BotCommand { Command = "mute", Description = Resource.Enable_muting },
            new BotCommand { Command = "set_spam_time", Description = Resource.Set_the_spam_time },
            new BotCommand { Command = "set_restriction_time", Description = Resource.Set_the_restriction_duration },
            new BotCommand { Command = "silent", Description = Resource.Toggle_silent },
            new BotCommand { Command = "joining", Description = Resource.joining_description },
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
            if (ex is { ErrorCode: 429, Parameters.RetryAfter: { } retryAfter })
            {
                await Logger.LogErrorAsync(PrintApiError(ex), false);

                await Logger.LogErrorAsync(string.Format(Resource.API_rate_limit_exceeded, retryAfter + 10), false);
                await Task.Delay((retryAfter + 10) * 1000, cancellationToken);
            }
            else
            {
                var error = string.Join(" | ", ex.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                await Logger.LogErrorAsync(error, false);
            }
        }
        catch (Exception e)
        {
            var error = string.Join(" | ", e.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            await Logger.LogErrorAsync(string.Format(Resource.Failed_to_handle_message, error));
        }
    }

    private async Task ProcessPrivateMessageAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (ownerId.HasValue && message.From?.Id != ownerId)
        {
            await client.SendTextMessageAsync(message.Chat.Id, Resource.not_bot_owner, cancellationToken: cancellationToken);
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
                var logContent = string.Join($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", Logger.GetLog());
                await client.SendTextMessageAsync(message.Chat.Id, string.IsNullOrWhiteSpace(logContent.Trim()) ? "No actions logged yet." : logContent, cancellationToken: cancellationToken);

                break;

            case "/engage":
                Settings.Engaged = true;
                await Logger.LogInformationAsync(Resource.Bot_is_engaged);

                break;

            case "/disengage":
                Settings.Engaged = false;
                await Logger.LogInformationAsync(Resource.Bot_disengaged);

                break;

            case "/restart_service":
                await Logger.LogInformationAsync(Resource.Restarting);
                await _cts.CancelAsync();
                applicationLifetime.Exit(42);  // Upgrade

                break;

            case "/exit":
                await Logger.LogInformationAsync(Resource.shutting_down);
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
                    await Logger.LogInformationAsync(string.Format(Resource.users_will_be_banned_info, StripChatId(message.Chat.Id)));

                    break;

                case "/no_restrict":
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.BanUsers), false);
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.UseMute), false);

                    await client.SendTextMessageAsync(message.Chat.Id, Resource.users_will_not_be_restricted, cancellationToken: cancellationToken);
                    await Logger.LogInformationAsync(string.Format(Resource.users_will_not_be_restricted_info, StripChatId(message.Chat.Id)));

                    break;

                case "/clean_non_group_url_messages":
                    bool cleanNonGroupUrl;
                    lock (Settings)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.CleanNonGroupUrl), !Settings.GetGroupSettings(message.Chat.Id).CleanNonGroupUrl);
                        cleanNonGroupUrl = Settings.GetGroupSettings(message.Chat.Id).CleanNonGroupUrl;
                    }

                    var clean = string.Format(Resource.Clean_non_group_url_mode, (cleanNonGroupUrl ? "enabled" : "disabled"));
                    await client.SendTextMessageAsync(message.Chat.Id, clean, cancellationToken: cancellationToken);
                    await Logger.LogInformationAsync(string.Format(Resource.Clean_non_group_url_mode_info, clean, StripChatId(message.Chat.Id)));

                    break;

                case "/throttle_user":
                    if (ulong.TryParse(args, out var sec) && sec >= 10)
                    {
                        if (message.ReplyToMessage?.From?.Id is { } userId)
                        {
                            var chatMember = await client.GetChatMemberAsync(message.Chat.Id, message.From.Id, cancellationToken);
                            if (GetMemberPermissions(chatMember) is not { } permissions)
                            {
                                var chat = await client.GetChatAsync(message.Chat.Id, cancellationToken);
                                permissions = chat.Permissions;
                            }

                            Settings.SetUserState(message.Chat.Id, userId, sec, permissions ?? new ChatPermissions());

                            await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.throttle_notify, userId, sec), cancellationToken: cancellationToken);
                            await Logger.LogInformationAsync(string.Format(Resource.throttle_notify_info, userId, sec, StripChatId(message.Chat.Id)));
                        }
                    }
                    else
                    {
                        await Logger.LogErrorAsync(string.Format(Resource.Invalid_time_specified2, ulong.MaxValue));
                    }

                    break;

                case "/free_user":

                    if (message.ReplyToMessage?.From?.Id is { } userId2)
                    {
                        var state = Settings.GetUserState(message.Chat.Id, userId2);
                        Settings.SetUserState(message.Chat.Id, userId2, 0, state.DefaultPermissions);

                        await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.User_will_not_be_throttled, userId2), cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(string.Format(Resource.User_will_not_be_throttled_info, userId2, StripChatId(message.Chat.Id)));
                    }

                    break;

                case "/mute":
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.UseMute), true);
                    Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.BanUsers), false);

                    await client.SendTextMessageAsync(message.Chat.Id, Resource.users_will_be_muted, cancellationToken: cancellationToken);
                    await Logger.LogInformationAsync(string.Format(Resource.users_will_be_muted_info, StripChatId(message.Chat.Id)));
                    break;

                case "/set_spam_time":
                    if (byte.TryParse(args, out var seconds) && seconds > 0)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.SpamTimeWindow), TimeSpan.FromSeconds(seconds));

                        await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.Spam_time_window_set, seconds), cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(string.Format(Resource.Spam_time_window_set_info, seconds, StripChatId(message.Chat.Id)));
                    }
                    else
                    {
                        await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.Invalid_time_specified, byte.MaxValue), cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(string.Format(Resource.Invalid_time_specified_info, StripChatId(message.Chat.Id)));
                    }

                    break;

                case "/set_restriction_time":
                    if (args.Trim().Equals("0", StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.RestrictionDuration), (TimeSpan?)null);

                        await client.SendTextMessageAsync(message.Chat.Id, Resource.Restriction_forever, cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(string.Format(Resource.Restriction_forever_info, StripChatId(message.Chat.Id)));
                    }
                    else if (uint.TryParse(args, out var restrictionMinutes) && restrictionMinutes > 0)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.RestrictionDuration), TimeSpan.FromMinutes(restrictionMinutes));

                        await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.Restriction_duration, restrictionMinutes), cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(string.Format(Resource.Restriction_duration_info, restrictionMinutes, StripChatId(message.Chat.Id)));
                    }
                    else
                    {
                        await client.SendTextMessageAsync(message.Chat.Id, Resource.Invalid_restriction_duration, cancellationToken: cancellationToken);
                        await Logger.LogInformationAsync(Resource.Invalid_restriction_duration);
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
                    await Logger.LogInformationAsync(string.Format(Resource.Silent_mode_info, silentMode ? "enabled" : "disabled", StripChatId(message.Chat.Id)));
                    break;

                case "/joining":
                    bool disableJoining;
                    lock (Settings)
                    {
                        Settings.SetGroupSettings(message.Chat.Id, nameof(GroupSettings.DisableJoining), !Settings.GetGroupSettings(message.Chat.Id).DisableJoining);
                        disableJoining = Settings.GetGroupSettings(message.Chat.Id).DisableJoining;
                    }

                    await client.SendTextMessageAsync(message.Chat.Id, string.Format(Resource.join_disable, !disableJoining ? "enabled" : "disabled"), cancellationToken: cancellationToken);
                    await Logger.LogInformationAsync(string.Format(Resource.join_disable_info, !disableJoining ? "enabled" : "disabled", StripChatId(message.Chat.Id)));
                    break;

                case "/help":
                    var helpText =
                        $"Available commands:{Environment.NewLine}" +
                        $"/ban - {Resource.Enable_banning}{Environment.NewLine}" +
                        $"/throttle_user - {Resource.throttled_user}{Environment.NewLine}" +
                        $"/free_user - {Resource.free_user}{Environment.NewLine}" +
                        $"/no_restrict - {Resource.Disable_any_restricting}{Environment.NewLine}" +
                        $"/clean_non_group_url_messages - {Resource.Clean_non_group_url}{Environment.NewLine}" +
                        $"/mute - {Resource.Enable_muting}{Environment.NewLine}" +
                        $"/set_spam_time <seconds> - {Resource.Set_the_spam_time}{Environment.NewLine}" +
                        $"/set_restriction_time <minutes or 0> - {Resource.Set_the_restriction_duration}{Environment.NewLine}" +
                        $"/silent - {Resource.Toggle_silent}{Environment.NewLine}" +
                        $"/joining - {Resource.joining_description}{Environment.NewLine}" +
                        $"/help - {Resource.Show_this_help_message}";
                    await client.SendTextMessageAsync(message.Chat.Id, helpText, cancellationToken: cancellationToken);
                    break;

                default:
                    await client.SendTextMessageAsync(message.Chat.Id, Resource.UnknownCommand, cancellationToken: cancellationToken);
                    await Logger.LogInformationAsync(string.Format(Resource.UnknownCommand_info, StripChatId(message.Chat.Id)));
                    break;
            }
        }
    }

    private async Task HandleSpamAsync(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var spamTimeWindow = Settings.GetGroupSettings(message.Chat.Id).SpamTimeWindow;
        var engaged = Settings.Engaged;

        if (await DeleteJoin(client, message, cancellationToken))
        {
            return;
        }

        if (!engaged || message.From == null)
        {
            return;
        }

        if (await PerformUserThrottling(client, message, cancellationToken))
        {
            return;
        }

        if (!IsReplyToLinkedChannelPost(message))
        {
            return;
        }

        if (Settings.GetGroupSettings(message.Chat.Id).CleanNonGroupUrl && message.Entities?.Any(e => e.Type is MessageEntityType.Url or MessageEntityType.TextLink) is true)
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

    private async Task<bool> DeleteJoin(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (Settings.GetGroupSettings(message.Chat.Id).DisableJoining && message.Type is MessageType.ChatMembersAdded or MessageType.ChatMemberLeft)
        {
            await client.DeleteMessageAsync(message.Chat.Id, message.MessageId, cancellationToken);

            var user = $"{message.From!.FirstName} {message.From.LastName} @{message.From.Username} ({message.From.Id}){(message.From.IsBot ? " (bot)" : string.Empty)}";

            await Logger.LogInformationAsync($"Deleted join/left message from {user}.");
            return true;
        }

        return false;
    }

    private async Task<bool> PerformUserThrottling(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var user = Settings.GetUserState(message.Chat.Id, message.From?.Id);

        if (message.From != null && user.ThrottleTime >= 10)
        {
            if (message.From.Id == UserAsTheChannelId && message.SenderChat?.Id is { } senderChatId)
            {
                await client.BanChatSenderChatAsync(message.Chat.Id, senderChatId, cancellationToken: cancellationToken);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(user.ThrottleTime), cancellationToken);
                        await client.UnbanChatSenderChatAsync(chatId: message.Chat.Id, senderChatId: message.SenderChat.Id, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await Logger.LogErrorAsync(string.Format(Resource.Error_while_unbanning, ex.Message));
                    }
                }, cancellationToken);

            }
            else
            {
                await MuteUser(client, message, TimeSpan.FromSeconds(user.ThrottleTime), cancellationToken);

                if (user.ThrottleTime < 60)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(user.ThrottleTime), cancellationToken);
                            await UnMuteUser(client, message, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            await Logger.LogInformationAsync(string.Format(Resource.Error_while_unbanning, ex.Message));
                        }
                    }, cancellationToken);
                }
            }

            return true;
        }

        return false;
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
                await MuteUser(client, message, restrictionDuration, cancellationToken);

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

        var chatId = StripChatId(message.Chat.Id);

        var link = message.ReplyToMessage?.MessageId is not null ? $"replay to https://t.me/c/{chatId}/{message.ReplyToMessage?.MessageId}" : $"https://t.me/c/{chatId}/{message.MessageId}";

        await Logger.LogInformationAsync($"{actionTaken} user {user} in chat {message.Chat.Title}. Deleted {link} | >> {message.Text}");
    }

    private static long StripChatId(long chatId)
    {
        var chatIdStr = chatId.ToString();
        long.TryParse(chatIdStr.StartsWith("-100") ? chatIdStr["-100".Length..] : chatIdStr, out var stripped);

        return stripped;
    }

    private static async Task MuteUser(TTBC.ThrottledTelegramBotClient client, Message message, TimeSpan? restrictionDuration, CancellationToken cancellationToken)
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
            userId: message.From!.Id,
            permissions,
            untilDate: restrictionDuration.HasValue ? DateTime.UtcNow + restrictionDuration : null,
            cancellationToken: cancellationToken);
    }

    private async Task UnMuteUser(TTBC.ThrottledTelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        var state = Settings.GetUserState(message.Chat.Id, message.From!.Id);
        
        await client.RestrictChatMemberAsync(
            message.Chat.Id,
            userId: message.From!.Id,
            state.DefaultPermissions,
            cancellationToken: cancellationToken);
    }

    private static ChatPermissions? GetMemberPermissions(ChatMember member)
    {
        if (member is ChatMemberRestricted restrictedMember)
        {
            return new ChatPermissions
            {
                CanAddWebPagePreviews = restrictedMember.CanAddWebPagePreviews,
                CanChangeInfo = restrictedMember.CanChangeInfo,
                CanInviteUsers = restrictedMember.CanInviteUsers,
                CanManageTopics = restrictedMember.CanManageTopics,
                CanPinMessages = restrictedMember.CanPinMessages,
                CanSendAudios = restrictedMember.CanSendMessages,
                CanSendDocuments = restrictedMember.CanSendDocuments,
                CanSendMessages = restrictedMember.CanSendMessages,
                CanSendOtherMessages = restrictedMember.CanSendOtherMessages,
                CanSendPhotos = restrictedMember.CanSendPhotos,
                CanSendPolls = restrictedMember.CanSendPolls,
                CanSendVideoNotes = restrictedMember.CanSendVideoNotes,
                CanSendVideos = restrictedMember.CanSendVideos,
                CanSendVoiceNotes = restrictedMember.CanSendMessages,
            };
        }

        return null;
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