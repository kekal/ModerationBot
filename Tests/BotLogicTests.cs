using Moq;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;
using TTCBT = ThrottledTelegramBotClient.TestingEntities;

namespace OrgBot.Tests;

[TestClass]
public class BotLogicTests
{
    private BotLogic _botLogic = null!;
    private Mock<TTCBT.IMyTelegramBotClient> _mockTelegramBotClient = null!;
    private Mock<TTCBT.IApplicationLifetime> _mockApplicationLifetime = null!;
    private ThrottledTelegramBotClient.ThrottledTelegramBotClient _throttledClient = null!;
    private const long OwnerId = 123456789;
    private const string BotToken = "test_bot_token";
    private const string TestSettingsFilePath = "test_botsettings.json";
    private readonly string TestLogFilePath = Path.Combine(".", "data", "bot_log.txt");

    [TestInitialize]
    public void Setup()
    {
        Environment.SetEnvironmentVariable("SETTINGS_PATH", TestSettingsFilePath);

        if (File.Exists(TestSettingsFilePath))
        {
            File.Delete(TestSettingsFilePath);
        }

        if (File.Exists(TestLogFilePath))
        {
            File.Delete(TestLogFilePath);
        }

        _mockTelegramBotClient = new Mock<TTCBT.IMyTelegramBotClient>();
        _throttledClient = new ThrottledTelegramBotClient.ThrottledTelegramBotClient(_mockTelegramBotClient.Object, TimeSpan.FromMilliseconds(100));
        _mockApplicationLifetime = new Mock<TTCBT.IApplicationLifetime>();
        _botLogic = new BotLogic(BotToken, OwnerId, _mockApplicationLifetime.Object);


        _botLogic.SetupLogger(async notification =>
        {
            await _throttledClient.SendTextMessageAsync(OwnerId, notification, allowSendingWithoutReply: true, cancellationToken: CancellationToken.None);
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(TestSettingsFilePath))
        {
            File.Delete(TestSettingsFilePath);
        }

        if (File.Exists(TestLogFilePath))
        {
            File.Delete(TestLogFilePath);
        }

        Environment.SetEnvironmentVariable("SETTINGS_PATH", null);

        _mockTelegramBotClient.Reset();
        _mockApplicationLifetime.Reset();
    }

    [TestMethod]
    public async Task HandleUpdateAsync_OwnerExitCommand_ShouldShutdownBot()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = OwnerId, Type = ChatType.Private },
            Text = "/exit",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 5 }]
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                message.Chat.Id,
                It.Is<string>(s => s.Contains(Resource.shutting_down)),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                true,
                default,
                It.IsAny<CancellationToken>())
            , Times.Once);

        _mockApplicationLifetime.Verify(a => a.Exit(0), Times.Once);
    }

     [TestMethod]
    public async Task SetCommandsAsync_ShouldSetCommands()
    {
        // Act
        await _botLogic.SetCommandsAsync(_throttledClient);

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
    }


    [TestMethod]
    public async Task HandleUpdateAsync_SpamMessageWithSilentMode_ShouldNotSendNotification()
    {
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

        // Arrange
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.SilentMode), true);

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

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.SilentMode), false);

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
        Assert.IsTrue(_botLogic.Settings.GetGroupSettings(chat.Id).SilentMode);
        

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
        var chat = new Chat { Id = 7007, Type = ChatType.Group };

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), true);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.UseMute), false);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.RestrictionDuration), TimeSpan.FromHours(1));
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.SilentMode), false);
        
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
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
                chat.Id,
                spammer.Id,
                It.IsAny<DateTime?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

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
                It.IsAny<CancellationToken>()),
            Times.Once);
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

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), false);

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
        Assert.IsTrue(_botLogic.Settings.GetGroupSettings(chat.Id).BanUsers);

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

        _botLogic.Settings.LogSize = 30;

        var update = new Update { Id = 1, Message = message };
        var privateHelpText =
            $"Available commands:{Environment.NewLine}" +
            $"/log - {string.Format(Resource.Show_the_last_actions, _botLogic.Settings.LogSize)}{Environment.NewLine}" +
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
        var chat = new Chat { Id = 10002, Type = ChatType.Group };

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), true);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.UseMute), false);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.RestrictionDuration), TimeSpan.FromHours(1));
        
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
        var message1 = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10009, Type = ChatType.Private },
            Text = "/engage",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 7 }]
        };
        var update1 = new Update { Id = 1, Message = message1 };

        var message2 = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = 10009, Type = ChatType.Private },
            Text = "/log",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };
        var update2 = new Update { Id = 2, Message = message2 };


        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update1, CancellationToken.None);
        await _botLogic.HandleUpdateAsync(_throttledClient, update2, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                It.IsAny<ChatId>(),
                It.Is<string>(s => s.Contains(Resource.Bot_is_engaged)),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                It.IsAny<bool?>(),
                default,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task HandleUpdateAsync_PrivateEngageCommand_ShouldEnableEngagement()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = OwnerId, Type = ChatType.Private },
            Text = "/engage",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 7 }]
        };
        var update = new Update { Id = 1, Message = message };

        _botLogic.Settings.Engaged = false;

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        Assert.IsTrue(_botLogic.Settings.Engaged);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            It.Is<string>(s => s.Contains(Resource.Bot_is_engaged)),
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
            OwnerId,
            It.Is<string>(s => s.Contains(Resource.UnknownCommand)),
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

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), true);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.UseMute), true);

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
        Assert.IsFalse(_botLogic.Settings.GetGroupSettings(chat.Id).BanUsers);
        Assert.IsFalse(_botLogic.Settings.GetGroupSettings(chat.Id).UseMute); 
        
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
        Assert.AreEqual(TimeSpan.FromSeconds(10), _botLogic.Settings.GetGroupSettings(chat.Id).SpamTimeWindow);

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
            OwnerId,
            It.Is<string>(s => s.Contains(string.Format(Resource.UnknownCommand_info, message.Chat.Id))),
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

        await using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        Assert.IsTrue(errorWriter.ToString().Contains("Telegram.Bot.Exceptions.ApiRequestException: Bad Request: Something went wrong"), "Standard Error does not contain the expected error message.");
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
            Chat = new Chat { Id = OwnerId, Type = ChatType.Private },
            Text = "/disengage",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 10 }]
        };
        var update = new Update { Id = 1, Message = message };

        _botLogic.Settings.Engaged = true;

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        Assert.IsFalse(_botLogic.Settings.Engaged);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            It.Is<string>(s => s.Contains(Resource.Bot_disengaged)),
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
    public async Task HandleUpdateAsync_PrivateRestartServiceCommand_ShouldRestartService()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            From = new User { Id = OwnerId, FirstName = "Owner" },
            Chat = new Chat { Id = OwnerId, Type = ChatType.Private },
            Text = "/restart_service",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 16 }]
        };
        var update = new Update { Id = 1, Message = message };

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            message.Chat.Id,
            It.Is<string>(s => s.Contains(Resource.Restarting)),
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

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), true);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.UseMute), false);

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
        Assert.IsFalse(_botLogic.Settings.GetGroupSettings(chat.Id).BanUsers);
        Assert.IsTrue(_botLogic.Settings.GetGroupSettings(chat.Id).UseMute);

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
        Assert.AreEqual(TimeSpan.FromMinutes(restrictionMinutes), _botLogic.Settings.GetGroupSettings(chat.Id).RestrictionDuration);

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
        Assert.AreEqual(null, _botLogic.Settings.GetGroupSettings(chat.Id).RestrictionDuration);

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
        _botLogic.Settings.Engaged = true;

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

    [TestMethod]
    public async Task ConfigurationPersistence_AfterRestart_BotUsesPersistedSettings()
    {
        // Arrange
        var chat = new Chat { Id = 9001, Type = ChatType.Group };
        var adminUser = new User { Id = OwnerId, FirstName = "Owner" };
        var spammer = new User { Id = 98765, FirstName = "Spammer" };

        var banCommandMessage = new Message
        {
            MessageId = 1,
            From = adminUser,
            Chat = chat,
            Text = "/ban",
            Entities = [new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 4 }]
        };
        var banCommandUpdate = new Update { Id = 1, Message = banCommandMessage };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                adminUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberOwner
            {
                User = adminUser
            });

        _mockTelegramBotClient.Setup(c => c.GetChatAdministratorsAsync(
                chat.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMemberOwner { User = adminUser }]);

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, banCommandUpdate, CancellationToken.None);

        //clear
        _mockTelegramBotClient.Invocations.Clear();

        // Arrange 2
        var newBotLogic = new BotLogic(BotToken, OwnerId, _mockApplicationLifetime.Object);
        newBotLogic.SetupLogger(async notification =>
        {
            await _throttledClient.SendTextMessageAsync(OwnerId, notification, disableNotification: true, cancellationToken: CancellationToken.None);
        });

        var spamMessage = new Message
        {
            MessageId = 2,
            From = spammer,
            Chat = chat,
            Text = "Spam message",
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 3,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var spamUpdate = new Update { Id = 2, Message = spamMessage };

        // Act 2
        await newBotLogic.HandleUpdateAsync(_throttledClient, spamUpdate, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
            chat.Id,
            spammer.Id,
            It.IsAny<DateTime?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
            chat.Id,
            spamMessage.MessageId,
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
            spamMessage.MessageId,
            true,
            default,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_NonMemberPostingLink_ShouldElaborateSpam()
    {
        // Arrange
        var chat = new Chat { Id = 11001, Type = ChatType.Group };
        var nonMemberUser = new User { Id = 222222, FirstName = "NonMember" };
        var message = new Message
        {
            MessageId = 1,
            From = nonMemberUser,
            Chat = chat,
            Text = "Check out this link: http://example.com",
            Entities = [new MessageEntity { Type = MessageEntityType.Url, Offset = 21, Length = 18 }],
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                nonMemberUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberLeft
            {
                User = nonMemberUser
            });

        var groupSettings = _botLogic.Settings.GetGroupSettings(chat.Id);
        groupSettings.BanUsers = true;
        groupSettings.UseMute = false;
        groupSettings.SilentMode = false;
        groupSettings.CleanNonGroupUrl = true;

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
            chat.Id,
            message.MessageId,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
            chat.Id,
            nonMemberUser.Id,
            It.IsAny<DateTime?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            chat.Id,
            "Non-group members are not allowed to post links.",
            default,
            default,
            default,
            default,
            true,
            default,
            message.ReplyToMessage.MessageId,
            true,
            default,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleUpdateAsync_MemberPostingLink_ShouldNotElaborateSpam()
    {
        // Arrange
        var chat = new Chat { Id = 11002, Type = ChatType.Group };
        var memberUser = new User { Id = 333333, FirstName = "Member" };
        var message = new Message
        {
            MessageId = 1,
            From = memberUser,
            Chat = chat,
            Text = "Check out this link: http://example.com",
            Entities = [new MessageEntity { Type = MessageEntityType.Url, Offset = 21, Length = 18 }],
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-1 - _botLogic.Settings.GetGroupSettings(chat.Id).SpamTimeWindow.TotalSeconds)
            }
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

        var groupSettings = _botLogic.Settings.GetGroupSettings(chat.Id);
        groupSettings.BanUsers = true;
        groupSettings.UseMute = false;
        groupSettings.SilentMode = false;

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

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
            It.IsAny<ChatId>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
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
    public async Task HandleUpdateAsync_NonMemberPostingLink_WithSilentMode_ShouldNotSendWarning()
    {
        // Arrange
        var chat = new Chat { Id = 11003, Type = ChatType.Group };
        var nonMemberUser = new User { Id = 444444, FirstName = "NonMember" };
        var message = new Message
        {
            MessageId = 1,
            From = nonMemberUser,
            Chat = chat,
            Text = "Visit my website: http://spam.com",
            Entities = [new MessageEntity { Type = MessageEntityType.Url, Offset = 18, Length = 16 }],
            Date = DateTime.UtcNow,
            ReplyToMessage = new Message
            {
                MessageId = 2,
                From = new User { Id = BotLogic.GenericTelegramId },
                Date = DateTime.UtcNow.AddSeconds(-5)
            }
        };
        var update = new Update { Id = 1, Message = message };

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                nonMemberUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberLeft
            {
                User = nonMemberUser
            });

        var groupSettings = _botLogic.Settings.GetGroupSettings(chat.Id);
        groupSettings.BanUsers = true;
        groupSettings.UseMute = false;
        groupSettings.SilentMode = true; // SilentMode is on

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.DeleteMessageAsync(
                chat.Id,
                message.MessageId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTelegramBotClient.Verify(c => c.BanChatMemberAsync(
                chat.Id,
                nonMemberUser.Id,
                It.IsAny<DateTime?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTelegramBotClient.Verify(c => c.SendTextMessageAsync(
                It.IsAny<ChatId>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
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


    [TestMethod]
    public async Task HandleUpdateAsync_NonMemberPostingSpam_ShouldDeleteAndBanUser()
    {
        // Arrange
        var chat = new Chat { Id = 77007, Type = ChatType.Group };

        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.BanUsers), true);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.UseMute), false);
        _botLogic.Settings.SetGroupSettings(chat.Id, nameof(GroupSettings.SilentMode), false);

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

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                spammer.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberLeft
            {
                User = spammer
            });

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
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [TestMethod]
    public async Task PerformUserThrottling_UserWithThrottleTime_ShouldMuteUser()
    {
        // Arrange
        var chat = new Chat { Id = 12001, Type = ChatType.Group };
        var throttledUser = new User { Id = 666666, FirstName = "ThrottledUser" };
        const uint throttleTime = 11;

        var message = new Message
        {
            MessageId = 1,
            From = throttledUser,
            Chat = chat,
            Text = "This is a message from a throttled user",
            Date = DateTime.UtcNow
        };
        var update = new Update { Id = 1, Message = message };

        var defaultPermissions = new ChatPermissions
        {
            CanSendMessages = true,
            CanSendDocuments = true,
            CanSendPhotos = true,
            CanSendPolls = true,
            CanSendOtherMessages = true,
            CanAddWebPagePreviews = true,
            CanChangeInfo = false,
            CanInviteUsers = true,
            CanPinMessages = false
        };
        _botLogic.Settings.SetUserState(chat.Id, throttledUser.Id, throttleTime, defaultPermissions);
        _botLogic.Timeout = 12;

        _mockTelegramBotClient.Setup(c => c.GetChatMemberAsync(
                chat.Id,
                throttledUser.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatMemberMember
            {
                User = throttledUser
            });

        // Act
        await _botLogic.HandleUpdateAsync(_throttledClient, update, CancellationToken.None);

        // Assert
        _mockTelegramBotClient.Verify(c => c.RestrictChatMemberAsync(
                chat.Id,
                throttledUser.Id,
                It.Is<ChatPermissions>(p => p.CanSendMessages == false),
                default,
                It.Is<DateTime?>(d => DHasValue(d, throttleTime)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static bool DHasValue(DateTime? d, uint throttleTime)
    {
        var dateTime = d.Value;
        var fromSeconds = DateTime.UtcNow + TimeSpan.FromSeconds(throttleTime);

        var sdfg = dateTime - fromSeconds;
        return d.HasValue && dateTime == fromSeconds;
    }




   
}