using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Moderation
{
    [Serializable]
    public class BadDisplayName : IComposite
    {
        public bool IsBase;
        public bool IsAdjective;
        public bool IsSizeAdjective;
        public bool IsFactionAdjective;
        public bool IsHonorific;
        public bool IsTitle;
        public bool IsEpithet;

        public IEnumerable<string> DebugStrings()
        {
            yield return $"{nameof(IsBase)}: {IsBase}";
            yield return $"{nameof(IsAdjective)}: {IsAdjective}";
            yield return $"{nameof(IsSizeAdjective)}: {IsSizeAdjective}";
            yield return $"{nameof(IsFactionAdjective)}: {IsFactionAdjective}";
            yield return $"{nameof(IsHonorific)}: {IsHonorific}";
            yield return $"{nameof(IsTitle)}: {IsTitle}";
            yield return $"{nameof(IsEpithet)}: {IsEpithet}";
        }
    }
}
