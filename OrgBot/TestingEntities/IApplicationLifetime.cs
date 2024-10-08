namespace OrgBot.TestingEntities;

public interface IApplicationLifetime
{
    /// <inheritdoc cref="Environment.Exit"/>>
    void Exit(int exitCode);
}