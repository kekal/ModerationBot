﻿namespace OrgBot.TestEntities;

public class ApplicationLifetime : IApplicationLifetime
{
    public void Exit(int exitCode)
    {
        Environment.Exit(exitCode);
    }
}