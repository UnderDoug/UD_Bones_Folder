using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

using XRL;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ModRecord : ICoalescible<ModRecord>, IDisposable
    {
        public class EqualityComparer : CompositeEqualityComparer<ModRecord>
        {
            public override bool Equals(ModRecord x, ModRecord y)
                => x == null
                    || y == null
                ? (x == null) == (y == null)
                : x.ID == y.ID
                ;

            public override int GetHashCode(ModRecord obj)
                => (obj?.ID?.GetHashCode() ?? 0)
                ;
        }

        public static EqualityComparer EQComp = new();

        [JsonProperty]
        public string ID;
        [JsonProperty]
        public string DisplayName;
        [JsonProperty]
        public string Version;

        [JsonIgnore]
        public XRL.Version SemVer => new(Version);

        public ModRecord()
            : base()
        { }

        public ModRecord(string ID, string DisplayName, string Version)
            : this()
        {
            this.ID = ID;
            this.DisplayName = DisplayName;
            this.Version = Version;
        }

        public ModRecord(ModRecord Source)
            : this(Source?.ID, Source?.DisplayName, Source?.Version)
        { }

        public ModRecord Clone()
            => new(this)
            ;

        public string GetDisplayName()
        {
            if (ID == null)
                return null;

            if (ID == string.Empty)
                return ID;

            if (!DisplayName.IsNullOrEmpty())
                return DisplayName;

            return ModManager.GetModTitle(ID);
        }

        public override string ToString()
        {
            string iD = ID;
            string displayName = GetDisplayName();
            string semVer = !Version.IsNullOrEmpty()
                ? $"v{SemVer}"
                : null
                ;

            string output = displayName;

            if (!semVer.IsNullOrEmpty())
                output += $" {semVer}";

            if (displayName != iD)
                output += $" ({iD})";

            return output;
        }

        public static ModRecord Parse(string String)
        {
            if (String == null)
                return null;

            string[] iDNamePair = String.Split(";;");

            string[] nameVersionPair = iDNamePair.Length > 1
                ? iDNamePair[1].Split("@@")
                : iDNamePair[0].Split("@@")
                ;

            return new ModRecord
            {
                ID = iDNamePair.Length > 1
                    ? iDNamePair[0]
                    : nameVersionPair[0]
                    ,
                DisplayName = nameVersionPair[0],
                Version = nameVersionPair.Length > 1
                    ? nameVersionPair[1]
                    : "0.0.0"
                    ,
            };
        }

        public static ModRecord Parse(ModInfo ModInfo)
        {
            if (ModInfo == null)

            if (!ModInfo.IsEnabled)
                return null;

            return new ModRecord
            {
                ID = ModInfo.ID,
                DisplayName = ModInfo.Manifest?.Title ?? ModInfo.WorkshopInfo?.Title ?? ModInfo.ID,
                Version = (ModInfo.Manifest?.Version)?.ToString() ?? "0.0.1",
            };
        }

        public static IEnumerable<ModRecord> GetRunningMods(Predicate<ModRecord> Where = null)
        {
            foreach (var activeMod in ModManager.ActiveMods.IteratorSafe())
                if (Parse(activeMod) is ModRecord modRecord
                    && Where?.Invoke(modRecord) is not false)
                    yield return modRecord;
        }

        public static IEnumerable<ModRecord> GetAvailableMods(Predicate<ModRecord> Where = null)
        {
            foreach (var mod in ModManager.Mods.IteratorSafe())
                if (Parse(mod) is ModRecord modRecord
                    && Where?.Invoke(modRecord) is not false)
                    yield return modRecord;
        }

        public string ToPairString()
        {
            if (ID == null)
                return null;

            return !DisplayName.IsNullOrEmpty()
                    && ID != DisplayName
                ? $"{ID};;{DisplayName}"
                : ID
                ;
        }

        public string ToParsableString()
        {
            if (ID == null)
                return null;

            string output = ID;
            
            if (!DisplayName.IsNullOrEmpty()
                && ID != DisplayName)
                output += $";;{DisplayName}";

            if (!Version.IsNullOrEmpty())
                output += $"@@{Version}";

            return output;
        }

        public void Dispose()
        {
            ID = null;
            DisplayName = null;
            Version = null;
        }

        public ModRecord Coalesce(ModRecord Other)
            => this
            ;

        public static explicit operator string(ModRecord Operand)
            => Operand?.ToParsableString()
            ;

        public static explicit operator ModRecord(string Operand)
            => Parse(Operand)
            ;
    }

}
