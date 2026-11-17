using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.Serialization;

using XRL;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ModRecordSet : CoalescibleSet<ModRecord>, IBonesFileElement
    {
        public static int BaseBonesWeight = 1000;

        [JsonProperty]
        public string ID;

        [JsonIgnore]
        string IBonesFileElement.BonesID
        {
            get => ID;
            set => ID = value;
        }

        [JsonProperty(nameof(IBonesFileElement.BonesID))]
        string _BonesID { set => ID = value; }

        [JsonProperty]
        public string BonesModVersion;

        [JsonProperty]
        private ModRecord[] Records
        {
            get => ToArray();
            set
            {
                Clear();
                EnsureCapacity(value?.Length ?? 0);
                AddRange(value.IteratorSafe());
            }
        }

        public ModRecordSet()
            : base(ModRecord.EQComp, Coalescer<ModRecord>.Default)
        { }

        public ModRecordSet(string BonesID)
            : this()
        {
            this.ID = BonesID;
        }

        public ModRecordSet(string BonesID, string BonesModVersion, IEnumerable<string> ModsEnabled)
            : base(ModsEnabled?.Count() ?? 4, ModRecord.EQComp, Coalescer<ModRecord>.Default)
        {
            this.ID = BonesID;
            this.BonesModVersion = BonesModVersion;
            foreach (var modEnabled in ModsEnabled.IteratorSafe())
                if (ModRecord.ParseString(modEnabled) is ModRecord modRecord)
                    if (!Add(modRecord))
                        modRecord.Dispose();
        }

        public ModRecordSet(IEnumerable<ModRecord> ModRecords)
            : base(ModRecords?.Count() ?? 4, ModRecord.EQComp, Coalescer<ModRecord>.Default)
        {
            foreach (var modRecord in ModRecords.IteratorSafe())
            {
                var newModRecord = modRecord.Clone();
                if (!Add(newModRecord))
                    newModRecord.Dispose();
            }
        }

        public ModRecordSet(string BonesID, string BonesModVersion, IEnumerable<ModRecord> ModRecords)
            : this(ModRecords)
        {
            ID = BonesID;
            this.BonesModVersion = BonesModVersion;
        }

        public ModRecordSet(ModRecordSet Source)
            : this(Source?.ID, Source?.BonesModVersion, Source)
        {
            EqualityComparer = Source?.EqualityComparer ?? ModRecord.EQComp;
            Coalescer = Source?.Coalescer ?? Coalescer<ModRecord>.Default;
        }

        public static ModRecordSet NewEmpty()
            => new(Guid.Empty.ToString(), Utils.ModVersion.ToString(), ModRecord.GetRunningMods())
            ;

        public static ModRecordSet NewCurrent()
            => new(The.Game.GameID, Utils.ModVersion.ToString(), ModRecord.GetRunningMods())
            ;

        public static int? GetBonesWeight(ModRecordSet EnabledMods)
        {
            if (EnabledMods.IsNullOrEmpty())
                return null;

            int saveWeight = BaseBonesWeight;

            var modsDiffer = ModsDifferInfo.Get(EnabledMods, false);

            saveWeight += modsDiffer.EnabledWhereBonesDisabledCount * -1;
            saveWeight += modsDiffer.DisabledWhereBonesEnabledCount * -2;
            saveWeight += modsDiffer.UnavailableWhereBonesEnabledCount * -4;

            saveWeight += modsDiffer.EnabledWhereBonesEnabledCount * 3;

            /*if (Stats.Encountered is BonesStatSet encountered)
            {
                int timesEncountered = encountered.GetStatValue(OsseousAsh.Config.ID);
                // do something on the basis of times encountered maybe?
            }*/

            if (new XRL.Version(EnabledMods.BonesModVersion) is XRL.Version saveVersion
                && Utils.ModVersion is XRL.Version modVersion)
            {
                if (modVersion < saveVersion)
                    return null;

                if (modVersion.Build != saveVersion.Build)
                    return null;

                int versionDiff = Math.Max(0, modVersion.Revision - saveVersion.Revision);

                int diffReduction = 100;
                for (int i = 0; i < versionDiff; i++)
                {
                    if (diffReduction <= 0
                        || saveWeight <= 0)
                        break;

                    saveWeight -= diffReduction;

                    diffReduction *= 2;
                }
            }

            return Math.Max(1, saveWeight);
        }

        public int? GetBonesWeight()
            => GetBonesWeight(this)
            ;
    }
}
