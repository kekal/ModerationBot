using Moq;
using OrgBot.TestEntities;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot.Tests;

[TestClass]
public class BotLogicTests
{
    private BotLogic _botLogic = null!;
    private Mock<IMyTelegramBotClient> _mockTelegramBotClient = null!;
    private Mock<IApplicationLifetime> _mockApplicationLifetime = null!;
    private ThrottledTelegramBotClient _throttledClient = null!;
    private const long OwnerId = 123456789;
    private const string BotToken = "test_bot_token";

    [TestInitialize]
    public void Setup()
    {
        _mockTelegramBotClient = new Mock<IMyTelegramBotClient>();
        _throttledClient = new ThrottledTelegramBotClient(_mockTelegramBotClient.Object, TimeSpan.FromMilliseconds(100));
        _mockApplicationLifetime = new Mock<IApplicationLifetime>();
        _botLogic = new BotLogic(BotToken, OwnerId, _mockApplicationLifetime.Object);


        _botLogic.SetupLogger();
    }

    [TestMethod]
    public async Task HandleUpdateAsync_OwnerExitCommand_ShouldShutdownBot()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 1000, Type = ChatType.Private },
            Text = "/exit",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                message.Chat.Id,
                Resource.shutting_down,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>())
            , Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendContactAsync(
                OwnerId,
                It.IsAny<string>(),
                "Bot service ",
                default,
                "has been stopped",
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockApplicationLifetime.Verify(a => a.Exit(0), Times.Once);
    }

     [TestMethod]
    public async Task SetCommandsAsync_ShouldSetCommands()
    {
        // Act
        await BotLogic.SetCommandsAsync(_throttledClient);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SetMyCommandsAsync(
            It.IsAny<IEnumerable<BotCommand>>(),
            It.Is<BotCommandScopeAllGroupChats>(scope => true),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.SetMyCommandsAsync(
            It.IsAny<IEnumerable<BotCommand>>(),
            It.Is<BotCommandScopeAllPrivateChats>(scope => true),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [TestMethod]
    public async Task HandleUpdateAsync_PrivateCommandFromNonOwner_ShouldRejectUser()
    {
        // Arrange
        const long nonOwnerId = OwnerId + 1;
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = nonOwnerId, FirstName = "NonOwner" },
            Chat = new Chat { Id = 1001, Type = ChatType.Private },
            Text = "/log",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };
        var update = new Update { Id = 1, Message = message };


        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.not_bot_owner,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.LeaveChatAsync(
            message.Chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupOwnershipCheckFails_ShouldLeaveChat()
    {
        // Arrange
        var chat = new Chat { Id = 3003, Type = ChatType.Group };
        const int botId = 11111111;
        var botUser = new User { Id = botId, IsBot = true };
        var update = new Update
        {
            Id = 1,
            MyChatMember = new ChatMemberUpdated
            {
                Chat = chat,
                From = botUser,
                Date = DateTime.UtcNow,
                OldChatMember = new ChatMemberMember { User = botUser },
                NewChatMember = new ChatMemberAdministrator { User = botUser }
            }
        };

        _mockTelegramBotClient.SetupGet(c => c.BotId).Returns(botId);

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new ChatMemberOwner { User = new User { Id = OwnerId + 1, FirstName = "Admin" } }
                ]
            );


        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                Resource.chat_does_not_belong_owner,
                default,
                default,
                default,
                default,
                true,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTelegramBotClient.Verify(c => c.LeaveChatAsync(
            chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateMessageFromNonOwner_ShouldRejectUser()
    {
        // Arrange
        const long nonOwnerId = OwnerId + 1;
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = nonOwnerId, FirstName = "NonOwner" },
            Chat = new Chat { Id = 4004, Type = ChatType.Private },
            Text = "Hello",
            Entities = null
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.not_bot_owner,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.LeaveChatAsync(
            message.Chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [TestMethod]
    public async Task HandleUpdateAsync_SpamMessageWithSilentMode_ShouldNotSendNotification()
    {
        // Arrange
        lock (_botLogic.Settings)
        {
            _botLogic.Settings.SilentMode = true;
        }

        var chat = new Chat { Id = 5005, Type = ChatType.Group };
        var spammer = new User { Id = 987654, FirstName = "Spammer" };
        var message = new Message
        {
            MessageId = 1,
            From = spammer,
            Chat = chat,
            Text = "Spam message",
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
            chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            It.IsAny<ChatId>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<ParseMode>(),
            It.IsAny<IEnumerable<MessageEntity>>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<IReplyMarkup>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_SilentCommand_ShouldToggleSilentMode()
    {
        // Arrange
        var chat = new Chat { Id = 6006, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var administrator = new ChatMemberAdministrator { User = adminUser, CanRestrictMembers = true };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/silent",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 7 }]
        };
        var update = new Update { Id = 1, Message = message };

        lock (_botLogic.Settings)
        {
            _botLogic.Settings.SilentMode = false;
        }

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    administrator,
                    new ChatMemberOwner { User = new User { Id = OwnerId, FirstName = "Admin" } }
                ]
            );

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(administrator);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.IsTrue(_botLogic.Settings.SilentMode);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                string.Format(Resource.Silent_mode, "enabled"),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [TestMethod]
    public async Task HandleUpdateAsync_SpamMessage_ShouldDeleteAndBanUser()
    {
        // Arrange
        lock (_botLogic.Settings)
        {
            _botLogic.Settings.BanUsers = true;
            _botLogic.Settings.UseMute = false;
            _botLogic.Settings.RestrictionDuration = TimeSpan.FromHours(1);
        }

        var chat = new Chat { Id = 7007, Type = ChatType.Group };
        var spammer = new User { Id = 123456, FirstName = "Spammer" };
        var message = new Message
        {
            MessageId = 1,
            From = spammer,
            Chat = chat,
            Text = "Spam message",
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var update = new Update { Id = 1, Message = message };
        
        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
            chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
            chat.Id,
            spammer.Id,
            It.IsAny<DateTime?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            It.Is<string>(s => s.Contains("has been banned")),
            default,
            default,
            default,
            default,
            true,
            default,
            message.MessageId,
            true,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupBanCommand_ByAdmin_ShouldEnableBanUsers()
    {
        // Arrange
        var chat = new Chat { Id = 8008, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var administrator = new ChatMemberAdministrator { User = adminUser, CanRestrictMembers = true };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/ban",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };
        var update = new Update { Id = 1, Message = message };

        lock (_botLogic.Settings)
        {
            _botLogic.Settings.BanUsers = false;
        }

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    administrator,
                    new ChatMemberOwner { User = new User { Id = OwnerId, FirstName = "Admin" } }
                ]
            );

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(administrator);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.IsTrue(_botLogic.Settings.BanUsers);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            Resource.users_will_be_banned,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateHelpCommand_ShouldSendHelpMessage()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 9009, Type = ChatType.Private },
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };
        var privateHelpText =
            $"Available commands:{Environment.NewLine}" +
            $"/log - {string.Format(Resource.Show_the_last_actions, 30)}{Environment.NewLine}" +
            $"/engage - {Resource.Start}{Environment.NewLine}" +
            $"/disengage - {Resource.Pause}{Environment.NewLine}" +
            $"/restart_service - {Resource.Restarting}{Environment.NewLine}" +
            $"/exit - {Resource.Stop}{Environment.NewLine}" +
            $"/help - {Resource.ShowHelp}";

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            privateHelpText,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_CheckGroupOwnership_NoAdmins_ShouldThrowRequestException()
    {
        // Arrange
        var chat = new Chat { Id = OwnerId, Type = ChatType.Private };
        const int botId = 11111111;
        var botUser = new User { Id = botId, IsBot = true };
        var update = new Update
        {
            Id = 1,
            MyChatMember = new ChatMemberUpdated
            {
                Chat = chat,
                From = botUser,
                Date = DateTime.UtcNow,
                OldChatMember = new ChatMemberMember { User = botUser },
                NewChatMember = new ChatMemberAdministrator { User = botUser }
            }
        };

        _mockTelegramBotClient.SetupGet(c => c.BotId).Returns(botId);

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act

        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            It.Is<string>(s => s.Contains($"Telegram.Bot.Exceptions.RequestException: {Resource.not_an_Admin}")),
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            true,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_SpamMessageFromChannel_ShouldBanChatSenderChat()
    {
        // Arrange
        lock (_botLogic.Settings)
        {
            _botLogic.Settings.BanUsers = true;
            _botLogic.Settings.UseMute = false;
            _botLogic.Settings.RestrictionDuration = TimeSpan.FromHours(1);
        }

        var chat = new Chat { Id = 10002, Type = ChatType.Group };
        var spammer = new User { Id = BotLogic.UserAsTheChannelId, FirstName = "ChannelUser" };
        var senderChat = new Chat { Id = 1234567890, Title = "SpamChannel", Username = "spamChannel" };

        var message = new Message
        {
            MessageId = 1,
            From = spammer,
            SenderChat = senderChat,
            Chat = chat,
            Text = "Spam message from channel",
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatAsync(
                senderChat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderChat);

        _mockTelegramBotClient.Setup(c => c.BanChatSenderChatAsync(
                chat.Id,
                senderChat.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
            chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.BanChatSenderChatAsync(
            chat.Id,
            senderChat.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            It.Is<string>(s => s.Contains("has been muted")),
            default,
            default,
            default,
            default,
            true,
            default,
            message.MessageId,
            true,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateNonCommandMessageFromOwner_ShouldPromptUseCommands()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10003, Type = ChatType.Private },
            Text = "Hello, bot!",
            Entities = null
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.non_command_error,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_MessageFromBot_ShouldIgnoreMessage()
    {
        // Arrange
        const int botId = 11111111;
        var botUser = new User { Id = botId, IsBot = true, FirstName = "Bot" };
        var message = new Message
        {
            MessageId = 1,
            From = botUser,
            Chat = new Chat { Id = 10008, Type = ChatType.Group },
            Text = "Bot's own message",
            Entities = null
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.SetupGet(c => c.BotId).Returns(botId);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.VerifyGet(c => c.BotId);
        _mockTelegramBotClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateLogCommand_ShouldSendActionLog()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10009, Type = ChatType.Private },
            Text = "/log",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };
        var update = new Update { Id = 1, Message = message };

        lock (_botLogic.ActionLog)
        {
            _botLogic.ActionLog.Add("[Time] Action1");
            _botLogic.ActionLog.Add("[Time] Action2");
        }

        var expectedLogContent = $"[Time] Action1{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}[Time] Action2";

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                message.Chat.Id,
                expectedLogContent,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateEngageCommand_ShouldEnableEngagement()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10010, Type = ChatType.Private },
            Text = "/engage",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 7 }]
        };
        var update = new Update { Id = 1, Message = message };

        _botLogic.Engaged = false;

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        Assert.IsTrue(_botLogic.Engaged);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.Bot_is_engaged,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateUnknownCommand_ShouldNotifyUnknownCommand()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10011, Type = ChatType.Private },
            Text = "/unknown",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 8 }]
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.UnknownCommand,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupNoRestrictCommand_ShouldDisableRestrictions()
    {
        // Arrange
        var chat = new Chat { Id = 10012, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/no_restrict",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 12 }]
        };
        var update = new Update { Id = 1, Message = message };

        lock (_botLogic.Settings)
        {
            _botLogic.Settings.BanUsers = true;
            _botLogic.Settings.UseMute = true;
        }

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberOwner
            {
                User = adminUser
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.IsFalse(_botLogic.Settings.BanUsers);
            Assert.IsFalse(_botLogic.Settings.UseMute);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            Resource.users_will_not_be_restricted,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupSetSpamTimeCommand_ShouldUpdateSpamTimeWindow()
    {
        // Arrange
        var chat = new Chat { Id = 10013, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var administrator = new ChatMemberAdministrator { User = adminUser, CanRestrictMembers = true };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/set_spam_time 10",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 14 }]
        };
        var update = new Update { Id = 1, Message = message };

        
        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    administrator,
                    new ChatMemberOwner { User = new User { Id = OwnerId, FirstName = "Admin" } }
                ]
            );

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(administrator);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.AreEqual(TimeSpan.FromSeconds(10), _botLogic.Settings.SpamTimeWindow);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            "Spam time window set to 10 seconds.",
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupUnknownCommand_ShouldNotifyUnknownCommand()
    {
        // Arrange
        var chat = new Chat { Id = 10014, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/unknown",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 8 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new ChatMemberAdministrator { User = adminUser, CanRestrictMembers = true },
                    new ChatMemberOwner { User = new User { Id = OwnerId, FirstName = "Admin" } }
                ]
            );

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            "Unknown command.",
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [TestMethod]
    public async Task HandleUpdateAsync_WhenApiRequestExceptionOccurs_ShouldLogError()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = OwnerId, Type = ChatType.Private },
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        var apiException = new ApiRequestException("Bad Request: Something went wrong");

        _mockTelegramBotClient.Setup(c => c.SendTextMessageAsync(
                message.Chat.Id,
                It.IsAny<string>(),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(apiException);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            OwnerId,
            $"API Error: {Environment.NewLine}ErrorCode: 0 | Bad Request: Something went wrong{Environment.NewLine} | ",
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            true,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupCommandFromOwner_ShouldProcessCommand()
    {
        // Arrange
        var chat = new Chat { Id = 10015, Type = ChatType.Group };
        var ownerUser = new User { Id = OwnerId, FirstName = "Owner" };
        var message = new Message
        {
            MessageId = 1,
            From = ownerUser,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                ownerUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberOwner
            {
                User = ownerUser
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                It.Is<string>(s => s.Contains("Available commands")),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupCommandFromRegularMember_ShouldIgnoreCommand()
    {
        // Arrange
        var chat = new Chat { Id = 10016, Type = ChatType.Group };
        var memberUser = new User { Id = OwnerId + 2, FirstName = "Member" };
        var message = new Message
        {
            MessageId = 1,
            From = memberUser,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                memberUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberMember
            {
                User = memberUser
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            It.IsAny<ChatId>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<ParseMode>(),
            It.IsAny<IEnumerable<MessageEntity>>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<IReplyMarkup>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupCommandFromAdmin_ShouldProcessCommandIfGroupOwnedByBotOwner()
    {
        // Arrange
        var chat = new Chat { Id = 10017, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatMemberOwner
                {
                    User = new User { Id = OwnerId }
                }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                It.Is<string>(s => s.Contains("Available commands")),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupCommandFromAdmin_ShouldIgnoreCommandIfGroupNotOwnedByBotOwner()
    {
        // Arrange
        var chat = new Chat { Id = 10018, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatMemberOwner
                {
                    User = new User { Id = OwnerId + 2 }
                }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                It.Is<string>(s => s.Contains(Resource.chat_does_not_belong_owner)),
                default,
                default,
                default,
                default,
                true,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTelegramBotClient.Verify(c => c.LeaveChatAsync(
            message.Chat.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_MessageFromAnonymousUser_ShouldIgnoreMessage()
    {
        // Arrange
        var chat = new Chat { Id = 10015, Type = ChatType.Group };
        var ownerUser = new User { Id = OwnerId, FirstName = "Owner" };
        var groupBot = new User { Id = BotLogic.GroupAnonymousAdminId, FirstName = "GroupBot" };
        var message = new Message
        {
            MessageId = 1,
            From = groupBot,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new ChatMemberOwner { User = ownerUser }
                ]
            );

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                groupBot.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberOwner
            {
                User = groupBot
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                It.Is<string>(s => s.Contains("Available commands")),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateDisengageCommand_ShouldDisableEngagement()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10022, Type = ChatType.Private },
            Text = "/disengage",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 10 }]
        };
        var update = new Update { Id = 1, Message = message };

        _botLogic.Engaged = true;

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        Assert.IsFalse(_botLogic.Engaged);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.Bot_disengaged,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateRestartServiceCommand_ShouldRestartService()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10023, Type = ChatType.Private },
            Text = "/restart_service",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 16 }]
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            Resource.Restarting,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockApplicationLifetime.Verify(a => a.Exit(42), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupMuteCommand_ByAdmin_ShouldEnableMuteUsers()
    {
        // Arrange
        var chat = new Chat { Id = 10024, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/mute",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        lock (_botLogic.Settings)
        {
            _botLogic.Settings.BanUsers = true;
            _botLogic.Settings.UseMute = false;
        }

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatMemberOwner { User = new User { Id = OwnerId } },
                new ChatMemberAdministrator { User = adminUser }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.IsFalse(_botLogic.Settings.BanUsers);
            Assert.IsTrue(_botLogic.Settings.UseMute);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            Resource.users_will_be_muted,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupSetRestrictionTimeCommand_ValidArgument_ShouldUpdateRestrictionDuration()
    {
        // Arrange
        var chat = new Chat { Id = 10025, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        const int restrictionMinutes = 300;
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = $"/set_restriction_time {restrictionMinutes}",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 21 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatMemberOwner { User = new User { Id = OwnerId } },
                new ChatMemberAdministrator { User = adminUser }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.AreEqual(TimeSpan.FromMinutes(restrictionMinutes), _botLogic.Settings.RestrictionDuration);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                string.Format(Resource.Restriction_duration, restrictionMinutes),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupSetRestrictionTimeCommand_ValidArgument_ShouldSetRestrictionForever()
    {
        // Arrange
        var chat = new Chat { Id = 10025, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/set_restriction_time 0",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 21 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChatMemberOwner { User = new User { Id = OwnerId } },
                new ChatMemberAdministrator { User = adminUser }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        lock (_botLogic.Settings)
        {
            Assert.AreEqual(null, _botLogic.Settings.RestrictionDuration);
        }

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                chat.Id,
                Resource.Restriction_forever,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupSetRestrictionTimeCommand_InvalidArgument_ShouldSendErrorMessage()
    {
        // Arrange
        var chat = new Chat { Id = 10026, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId + 1, FirstName = "Admin" };
        const string invalidArgs = "invalid";
        var message = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = $"/set_restriction_time {invalidArgs}",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 21 }]
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberAdministrator
            {
                User = adminUser,
                CanRestrictMembers = true
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChatMemberOwner { User = new User { Id = OwnerId } },
                new ChatMemberAdministrator { User = adminUser }
            ]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            Resource.Invalid_restriction_duration,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_GroupCommandInNonGroupChat_ShouldIgnoreCommand()
    {
        // Arrange
        var chat = new Chat { Id = 10027, Type = ChatType.Channel };
        var user = new User { Id = BotLogic.GenericTelegramId, FirstName = "User" };
        var message = new Message
        {
            MessageId = 1,
            From = user,
            Chat = chat,
            Text = "/help",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.VerifyGet(c => c.BotId);
        _mockTelegramBotClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task HandleUpdateAsync_MessageNotReplyToLinkedChannelPost_ShouldNotTakeAction()
    {
        // Arrange
        _botLogic.Engaged = true;

        var chat = new Chat { Id = 10028, Type = ChatType.Group };
        var user = new User { Id = 123456, FirstName = "User" };
        var message = new Message
        {
            MessageId = 1,
            From = user,
            Chat = chat,
            Text = "Regular message",
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = 789012, FirstName = "AnotherUser" }, // Not from GenericTelegramId
                Date = DateTime.UtcNow.AddMinutes(-1)
            }
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
                It.IsAny<ChatId>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
                It.IsAny<ChatId>(),
                It.IsAny<long>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mockTelegramBotClient.Verify(c => c.RestrictChatMemberAsync(
                It.IsAny<ChatId>(),
                It.IsAny<long>(),
                It.IsAny<ChatPermissions>(),
                It.IsAny<bool>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                It.IsAny<ChatId>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ParseMode>(),
                It.IsAny<IEnumerable<MessageEntity>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<IReplyMarkup>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}