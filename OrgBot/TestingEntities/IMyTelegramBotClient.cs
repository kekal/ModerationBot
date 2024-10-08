using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot.TestingEntities;

/// <inheritdoc />
public interface IMyTelegramBotClient : ITelegramBotClient
{
    /// <inheritdoc cref="TelegramBotClientExtensions.GetMeAsync"/>
    Task<User> GetMeAsync(CancellationToken cancellationToken = default);
    
    /// <inheritdoc cref="TelegramBotClientExtensions.SetMyCommandsAsync"/>
    Task SetMyCommandsAsync(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.SendContactAsync"/>
    Task<Message> SendContactAsync(ChatId chatId, string phoneNumber, string firstName, int? messageThreadId = default, string? lastName = default, string? vCard = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.LeaveChatAsync"/>
    Task LeaveChatAsync(ChatId chatId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.DeleteMessageAsync"/>
    Task DeleteMessageAsync(ChatId chatId, int messageId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.BanChatSenderChatAsync"/>
    Task BanChatSenderChatAsync(ChatId chatId, long senderChatId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.BanChatMemberAsync"/>
    Task BanChatMemberAsync(ChatId chatId, long userId, DateTime? untilDate = null, bool revokeMessages = false, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.RestrictChatMemberAsync"/>
    Task RestrictChatMemberAsync(ChatId chatId, long userId, ChatPermissions permissions, bool? useIndependentChatPermissions = default, DateTime? untilDate = default, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatAdministratorsAsync"/>
    Task<ChatMember[]> GetChatAdministratorsAsync(ChatId chatId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatAsync"/>
    Task<Chat> GetChatAsync(ChatId chatId, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.GetUpdatesAsync"/>
    Task<Update[]> GetUpdatesAsync(int? offset, int? limit, int? timeout, IEnumerable<UpdateType>? allowedUpdates = default, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.SendTextMessageAsync"/>
    Task<Message> SendTextMessageAsync(ChatId chatId, string text, int? messageThreadId = default, ParseMode? parseMode = default, IEnumerable<MessageEntity>? entities = default, bool? disableWebPagePreview = default, bool? disableNotification = default, bool? protectContent = default, int? replyToMessageId = default, bool? allowSendingWithoutReply = default, IReplyMarkup? replyMarkup = default, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TelegramBotClientExtensions.GetChatMemberAsync"/>
    Task<ChatMember> GetChatMemberAsync(ChatId chatId, long userId, CancellationToken cancellationToken = default);
}