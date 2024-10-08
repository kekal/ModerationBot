using OrgBot.TestingEntities;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot;

/// <summary> A client interface to use the Telegram Bot API with specified throttling delay </summary>
public sealed class ThrottledTelegramBotClient(IMyTelegramBotClient client, TimeSpan delayBetweenRequests) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    public long? BotId => client.BotId;

    public async Task<Update[]> GetUpdatesAsync(int? offset, int? limit, int? timeout, IEnumerable<UpdateType>? allowedUpdates = default, CancellationToken cancellationToken = default)
    {
        return await (client.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));
    }

    private async Task<TResult> ExecuteWithDelayAsync<TResult>(Func<Task<TResult>> apiCall)
    {
        await _semaphore.WaitAsync();
        try
        {
            var result = await apiCall();
#if DEBUG
            Console.WriteLine($@"{apiCall.GetMethodInfo().Name.Replace('<', '|').Replace('>', '|').Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()} called");
#endif
            await Task.Delay(delayBetweenRequests);
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ExecuteWithDelayAsync(Func<Task?> apiCall)
    {
        await _semaphore.WaitAsync();
        try
        {
            await apiCall()!;
#if DEBUG
            Console.WriteLine($@"{apiCall.GetMethodInfo().Name.Replace('<', '|').Replace('>', '|').Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()} called");
#endif
            await Task.Delay(delayBetweenRequests);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc cref="TelegramBotClientExtensions.GetMeAsync"/>
    public Task<User> GetMeAsync(CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.GetMeAsync(cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc cref="TelegramBotClientExtensions.SendTextMessageAsync"/>
    public Task<Message> SendTextMessageAsync(ChatId chatId, string text, int? messageThreadId = default, ParseMode? parseMode = default, IEnumerable<MessageEntity>? entities = default, bool? disableWebPagePreview = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.SendTextMessageAsync(chatId, text, messageThreadId, parseMode, entities, disableWebPagePreview, disableNotification, protectContent, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc cref="TelegramBotClientExtensions.SetMyCommandsAsync"/>
    public Task SetMyCommandsAsync(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.SendContactAsync"/>
    public Task<Message> SendContactAsync(ChatId chatId, string phoneNumber, string firstName, int? messageThreadId = default, string? lastName = default, string? vCard = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.SendContactAsync(chatId, phoneNumber, firstName, messageThreadId, lastName, vCard, disableNotification, protectContent, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc cref="TelegramBotClientExtensions.LeaveChatAsync"/>
    public Task LeaveChatAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.LeaveChatAsync(chatId, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.DeleteMessageAsync"/>
    public Task DeleteMessageAsync(ChatId chatId, int messageId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.DeleteMessageAsync(chatId, messageId, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.BanChatSenderChatAsync"/>
    public Task BanChatSenderChatAsync(ChatId chatId, long senderChatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.BanChatMemberAsync"/>
    public Task BanChatMemberAsync(ChatId chatId, long userId, DateTime? untilDate = null, bool revokeMessages = false, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.RestrictChatMemberAsync"/>
    public Task RestrictChatMemberAsync(ChatId chatId, long userId, ChatPermissions permissions, bool? useIndependentChatPermissions = default, DateTime? untilDate = default, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.RestrictChatMemberAsync(chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken));

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatAdministratorsAsync"/>
    public Task<ChatMember[]> GetChatAdministratorsAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.GetChatAdministratorsAsync(chatId, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatAsync"/>
    public Task<Chat> GetChatAsync(ChatId chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.GetChatAsync(chatId, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatMemberAsync"/>
    public Task<ChatMember> GetChatMemberAsync(ChatId chatId, long userId, CancellationToken cancellationToken = default) =>
        ExecuteWithDelayAsync(() => client.GetChatMemberAsync(chatId, userId, cancellationToken) ?? throw new InvalidOperationException("Client is no instantiated"));

    /// <inheritdoc />>
    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    ~ThrottledTelegramBotClient()
    {
        Dispose();
    }



}