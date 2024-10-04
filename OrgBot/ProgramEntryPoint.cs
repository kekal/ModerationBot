using OrgBot.TestEntities;

namespace OrgBot;

public static class ProgramEntryPoint
{
    public static async Task Main()
    {
        var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        var botOwner = Environment.GetEnvironmentVariable("OWNER");

        if (botToken == null)
        {
            await Console.Error.WriteLineAsync("BOT_TOKEN env variable is not specified");
            Environment.Exit(1);
        }

        if (long.TryParse(botOwner, out var ownerId))
        {
            await Console.Out.WriteLineAsync($"Bot will work only for chats created by the user {ownerId}");
        }

        var program = new BotLogic(botToken, ownerId, new ApplicationLifetime());
        await program.RunAsync();
    }
}