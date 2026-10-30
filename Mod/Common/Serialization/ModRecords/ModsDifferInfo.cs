using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Collections;

namespace UD_Bones_Folder.Mod
{
    public class ModsDifferInfo
    {
        public bool IsDummy;
        public int UnavailableWhereBonesEnabled;
        public int EnabledWhereBonesDisabled;
        public int DisabledWhereBonesEnabled;
        public int MatchingMods;
        public int DifferLevel
        {
            get
            {
                if (UnavailableWhereBonesEnabled < 0)
                    return -1;
                if (DisabledWhereBonesEnabled > 0)
                    return 3;
                if (DisabledWhereBonesEnabled > 0)
                    return 2;
                return EnabledWhereBonesDisabled > 0
                    ? 1
                    : 0
                    ;
            }
        }
        public bool IsRed => DifferLevel >= 2;
        public bool IsYellow => DifferLevel == 1;

        public static ModsDifferInfo Get(ModRecordCollection EnabledMods, bool IsDummy)
        {
            if (IsDummy)
            {
                return new ModsDifferInfo
                {
                    IsDummy = true
                };
            }

            var enabledMods = EnabledMods;

            using var loadedMods = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(ModRecord.GetRunningMods());

            using var bonesHasButNotAvailable = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            var availableMods = ModRecord.GetAvailableMods();
            bonesHasButNotAvailable.RemoveAll(r => availableMods.Any(ar => ModRecord.EQComp.Equals(r, ar)));

            using var loadedButBonesMissing = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(loadedMods);
            loadedButBonesMissing.RemoveAll(r => enabledMods.Any(ar => ModRecord.EQComp.Equals(r, ar)));

            using var bonesHasButNotLoaded = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            bonesHasButNotLoaded.RemoveAll(delegate (ModRecord r)
            {
                return loadedMods.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || bonesHasButNotAvailable.Any(ar => ModRecord.EQComp.Equals(r, ar));
            });

            using var bothHaveLoaded = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            bothHaveLoaded.RemoveAll(delegate (ModRecord r)
            {
                return bonesHasButNotLoaded.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || bonesHasButNotAvailable.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || loadedButBonesMissing.Any(ar => ModRecord.EQComp.Equals(r, ar));
            });

            return new ModsDifferInfo
            {
                UnavailableWhereBonesEnabled = bonesHasButNotAvailable.Count,
                EnabledWhereBonesDisabled = loadedButBonesMissing.Count,
                DisabledWhereBonesEnabled = bonesHasButNotLoaded.Count,
                MatchingMods = bothHaveLoaded.Count,
            };
        }

        public override string ToString()
        {
            if (IsDummy)
                return "{{y|mods:}} {{K|N/A}}";

            if (DifferLevel < 0)
                return "{{y|mods:}} {{red|error}}";

            if (DifferLevel == 0)
                return "{{y|mods:}} {{green|\u00fb}}";

            string output = "mods:".Colored("y");

            string extras = null;
            if (EnabledWhereBonesDisabled > 0)
                extras = $"{extras}{(-EnabledWhereBonesDisabled).Signed()}".Colored("yellow");

            if (DisabledWhereBonesEnabled > 0)
            {
                if (!extras.IsNullOrEmpty())
                    extras += "|".Colored("y");
                extras = $"{extras}{DisabledWhereBonesEnabled.Signed()}".Colored("red");
            }

            if (UnavailableWhereBonesEnabled > 0)
            {
                if (!extras.IsNullOrEmpty())
                    extras += "|".Colored("y");
                extras = $"{extras}X{UnavailableWhereBonesEnabled}".Colored("red");
            }
            return $"{output} {extras}";
        }
    }
}
