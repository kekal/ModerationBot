# ModerationBot

ModerationBot is a simple Telegram bot designed to handle spam sent by bots as an instant reaction to posts from the channel. 
It supports user restrictions and provides information about recent actions. 

Optionally, a channel owner can control the bot exclusively, ensuring exclusive use of the service.

Bot based on the popular [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) library.

## 🚀 Features 

- 🔒 Exclusive Ownership: Bot commands can be restricted to a channel owner, preventing unauthorized use.
- 🛡️ Spam Management: Automatically remove spam replies to the channel in specified timeout (10 sec by default) with customizable actions like banning or muting users.
- 🗒️ Action Logs: Keep track of the last 30 actions performed by the bot for transparency and accountability.
- 📋 No persistent data: All configuration, creds and actions are not stored anywhere.

## 📋 Prerequisites 

- .NET 8.0 SDK (in case of manual build)
- An active Telegram bot token. ([Create bot first](https://t.me/BotFather))
- Docker.
- Git.
- Optional: Telegram owner ID for exclusive use. ([Get my ID](https://t.me/getmyid_bot))

## 🛠️ Installation

1. Clone the repository:

   Go to the personal bot folder (<mark>installation cleans previous files</mark>)
    ```bash
    git clone https://github.com/your-repo/orgbot.git    
    ```

3. Run the bot:
    ```bash
    nohup ./install.sh BOT_TOKEN="YOUR_BOT_TOKEN" OWNER=109671846  >log.txt 2>&1 &
    tail -f log.txt    
    ```

## 📘 Usage 

### 🔑 Admin Commands (Private Chat) 

1. `/log`: Displays the last 30 actions logged by the bot.
2. `/engage`: Engage the bot to start processing updates.
3. `/disengage`: Disengage the bot to stop processing updates.
4. `/restart_service`: Stop and re-deploy service with the same environment with the last revision from GitHub.
5. `/exit`: Stops the service.
6. `/help`: Displays the available commands.

### 👥 Group Commands (Group Chats) 

1. `/ban`: Enables banning users when deleting spam (disables muting).
2. `/no_restrict`: Disables banning/muting users when deleting spam.
3. `/mute`: Mute users instead of banning them (disables banning).
4. `/set_spam_time <seconds>`: Sets the spam time window in seconds.
5. `/set_restriction_time <minutes | 0>`: Sets the restriction duration in minutes or ‘0’ for indefinite.
6. `/silent`: Toggles silent mode (no messages on spam actions).
7. `/help`: Displays the available commands.

## 👤 Bot Ownership (Optional) 

If an `OWNER` argument is specified, the bot will only keep the dialogue with the specified Telegram user and work only in groups created by that user.

## 📄 Logging 

The bot maintains a log of the last 30 actions, accessible using the `/log` command in private chat. Logs are stored in-memory and will be reset if the bot restarts.

## 🤝 Contributing

Contributions are welcome! Please submit a pull request or raise an issue to discuss any changes or feature requests.

## ➕ Additional
- [Telegram bots API book](https://telegrambots.github.io/book/)
- [Introduction to bots](https://core.telegram.org/bots)
- [Bot API](https://core.telegram.org/bots/api)
