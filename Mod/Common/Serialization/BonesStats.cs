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

        [JsonProperty(nameof(LastEncountered))]
        protected long _LastEncountered;

        [JsonIgnore]
        public DateTime LastEncountered => new(_LastEncountered);

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

            int totalEncounters = GetEncounteredTotal();

            if (totalEncounters > 0)
            {
                sB.Append(RegentName.Capitalize()).Append(" has been encountered ").AppendRule(totalEncounters.Things("time"));

                if (_LastEncountered > 0)
                    sB.Append(", and was last encountered").AppendRule(LastEncountered.TimeAgo()).Append(" ago.");
                else
                    sB.Append(".");

                int totalDefeats = GetDefeatedTotal();
                int totalReclamations = GetReclaimedTotal();
                int playerEncounters = 0;

                if (haveID)
                {
                    playerEncounters = GetEncounteredValue(oAID);

                    if (playerEncounters > 0)
                    {
                        string percentEncountered = GetPercentageString(GetPercentOfEncountered(oAID));
                        sB.AppendLine().Append("You've personally encountered them in ").AppendRule(playerEncounters.Things("different run")).Append(", ")
                            .Append("which is ").AppendRule(percentEncountered).Append(" of the times they've ever been encountered.")
                            .AppendLine();
                    }
                    else
                        sB.AppendLine().Append("You are yet to encounter them.");
                }

                if (totalDefeats > 0)
                {
                    sB.AppendLine().Append("They have been made to relive their demise in ").AppendRule(totalDefeats.Things("run")).Append(" since their first.");

                    if (haveID)
                    {
                        int playerTriumphs = GetDefeatedValue(oAID);
                        if (playerTriumphs > 0)
                        {
                            if (playerTriumphs > 3
                                && playerTriumphs == playerEncounters)
                            {
                                sB.AppendLine().Append("Bane of ").Append(RegentName).Append(" that you are, you have defeated them ").AppendRule("every time")
                                    .Append(" they have attempted to reclaim a run of yours; ").Append(playerTriumphs.Things("total time")).Append(", ");
                            }
                            else
                            {
                                sB.AppendLine().Append("On ").AppendRule(playerTriumphs.Things("separate occasion")).Append(", ")
                                    .Append(RegentName).Append(" was defeated in an attempt to reclaim your run, ");
                            }
                            string percentTriumphed = GetPercentageString(GetPercentOftDefeated(oAID));
                            sB.Append("accounting for ").AppendRule(percentTriumphed).Append(" of their total defeats.")
                                .AppendLine();
                        }
                        else
                            sB.AppendLine().Append("They have never faced defeat at your hand.");
                    }
                }
                else
                    sB.AppendLine().Append("They have ").AppendRule("never").Append(" been defeated.");
                

                if (totalReclamations > 0)
                {
                    sB.AppendLine().AppendRule(totalReclamations.Things("time")).Append(", ").Append(RegentName).Append(" has reclaimed a run in which they've found themselves.");

                    if (haveID)
                    {
                        int playerDefeats = GetReclaimedValue(oAID);
                        if (playerDefeats > 0)
                        {
                            if (playerDefeats > 3
                                && playerDefeats == playerEncounters)
                            {
                                sB.AppendLine().Append("Woe is the name ").Append(RegentName).Append("! They have reclaimed ").AppendRule("every run")
                                    .Append(" you've encountered them within; A total of ").Append(playerDefeats.Things("time")).Append(", ");
                            }
                            else
                            {
                                sB.AppendLine().Append(RegentName.Capitalize()).Append(" has successfully reclaimed ")
                                    .AppendRule(playerDefeats.ToString()).Append(" of your runs, ");
                            }
                            string percentDefeats = GetPercentageString(GetPercentOfEncountered(oAID));
                            sB.Append("representing ").AppendRule(percentDefeats).Append(" of the total runs they've reclaimed.")
                                .AppendLine();
                        }
                        else
                            sB.AppendLine().Append("They have never reclaimed a run of yours.");
                    }
                }
                else
                    sB.AppendLine().Append("They have ").AppendRule("never").Append(" reclaimed a run as their own.");
            }
            else
                sB.Append(RegentName.Capitalize()).Append(" has ").AppendRule("never").Append(" been encountered").Append(".");

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
