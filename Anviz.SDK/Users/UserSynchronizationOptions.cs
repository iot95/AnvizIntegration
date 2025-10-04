using System;

namespace Anviz.SDK.Users
{
    public class UserSynchronizationOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool ForceFaceTemplateRewrite { get; set; } = false;
        public bool RemoveOrphanFaceTemplates { get; set; } = true;
        public bool IgnoreFaceTemplateReadErrors { get; set; } = true;
    }
}

