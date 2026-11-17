using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

using XRL;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ModRecord : IDisposable
    {
        public class EqualityComparer : CompositeEqualityComparer<ModRecord>
        {
            public override bool Equals(ModRecord x, ModRecord y)
                => x == null
                    || y == null
                ? (x == null) == (y == null)
                : x.ID == y.ID
                    && x.SemVer == y.SemVer
                ;

            public override int GetHashCode(ModRecord obj)
                => (obj?.ID?.GetHashCode() ?? 0)
                ^ (obj?.Version?.GetHashCode() ?? 0)
                ;
        }

        public static EqualityComparer EQComp = new();

        [JsonProperty]
        public string ID;
        [JsonProperty]
        public string Version;
        [JsonProperty]
        public string Title;

        [JsonIgnore]
        public XRL.Version SemVer => new(Version);

        public ModRecord()
            : base()
        { }

        public ModRecord(string ID, string Version, string Title)
            : this()
        {
            this.ID = ID;
            this.Version = Version;
            this.Title = Title;
        }

        public ModRecord(ModRecord Source)
            : this(Source?.ID, Source?.Version, Source?.Title)
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

            if (!Title.IsNullOrEmpty())
                return Title;

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

        public static ModRecord ParseString(string String)
        {
            if (String.IsNullOrEmpty())
                return null;

            if (!String.Contains("@@"))
                String += "@@0.0.0";

            string[] versionPair = String.Split("@@");

            bool rightSideTitle = versionPair[1].Contains(";;");

            string[] remainingPair = rightSideTitle
                ? versionPair[1].Split(";;")
                : versionPair[0].Split(";;")
                ;

            return new ModRecord
            {
                ID = rightSideTitle
                    ? versionPair[0]
                    : remainingPair[0]
                    ,
                Title = remainingPair.Length > 1
                    ? remainingPair[1]
                    : null
                    ,
                Version = rightSideTitle
                    ? remainingPair[0]
                    : versionPair[1]
                    ,
            };
        }

        public static string GetDisplayName(ModInfo ModInfo)
            => ModInfo?.Manifest?.Title
            ?? ModInfo?.WorkshopInfo?.Title
            ?? ModInfo?.ID
            ;

        public static string GetVersion(ModInfo ModInfo)
            => (ModInfo?.Manifest?.Version)?.ToString() is string version
                && !version.IsNullOrEmpty()
            ? version
            : "0.0.0"
            ;

        public static ModRecord FromInfo(ModInfo ModInfo)
        {
            if (ModInfo == null)
                return null;

            return new ModRecord
            {
                ID = ModInfo.ID,
                Title = GetDisplayName(ModInfo),
                Version = GetVersion(ModInfo),
            };
        }

        public static IEnumerable<ModRecord> GetRunningMods(Predicate<ModRecord> Where = null)
        {
            foreach (var activeMod in ModManager.ActiveMods.IteratorSafe())
                if (FromInfo(activeMod) is ModRecord modRecord
                    && Where?.Invoke(modRecord) is not false)
                    yield return modRecord;
        }

        public static IEnumerable<ModRecord> GetAvailableMods(Predicate<ModRecord> Where = null)
        {
            foreach (var mod in ModManager.Mods.IteratorSafe())
                if (FromInfo(mod) is ModRecord modRecord
                    && Where?.Invoke(modRecord) is not false)
                    yield return modRecord;
        }

        public string ToParsableString()
        {
            if (ID == null)
                return null;

            string output = ID;
            
            if (!Title.IsNullOrEmpty()
                && ID != Title)
                output += $";;{Title}";

            if (!Version.IsNullOrEmpty())
                output += $"@@{Version}";

            return output;
        }

        public void Dispose()
        {
            ID = null;
            Title = null;
            Version = null;
        }

        public static explicit operator string(ModRecord Operand)
            => Operand?.ToParsableString()
            ;

        public static explicit operator ModRecord(string Operand)
            => ParseString(Operand)
            ;
    }

}
