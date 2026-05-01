using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesStats
    {
        public static string MissingLunarRegent => "this =LunarShader:Moon Sovran:*=, lost to time,";

        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Encountered;
        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Defeated;
        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Reclaimed;

        public BonesStats()
        {
            Encountered = new();
            Defeated = new();
            Reclaimed = new();
        }

        public bool IncrementStat(ref BonesStatSet StatSet, Guid OsseousAshID)
            => (StatSet ??= new()).IncrementStat(OsseousAshID);

        public bool IncrementEncountered(Guid OsseousAshID)
            => IncrementStat(ref Encountered, OsseousAshID)
            ;

        public bool IncrementDefeated(Guid OsseousAshID)
            => IncrementStat(ref Defeated, OsseousAshID)
            ;

        public bool IncrementReclaimed(Guid OsseousAshID)
            => IncrementStat(ref Reclaimed, OsseousAshID)
            ;

        public int GetStatValue(ref BonesStatSet StatSet, Guid OsseousAshID)
            => (StatSet ??= new()).GetStatValue(OsseousAshID)
            ;

        public int GetEncounteredValue(Guid OsseousAshID)
            => GetStatValue(ref Encountered, OsseousAshID)
            ;

        public int GetDefeatedValue(Guid OsseousAshID)
            => GetStatValue(ref Defeated, OsseousAshID)
            ;

        public int GetReclaimedValue(Guid OsseousAshID)
            => GetStatValue(ref Reclaimed, OsseousAshID)
            ;

        public BonesStat GetStat(ref BonesStatSet StatSet, Guid OsseousAshID)
            => (StatSet ??= new()).GetStat(OsseousAshID)
            ;

        public BonesStat GetEncountered(Guid OsseousAshID)
            => GetStat(ref Encountered, OsseousAshID)
            ;

        public BonesStat GetDefeated(Guid OsseousAshID)
            => GetStat(ref Defeated, OsseousAshID)
            ;

        public BonesStat GetReclaimed(Guid OsseousAshID)
            => GetStat(ref Reclaimed, OsseousAshID)
            ;

        public int GetStatTotal(ref BonesStatSet StatSet)
            => (StatSet ??= new()).GetStatTotal()
            ;

        public int GetEncounteredTotal()
            => GetStatTotal(ref Encountered)
            ;

        public int GetDefeatedTotal()
            => GetStatTotal(ref Defeated)
            ;

        public int GetReclaimedTotal()
            => GetStatTotal(ref Reclaimed)
            ;

        public double GetPercentOfStat(ref BonesStatSet StatSet, Guid OsseousAshID)
            => GetStatTotal(ref StatSet) != 0
            ? GetStatValue(ref StatSet, OsseousAshID) / GetStatTotal(ref StatSet)
            : 0
            ;

        public double GetPercentOfEncountered(Guid OsseousAshID)
            => GetPercentOfStat(ref Encountered, OsseousAshID)
            ;

        public double GetPercentOftDefeated(Guid OsseousAshID)
            => GetPercentOfStat(ref Defeated, OsseousAshID)
            ;

        public double GetPercentOfReclaimed(Guid OsseousAshID)
            => GetPercentOfStat(ref Reclaimed, OsseousAshID)
            ;

        private string GetPercentageString(double Amount)
            => $"{Amount * 100:0.0}%";

        public string GetBlurb(string RegentName)
        {
            var sB = Event.NewStringBuilder();
            var oAID = OsseousAsh.Config?.ID ?? Guid.Empty;
            bool haveID = oAID != Guid.Empty;

            RegentName = (RegentName ?? MissingLunarRegent).StartReplace().ToString();

            sB.Append(RegentName.Capitalize()).Append(" has been encountered ").AppendRules(GetEncounteredTotal().Things("time")).Append(".");
            
            if (haveID)
                sB.AppendLine().Append("You've personally encountered their bones ").AppendRules(GetEncounteredValue(oAID).Things("time")).Append(", ")
                    .Append("which is ").AppendRules(GetPercentageString(GetPercentOfEncountered(oAID))).Append(" of the times they've been encountered.")
                    .AppendLine();
            
            sB.AppendLine().Append("They have been made to relive their demise in ").AppendRules(GetDefeatedTotal().Things("run")).Append(" since their first.");
            
            if (haveID)
                sB.AppendLine().Append("On ").AppendRules(GetDefeatedValue(oAID).Things("separate occasion")).Append(", ")
                    .Append(RegentName).Append(" has attempted to reclaim your run and been defeated, ")
                    .Append("accounting for ").AppendRules(GetPercentageString(GetPercentOftDefeated(oAID))).Append(" of their total defeats.")
                    .AppendLine();
            
            sB.AppendLine().AppendRules(GetReclaimedTotal().Things("time")).Append(", ").Append(RegentName).Append(" has reclaimed a run they've found themselves in.");
            
            if (haveID)
                sB.AppendLine().Append(RegentName.Capitalize()).Append(" has successfully reclaimed ").AppendRules(GetReclaimedValue(oAID).ToString()).Append(" of your runs, ")
                    .Append("representing ").AppendRules(GetPercentageString(GetPercentOfEncountered(oAID))).Append(" of the total runs they've reclaimed.")
                    .AppendLine();

            return Event.FinalizeString(sB);
        }
    }

    public static class BonesStatsExtensions
    {
        public static StringBuilder AppendBonesStatsBlurb(this StringBuilder SB, BonesStats BonesStats, string RegentName)
            => SB.Append(BonesStats?.GetBlurb(RegentName))
            ;
    }
}
