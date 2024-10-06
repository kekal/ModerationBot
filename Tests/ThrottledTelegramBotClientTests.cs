using Moq;
using System.Diagnostics;
using OrgBot.TestingEntities;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OrgBot.Tests;

[TestClass]
public class ThrottledTelegramBotClientTests
{
    private Mock<IMyTelegramBotClient> _mockClient = null!;
    private ThrottledTelegramBotClient _throttledClient = null!;
    private readonly TimeSpan _throttleDelay = TimeSpan.FromSeconds(1);

    [TestInitialize]
    public void Setup()
    {
        _mockClient = new Mock<IMyTelegramBotClient>();
        _throttledClient = new ThrottledTelegramBotClient(_mockClient.Object, _throttleDelay);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _throttledClient.Dispose();
    }

    [TestMethod]
    public async Task GetMeAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _mockClient.SetupSequence(c => c.GetMeAsync(cancellationToken))
            .ReturnsAsync(new User { Id = 123, FirstName = "TestBot" })
            .ReturnsAsync(new User { Id = 456, FirstName = "TestBot2" });

        // Act
        var stopwatch = Stopwatch.StartNew();
        var user1 = await _throttledClient.GetMeAsync(cancellationToken);
        var user2 = await _throttledClient.GetMeAsync(cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(123, user1.Id);
        Assert.AreEqual(456, user2.Id);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.GetMeAsync(cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SendTextMessageAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const string text = "Hello, World!";
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.SendTextMessageAsync(
                chatId, text, It.IsAny<int?>(), It.IsAny<ParseMode?>(),
                It.IsAny<IEnumerable<MessageEntity>>(), It.IsAny<bool?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<IReplyMarkup>(), cancellationToken))
            .ReturnsAsync(new Message { MessageId = 1 });

        // Act
        var stopwatch = Stopwatch.StartNew();
        var message1 = await _throttledClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
        var message2 = await _throttledClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(1, message1.MessageId);
        Assert.AreEqual(1, message2.MessageId);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.SendTextMessageAsync(
            chatId, text, It.IsAny<int?>(), It.IsAny<ParseMode?>(),
            It.IsAny<IEnumerable<MessageEntity>>(), It.IsAny<bool?>(),
            It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<bool?>(), It.IsAny<IReplyMarkup>(), cancellationToken), Times.Exactly(2));
    }


    [TestMethod]
    public async Task DeleteMessageAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const int messageId = 1;
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.DeleteMessageAsync(chatId, messageId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        await _throttledClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.DeleteMessageAsync(chatId, messageId, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task BanChatMemberAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const long userId = 789L;
        DateTime? untilDate = DateTime.UtcNow.AddDays(1);
        const bool revokeMessages = false;
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken);
        await _throttledClient.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.BanChatMemberAsync(chatId, userId, untilDate, revokeMessages, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SetMyCommandsAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var commands = new List<BotCommand>
        {
            new() { Command = "start", Description = "Start the bot" },
            new() { Command = "help", Description = "Show help" }
        };
        BotCommandScope? scope = null;
        string? languageCode = null;
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken);
        await _throttledClient.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.SetMyCommandsAsync(commands, scope, languageCode, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SendContactAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const string phoneNumber = "+1234567890";
        const string firstName = "John";
        const string lastName = "Doe";
        var vCard = $"BEGIN:VCARD{Environment.NewLine}VERSION:2.1{Environment.NewLine}N:Doe;John{Environment.NewLine}TEL;CELL:+1234567890{Environment.NewLine}END:VCARD";
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.SendContactAsync(
                chatId, phoneNumber, firstName, It.IsAny<int?>(), lastName, vCard,
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<IReplyMarkup>(), cancellationToken))
            .ReturnsAsync(new Message { MessageId = 1 });

        // Act
        var stopwatch = Stopwatch.StartNew();
        var message1 = await _throttledClient.SendContactAsync(chatId, phoneNumber, firstName, lastName: lastName, vCard: vCard, cancellationToken: cancellationToken);
        var message2 = await _throttledClient.SendContactAsync(chatId, phoneNumber, firstName, lastName: lastName, vCard: vCard, cancellationToken: cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(1, message1.MessageId);
        Assert.AreEqual(1, message2.MessageId);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.SendContactAsync(
            chatId, phoneNumber, firstName, It.IsAny<int?>(), lastName, vCard,
            It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int?>(),
            It.IsAny<bool?>(), It.IsAny<IReplyMarkup>(), cancellationToken), Times.Exactly(2));
    }


    [TestMethod]
    public async Task LeaveChatAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.LeaveChatAsync(chatId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.LeaveChatAsync(chatId, cancellationToken);
        await _throttledClient.LeaveChatAsync(chatId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.LeaveChatAsync(chatId, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task BanChatSenderChatAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const long senderChatId = 789L;
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken);
        await _throttledClient.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.BanChatSenderChatAsync(chatId, senderChatId, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task RestrictChatMemberAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const long userId = 789L;
        var permissions = new ChatPermissions { CanSendMessages = false };
        bool? useIndependentChatPermissions = null;
        DateTime? untilDate = DateTime.UtcNow.AddHours(1);
        var cancellationToken = CancellationToken.None;

        _mockClient.Setup(c => c.RestrictChatMemberAsync(
                chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _throttledClient.RestrictChatMemberAsync(chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken);
        await _throttledClient.RestrictChatMemberAsync(chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");

        // Verify that the method was called twice
        _mockClient.Verify(c => c.RestrictChatMemberAsync(
            chatId, userId, permissions, useIndependentChatPermissions, untilDate, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetChatAdministratorsAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        var cancellationToken = CancellationToken.None;
        var chatMembers = new ChatMember[]
        {
            new ChatMemberAdministrator { User = new User { Id = 1, FirstName = "Admin1" } },
            new ChatMemberAdministrator { User = new User { Id = 2, FirstName = "Admin2" } }
        };

        _mockClient.Setup(c => c.GetChatAdministratorsAsync(chatId, cancellationToken))
            .ReturnsAsync(chatMembers);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result1 = await _throttledClient.GetChatAdministratorsAsync(chatId, cancellationToken);
        var result2 = await _throttledClient.GetChatAdministratorsAsync(chatId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(2, result1.Length);
        Assert.AreEqual(2, result2.Length);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.GetChatAdministratorsAsync(chatId, cancellationToken), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetChatAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        var cancellationToken = CancellationToken.None;
        var chat = new Chat { Id = chatId.Identifier!.Value, Title = "Test Chat" };

        _mockClient.Setup(c => c.GetChatAsync(chatId, cancellationToken))
            .ReturnsAsync(chat);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result1 = await _throttledClient.GetChatAsync(chatId, cancellationToken);
        var result2 = await _throttledClient.GetChatAsync(chatId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(chatId.Identifier, result1.Id);
        Assert.AreEqual(chatId.Identifier, result2.Id);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.GetChatAsync(chatId, cancellationToken), Times.Exactly(2));
    }


    [TestMethod]
    public async Task GetChatMemberAsync_ShouldRespectThrottleDelay()
    {
        // Arrange
        var chatId = new ChatId(123456);
        const long userId = 789L;
        var cancellationToken = CancellationToken.None;
        var chatMember = new ChatMemberMember { User = new User { Id = userId, FirstName = "Test User" } };

        _mockClient.Setup(c => c.GetChatMemberAsync(chatId, userId, cancellationToken))
            .ReturnsAsync(chatMember);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result1 = await _throttledClient.GetChatMemberAsync(chatId, userId, cancellationToken);
        var result2 = await _throttledClient.GetChatMemberAsync(chatId, userId, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff >= _throttleDelay, $"Throttling delay was not applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(userId, result1.User.Id);
        Assert.AreEqual(userId, result2.User.Id);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.GetChatMemberAsync(chatId, userId, cancellationToken), Times.Exactly(2));
    }


    [TestMethod]
    public async Task GetUpdatesAsync_ShouldNotBeThrottled()
    {
        // Arrange
        int? offset = null;
        int? limit = 100;
        int? timeout = 0;
        List<UpdateType>? allowedUpdates = null;
        var cancellationToken = CancellationToken.None;
        var updates = new Update[]
        {
            new() { Id = 1, Message = new Message { MessageId = 1 } },
            new() { Id = 2, Message = new Message { MessageId = 2 } }
        };

        _mockClient.Setup(c => c.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken))
            .ReturnsAsync(updates);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result1 = await _throttledClient.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken);
        var result2 = await _throttledClient.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken);
        stopwatch.Stop();

        var timeDiff = stopwatch.Elapsed;

        // Assert
        Assert.IsTrue(timeDiff < _throttleDelay, $"Unexpected delay was applied. Elapsed time: {timeDiff.TotalMilliseconds} ms");
        Assert.AreEqual(2, result1.Length);
        Assert.AreEqual(2, result2.Length);

        // Verify that the method was called twice
        _mockClient.Verify(c => c.GetUpdatesAsync(offset, limit, timeout, allowedUpdates, cancellationToken), Times.Exactly(2));
    }


}