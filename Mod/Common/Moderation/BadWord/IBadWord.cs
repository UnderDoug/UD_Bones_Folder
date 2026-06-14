using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Moderation
{
    public interface IBadWord
    {
        [Serializable]
        public enum SeverityLevel
        {
            All,
            Mild,
            Medium,
            Strong,
            Severe,
            None,
        }

        private static Dictionary<string, SeverityLevel> _SeverityLevelValueCache;
        public static Dictionary<string, SeverityLevel> SeverityLevelValueCache => _SeverityLevelValueCache ??= Utils.GetValuesDictionary(ref _SeverityLevelValueCache);

        public const SeverityLevel DefaultSeverity = SeverityLevel.Strong;

        SeverityLevel Severity { get; }

        bool Check(string Text);
    }
}
