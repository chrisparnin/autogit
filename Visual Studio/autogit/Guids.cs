// Guids.cs
// MUST match guids.h
using System;

namespace ninlabsresearch.autogit
{
    static class GuidList
    {
        public const string guidautogitPkgString = "c567cea0-d935-42b4-8ae0-31a019f6a071";
        public const string guidautogitCmdSetString = "7e2e501b-d891-4cab-a411-686d41e9c27b";

        public static readonly Guid guidautogitCmdSet = new Guid(guidautogitCmdSetString);
    };
}