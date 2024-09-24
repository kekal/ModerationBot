using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot;

public class TelegramBotWrapper : IMyTelegramBotClient
{
    private readonly ITelegramBotClient _client;

    public TelegramBotWrapper(string botToken)
    {
        _client = new TelegramBotClient(botToken)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }


    public async Task<TResponse> MakeRequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = new CancellationToken())
    {
        return await _client.MakeRequestAsync(request, cancellationToken);
    }

    public async Task<bool> TestApiAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        return await _client.TestApiAsync(cancellationToken);
    }

    public async Task DownloadFileAsync(string filePath, Stream destination, CancellationToken cancellationToken = new CancellationToken())
    {
        await _client.DownloadFileAsync(filePath, destination, cancellationToken);
    }

    public bool LocalBotServer => _client.LocalBotServer;

    public long? BotId => _client.BotId;

    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    public IExceptionParser ExceptionsParser
    {
        get => _client.ExceptionsParser;
        set => _client.ExceptionsParser = value;
    }

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
    {
        add => _client.OnMakingApiRequest += value;
        remove => _client.OnMakingApiRequest -= value;
    }

    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
    {
        add => _client.OnApiResponseReceived += value;
        remove => _client.OnApiResponseReceived -= value;
    }

    public async Task<User> GetMeAsync(CancellationToken cancellationToken = default)
    {
        return await _client.GetMeAsync(cancellationToken);
    }

    public async Task SetMyCommandsAsync(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default, CancellationToken cancellationToken = default)
    {
        await _client.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken);
    }

    public async Task<Message> SendContactAsync(ChatId chatId, string phoneNumber, string firstName, int? messageThreadId = default, string? lastName = default, string? vCard = default,
        bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default,
        IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default)
    {
        return await _client.SendContactAsync(chatId, phoneNumber, firstName, messageThreadId, lastName, vCard, disableNotification, protectContent, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken);
    }

    public async Task LeaveChatAsync(ChatId chatId, CancellationToken cancellationToken = default)
    {
        await _client.LeaveChatAsync(chatId, cancellationToken);
    }

    public async Task DeleteMessageAsync(ChatId chatId, int messageId, CancellationToken cancellationToken = default)
    {
        await _client.DeleteMessageAsync(chatId, messageId, cancellationToken);
    }

    public async Task BanChatSenderChatAsync(ChatId chatId, long senderChatId, CancellationToken cancellationToken = default)
    {
        await _client.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken);
    }

    public async Task BanChatMemberAsync(ChatId chatId, long userId, DateTime? untilDate = null, bool revokeMessages = false, CancellationToken cancellationToken = default)
    {
        await _client.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken);
    }

    public async Task RestrictChatMemberAsync(ChatId chatId, long userId, ChatPermissions permissions, bool? useIndependentChatPermissions = default, DateTime? untilDate = default,
        CancellationToken cancellationToken = default)
    {
        await _client.RestrictChatMemberAsync(chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken);
    }

    public async Task<ChatMember[]> GetChatAdministratorsAsync(ChatId chatId, CancellationToken cancellationToken = default)
    {
        return await _client.GetChatAdministratorsAsync(chatId, cancellationToken);
    }

    public async Task<Chat> GetChatAsync(ChatId chatId, CancellationToken cancellationToken = default)
    {
        return await _client.GetChatAsync(chatId, cancellationToken);
    }

    public async Task<Update[]> GetUpdatesAsync(int? offset, int? limit, int? timeout, IEnumerable<UpdateType>? allowedUpdates = default, CancellationToken cancellationToken = default)
    {
        return await _client.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken);
    }

    public async Task<Message> SendTextMessageAsync(ChatId chatId, string text, int? messageThreadId = default, ParseMode? parseMode = default, IEnumerable<MessageEntity>? entities = default,
        bool? disableWebPagePreview = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default,
        bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default)
    {
        return await _client.SendTextMessageAsync(chatId, text, messageThreadId, parseMode, entities, disableWebPagePreview, disableNotification, protectContent, replyToMessageId, allowSendingWithoutReply, replyMarkup, cancellationToken);
    }

    public async Task<ChatMember> GetChatMemberAsync(ChatId chatId, long userId, CancellationToken cancellationToken = default)
    {
        return await _client.GetChatMemberAsync(chatId, userId, cancellationToken);
    }
}