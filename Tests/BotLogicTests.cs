using Moq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace OrgBot.Tests;

[TestClass]
public class BotLogicTests
{
    private BotLogic _botLogic = null!;
    private Mock<IMyTelegramBotClient> _mockClient = null!;
    private ThrottledTelegramBotClient _throttledClient = null!;
    private const long OwnerId = 123456789;
    private const string BotToken = "test_bot_token";

    [TestInitialize]
    public void Setup()
    {
        _mockClient = new Mock<IMyTelegramBotClient>();
        _throttledClient = new ThrottledTelegramBotClient(_mockClient.Object, TimeSpan.FromMilliseconds(100));
        _botLogic = new BotLogic(BotToken, OwnerId);
        _botLogic.SetupLogger();
    }

    [TestMethod]
    public async Task SetCommandsAsync_ShouldSetCommands()
    {
        // Arrange
        _mockClient.Setup(c => c.SetMyCommandsAsync(
                It.IsAny<IEnumerable<BotCommand>>(),
                It.IsAny<BotCommandScope>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await BotLogic.SetCommandsAsync(_throttledClient);

        // Assert
        _mockClient.Verify(c => c.SetMyCommandsAsync(
            It.IsAny<IEnumerable<BotCommand>>(),
            It.Is<BotCommandScopeAllGroupChats>(scope => true),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClient.Verify(c => c.SetMyCommandsAsync(
            It.IsAny<IEnumerable<BotCommand>>(),
            It.Is<BotCommandScopeAllPrivateChats>(scope => true),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateMessageFromOwner_ShouldProcessPrivateMessage()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 1000, Type = ChatType.Private },
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                It.IsAny<string>(),
                null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { MessageId = 2 });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            It.Is<string>(s => s.Contains("Available commands")),
            null, null, null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessPrivateMessageAsync_FromNonOwner_ShouldRejectUser()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = 999999, FirstName = "User" },
            Chat = new Chat { Id = 1000, Type = ChatType.Private },
            Text = "Hello"
        };

        _mockClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                "You are not the bot owner.",
                null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { MessageId = 2 });

        _mockClient.Setup(c => c.LeaveChatAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _botLogic.ProcessPrivateMessageAsync(_throttledClient, message, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            "You are not the bot owner.",
            null, null, null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClient.Verify(c => c.LeaveChatAsync(
            message.Chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessPrivateCommandAsync_LogCommand_ShouldSendActionLog()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 1000, Type = ChatType.Private },
            Text = "/log",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };

        // Add some logs
        _botLogic.ActionLog.Add("Action 1");
        _botLogic.ActionLog.Add("Action 2");

        _mockClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                It.Is<string>(s => s.Contains("Action 1") && s.Contains("Action 2")),
                null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { MessageId = 2 });

        // Act
        await _botLogic.ProcessPrivateCommandAsync(_throttledClient, message, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            It.Is<string>(s => s.Contains("Action 1") && s.Contains("Action 2")),
            null, null, null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessGroupMessageAsync_SpamMessage_ShouldHandleSpam()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = 12345, FirstName = "Spammer" },
            Chat = new Chat { Id = -1000, Type = ChatType.Supergroup },
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                Date = DateTime.UtcNow.AddSeconds(-5),
                SenderChat = new Chat { Id = -2000, Type = ChatType.Channel }
            }
        };

        _mockClient.Setup(c => c.GetChatAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chat { Id = message.Chat.Id, LinkedChatId = -2000 });

        _mockClient.Setup(c => c.DeleteMessageAsync(
                message.Chat.Id,
                message.MessageId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClient.Setup(c => c.RestrictChatMemberAsync(
                message.Chat.Id,
                message.From.Id,
                new ChatPermissions { CanSendMessages = false },
                false,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _botLogic.ProcessGroupMessageAsync(_throttledClient, message, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.DeleteMessageAsync(
            message.Chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClient.Verify(c => c.RestrictChatMemberAsync(
            message.Chat.Id,
            message.From.Id,
            It.Is<ChatPermissions>(p => p.CanSendMessages == false),
            null,
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessGroupCommandAsync_BanCommand_ShouldUpdateSettings()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = 12345 },
            Chat = new Chat { Id = -1000, Type = ChatType.Supergroup },
            Text = "/ban",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };

        _mockClient.Setup(c => c.GetChatMemberAsync(
                message.Chat.Id,
                message.From.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator { User = message.From, CanRestrictMembers = true });

        _mockClient.Setup(c => c.GetChatAdministratorsAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMemberOwner { User = new User { Id = OwnerId } }]);

        _mockClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                "Spamming users will be banned.",
                null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { MessageId = 2 });

        // Act
        await _botLogic.ProcessGroupCommandAsync(_throttledClient, message, CancellationToken.None);

        // Assert
        Assert.IsTrue(_botLogic.Settings.BanUsers);
        Assert.IsFalse(_botLogic.Settings.UseMute);

        _mockClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            "Spamming users will be banned.",
            null, null, null, null, null, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleSpamAsync_UserIsBanned_WhenSpamDetected()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = 12345, FirstName = "Spammer" },
            Chat = new Chat { Id = -1000, Type = ChatType.Supergroup },
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                Date = DateTime.UtcNow.AddSeconds(-5),
                SenderChat = new Chat { Id = -2000, Type = ChatType.Channel }
            }
        };

        _botLogic.Settings.BanUsers = true;
        _botLogic.Settings.UseMute = false;

        _mockClient.Setup(c => c.GetChatAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chat { Id = message.Chat.Id, LinkedChatId = -2000 });

        _mockClient.Setup(c => c.DeleteMessageAsync(
                message.Chat.Id,
                message.MessageId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClient.Setup(c => c.BanChatMemberAsync(
                message.Chat.Id,
                message.From.Id,
                It.IsAny<DateTime?>(),
                false,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _botLogic.HandleSpamAsync(_throttledClient, message, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.DeleteMessageAsync(
            message.Chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClient.Verify(c => c.BanChatMemberAsync(
            message.Chat.Id,
            message.From.Id,
            It.IsAny<DateTime?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CheckBotOwner_ChatNotOwnedByBotOwner_ShouldLeaveChat()
    {
        // Arrange
        var message = new Message
        {
            Chat = new Chat { Id = -1000 }
        };

        _mockClient.Setup(c => c.GetChatAdministratorsAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMemberOwner { User = new User { Id = 999999 } }]);

        _mockClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                "This chat does not belong to bot owner.",
                null, null, null, null, true, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { MessageId = 1 });

        _mockClient.Setup(c => c.LeaveChatAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _botLogic.CheckBotOwner(_throttledClient, message, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            "This chat does not belong to bot owner.",
            null, null, null, null, true, null, null, null, null,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockClient.Verify(c => c.LeaveChatAsync(
            message.Chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IsUserAdminAsync_UserIsAdmin_ReturnsTrue()
    {
        // Arrange
        var chat = new Chat { Id = -1000 };
        const int userId = 12345;

        _mockClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator { User = new User { Id = userId }, CanRestrictMembers = true });

        // Act
        var result = await _botLogic.IsUserAdminAsync(_throttledClient, chat, userId, CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsReplyToLinkedChannelPost_MessageIsReplyToLinkedChannelPost_ReturnsTrue()
    {
        // Arrange
        var message = new Message
        {
            Chat = new Chat { Id = -1000 },
            ReplyToMessage = new Message
            {
                SenderChat = new Chat { Id = -2000, Type = ChatType.Channel }
            }
        };

        _mockClient.Setup(c => c.GetChatAsync(
                message.Chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chat { Id = message.Chat.Id, LinkedChatId = -2000 });

        // Act
        var result = await _botLogic.IsReplyToLinkedChannelPost(_throttledClient, message, CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
    }

}