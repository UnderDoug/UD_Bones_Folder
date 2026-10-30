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
    public class ModRecordCollection : CoalescibleSet<ModRecord>, IBonesFileElement
    {
        public static int BaseBonesWeight = 1000;

        [JsonProperty]
        public string BonesID;
        [JsonProperty]
        public string BonesModVersion;

        [JsonProperty]
        public ModRecord[] Records
        {
            get => Items;
            set => Items = value;
        }

        [JsonIgnore]
        string IBonesFileElement.BonesID
        {
            get => BonesID;
            set => BonesID = value;
        }

        public ModRecordCollection()
            : base(ModRecord.EQComp, Coalescer<ModRecord>.Default)
        { }

        public ModRecordCollection(string BonesID)
            : this()
        {
            this.BonesID = BonesID;
        }

        public ModRecordCollection(string BonesID, string BonesModVersion, IEnumerable<string> ModsEnabled)
            : base(ModsEnabled?.Count() ?? 4, ModRecord.EQComp, Coalescer<ModRecord>.Default)
        {
            this.BonesID = BonesID;
            this.BonesModVersion = BonesModVersion;
            foreach (var modEnabled in ModsEnabled.IteratorSafe())
                if (ModRecord.Parse(modEnabled) is ModRecord modRecord)
                    if (!Add(modRecord))
                        modRecord.Dispose();
        }

        public ModRecordCollection(string BonesID, string BonesModVersion, IEnumerable<ModRecord> ModRecords)
            : base(ModRecords?.Count() ?? 4, ModRecord.EQComp, Coalescer<ModRecord>.Default)
        {
            this.BonesID = BonesID;
            this.BonesModVersion = BonesModVersion;
            foreach (var modRecord in ModRecords.IteratorSafe())
            {
                var newModRecord = modRecord.Clone();
                if (!Add(newModRecord))
                    newModRecord.Dispose();
            }
        }

        public ModRecordCollection(ModRecordCollection Source)
            : this(Source?.BonesID, Source?.BonesModVersion, Source)
        {
            EqualityComparer = Source?.EqualityComparer ?? ModRecord.EQComp;
            Coalescer = Source?.Coalescer ?? Coalescer<ModRecord>.Default;
        }

        public static ModRecordCollection NewEmpty()
            => new(Guid.Empty.ToString(), Utils.ModVersion.ToString(), ModRecord.GetRunningMods())
            ;

        public static ModRecordCollection NewCurrent()
            => new(The.Game.GameID, Utils.ModVersion.ToString(), ModRecord.GetRunningMods())
            ;

        public static int? GetBonesWeight(ModRecordCollection EnabledMods)
        {
            if (EnabledMods.IsNullOrEmpty())
                return null;

            int saveWeight = BaseBonesWeight;

            var modsDiffer = ModsDifferInfo.Get(EnabledMods, false);

            saveWeight += modsDiffer.EnabledWhereBonesDisabled * -1;
            saveWeight += modsDiffer.DisabledWhereBonesEnabled * -2;
            saveWeight += modsDiffer.UnavailableWhereBonesEnabled * -4;

            saveWeight += modsDiffer.MatchingMods * 3;

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
