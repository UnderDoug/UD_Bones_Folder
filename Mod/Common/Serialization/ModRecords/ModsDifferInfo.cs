using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Logging;

using XRL.Collections;

namespace UD_Bones_Folder.Mod
{
    public class ModsDifferInfo : IDisposable
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(ModsDifferInfo),
                MethodNameValues: new()
                {
                    { nameof(Get), false },
                });
        #endregion

        public bool IsDummy;
        public ModRecordSet UnavailableWhereBonesEnabled;
        public ModRecordSet EnabledWhereBonesDisabled;
        public ModRecordSet DisabledWhereBonesEnabled;
        public ModRecordSet EnabledWhereBonesEnabled;
        public bool IsError;

        public int UnavailableWhereBonesEnabledCount => UnavailableWhereBonesEnabled?.Count ?? -1;
        public int EnabledWhereBonesDisabledCount => EnabledWhereBonesDisabled?.Count ?? -1;
        public int DisabledWhereBonesEnabledCount => DisabledWhereBonesEnabled?.Count ?? -1;
        public int EnabledWhereBonesEnabledCount => EnabledWhereBonesEnabled?.Count ?? -1;

        public int DifferLevel
        {
            get
            {
                if (UnavailableWhereBonesEnabledCount < 0)
                {
                    IsError = true;
                    return -1;
                }

                IsError = false;
                if (DisabledWhereBonesEnabledCount > 0)
                    return 3;
                if (DisabledWhereBonesEnabledCount > 0)
                    return 2;
                return EnabledWhereBonesDisabledCount > 0
                    ? 1
                    : 0
                    ;
            }
        }

        public static ModsDifferInfo Get(ModRecordSet EnabledMods, bool IsDummy)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(EnabledMods), EnabledMods != null ? EnabledMods.Count : "null"),
                    Debug.Arg(nameof(IsDummy), IsDummy),
                });

            if (IsDummy)
            {
                return new ModsDifferInfo
                {
                    IsDummy = true
                };
            }

            if (EnabledMods is not ModRecordSet bonesEnabledMods)
            {
                Utils.Warn($"{nameof(ModsDifferInfo)}.{nameof(Get)} passed null {nameof(EnabledMods)}; Errored results will be displayed");
                return new ModsDifferInfo
                {
                    IsError = true,
                };
            }

            var runningMods = ModRecord.GetRunningMods();
            var availableMods = ModRecord.GetAvailableMods();

            using var unavailableWhereBonesEnabled = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(bonesEnabledMods);
            unavailableWhereBonesEnabled.RemoveAll(r
                => availableMods.Any(ar => ModRecord.EQComp.Equals(r, ar))
                );

            using var enabledWhereBonesDisabled = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(runningMods);
            enabledWhereBonesDisabled.RemoveAll(r
                => bonesEnabledMods.Any(ar => ModRecord.EQComp.Equals(r, ar))
                );

            using var disabledWhereBonesEnabled = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(bonesEnabledMods);
            disabledWhereBonesEnabled.RemoveAll(r
                => runningMods.Any(ar => ModRecord.EQComp.Equals(r, ar))
                || unavailableWhereBonesEnabled.Any(ar => ModRecord.EQComp.Equals(r, ar))
                );

            using var matchingMods = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(bonesEnabledMods);
            matchingMods.RemoveAll(r
                => disabledWhereBonesEnabled.Any(ar => ModRecord.EQComp.Equals(r, ar))
                || unavailableWhereBonesEnabled.Any(ar => ModRecord.EQComp.Equals(r, ar))
                || enabledWhereBonesDisabled.Any(ar => ModRecord.EQComp.Equals(r, ar))
                );

            return new ModsDifferInfo
            {
                UnavailableWhereBonesEnabled = new(unavailableWhereBonesEnabled),
                EnabledWhereBonesDisabled = new(enabledWhereBonesDisabled),
                DisabledWhereBonesEnabled = new(disabledWhereBonesEnabled),
                EnabledWhereBonesEnabled = new(matchingMods),
            };
        }

        public override string ToString()
        {
            if (IsDummy)
                return "mods: {{K|N/A}}".Colored("y"); ;

            if (IsError)
                return "mods: {{red|error}}".Colored("y"); ;

            if (DifferLevel < 0)
                return "mods: {{red|error}}".Colored("y"); ;

            if (DifferLevel == 0)
                return "{{green|\u00fb}}";

            string extras = null;
            if (EnabledWhereBonesDisabledCount > 0)
                extras = $"{extras}{(-EnabledWhereBonesDisabledCount).Signed()}".Colored("yellow");

            if (DisabledWhereBonesEnabledCount > 0)
            {
                if (!extras.IsNullOrEmpty())
                    extras += "|";
                extras = $"{extras}{DisabledWhereBonesEnabledCount.Signed()}".Colored("red");
            }

            if (UnavailableWhereBonesEnabledCount > 0)
            {
                if (!extras.IsNullOrEmpty())
                    extras += "|";
                extras = $"{extras}X{UnavailableWhereBonesEnabledCount}".Colored("red");
            }
            return $"mods: {extras}".Colored("y");
        }

        public void Dispose()
        {
            IsDummy = default;
            UnavailableWhereBonesEnabled = null;
            EnabledWhereBonesDisabled = null;
            DisabledWhereBonesEnabled = null;
            EnabledWhereBonesEnabled = null;
            IsError = default;
        }
    }
}
