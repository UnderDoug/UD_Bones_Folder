using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using XRL;
using XRL.Language;
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
        public DateTime LastEncountered
        {
            get => new DateTime(_LastEncountered).ToLocalTime();
            set => _LastEncountered = value.ToUniversalTime().Ticks;
        }

        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Encountered;
        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Defeated;
        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Reclaimed;
        [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        public BonesStatSet Broken;

        public BonesStats()
        {
            Encountered = new();
            Defeated = new();
            Reclaimed = new();
            Broken = new();
        }

        public BonesStats(
            BonesStatSet Encountered,
            BonesStatSet Defeated,
            BonesStatSet Reclaimed,
            BonesStatSet Broken
            )
        {

            this.Encountered = Encountered?.Clone() ?? new();
            this.Defeated = Defeated?.Clone() ?? new();
            this.Reclaimed = Reclaimed?.Clone() ?? new();
            this.Broken = Broken?.Clone() ?? new();
        }

        public BonesStats(BonesStats Source)
            : this(Source?.Encountered, Source?.Defeated, Source?.Reclaimed, Source?.Broken)
        { }

        public bool IncrementStat(ref BonesStatSet StatSet, Guid OsseousAshID)
            => (StatSet ??= new()).IncrementStat(OsseousAshID);

        public bool IncrementEncountered(Guid OsseousAshID)
        {
            if (IncrementStat(ref Encountered, OsseousAshID))
            {
                LastEncountered = DateTime.Now.ToUniversalTime();
                return true;
            }
            return false;
        }

        public bool IncrementEncountered()
            => IncrementEncountered(OsseousAsh.Config.ID)
            ;

        public bool IncrementDefeated(Guid OsseousAshID)
            => IncrementStat(ref Defeated, OsseousAshID)
            ;

        public bool IncrementDefeated()
            => IncrementDefeated(OsseousAsh.Config.ID)
            ;

        public bool IncrementReclaimed(Guid OsseousAshID)
            => IncrementStat(ref Reclaimed, OsseousAshID)
            ;

        public bool IncrementReclaimed()
            => IncrementReclaimed(OsseousAsh.Config.ID)
            ;

        public bool IncrementBroken(Guid OsseousAshID)
            => IncrementStat(ref Broken, OsseousAshID)
            ;

        public bool IncrementBroken()
            => IncrementBroken(OsseousAsh.Config.ID)
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

        public int GetBrokenValue(Guid OsseousAshID)
            => GetStatValue(ref Broken, OsseousAshID)
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

        public BonesStat GetBroken(Guid OsseousAshID)
            => GetStat(ref Broken, OsseousAshID)
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

        public int GetBrokenTotal()
            => GetStatTotal(ref Broken)
            ;

        public double GetPercentOfStat(ref BonesStatSet StatSet, Guid OsseousAshID)
            => GetStatTotal(ref StatSet) is int statTotal
                && statTotal != 0
            ? GetStatValue(ref StatSet, OsseousAshID) / (double)statTotal
            : 0
            ;

        public double GetPercentOfEncountered(Guid OsseousAshID)
            => GetPercentOfStat(ref Encountered, OsseousAshID)
            ;

        public double GetPercentOfDefeated(Guid OsseousAshID)
            => GetPercentOfStat(ref Defeated, OsseousAshID)
            ;

        public double GetPercentOfReclaimed(Guid OsseousAshID)
            => GetPercentOfStat(ref Reclaimed, OsseousAshID)
            ;

        public double GetPercentOfBroken(Guid OsseousAshID)
            => GetPercentOfStat(ref Broken, OsseousAshID)
            ;

        private string GetPercentageString(double Amount)
            => $"{Amount * 100:#,##0.0}%";

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
                    sB.Append(", and was last encountered ").AppendRule(LastEncountered.TimeAgo("ago")).Append(".");
                else
                    sB.Append(".");

                int totalDefeats = GetDefeatedTotal();
                int totalReclamations = GetReclaimedTotal();
                int totalBroken = GetBrokenTotal();
                int playerEncounters = 0;

                if (haveID)
                {
                    playerEncounters = GetEncounteredValue(oAID);

                    if (playerEncounters > 0)
                    {
                        if (playerEncounters == totalEncounters)
                        {
                            if (totalBroken < totalEncounters)
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("As the focus of ").Append(Grammar.MakePossessive(RegentName)).Append(" obsession, ")
                                    .AppendRule("every time").Append(" they have attempted to reclaim a run, it has been one of yours.");
                            }
                            else
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("As the seeming cure to ").Append(Grammar.MakePossessive(RegentName)).Append(" affliction, ")
                                    .AppendRule("every time").Append(" they have found themselves in a run, it has been one of yours, and their fever has been broken.");
                            }
                        }
                        else
                        {
                            string percentEncountered = GetPercentageString(GetPercentOfEncountered(oAID));
                            sB.AppendLine()
                                .AppendLine().Append("You've personally encountered them in ")
                                .AppendRule(playerEncounters.Things("different run")).Append(", ")
                                .Append("which is ").AppendRule(percentEncountered).Append(" of the times they've ever been encountered.");
                        }
                    }
                    else
                        sB.AppendLine()
                            .AppendLine().Append("You are yet to encounter them.");
                }

                if (totalDefeats > 0)
                {
                    sB.AppendLine()
                        .AppendLine().Append("They have been made to relive their demise in ").AppendRule(totalDefeats.Things("run")).Append(" since their first.");

                    if (haveID)
                    {
                        int playerTriumphs = GetDefeatedValue(oAID);
                        if (playerTriumphs > 0)
                        {
                            if (playerTriumphs > 3
                                && playerTriumphs == playerEncounters)
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("Bane of ").Append(RegentName).Append(" that you are, you have defeated them ").AppendRule("every time")
                                    .Append(" they have attempted to reclaim a run of yours; ").Append(playerTriumphs.Things("total time")).Append(", ");
                            }
                            else
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("On ").AppendRule(playerTriumphs.Things("separate occasion")).Append(", ")
                                    .Append(RegentName).Append(" was defeated in an attempt to reclaim your run, ");
                            }
                            string percentTriumphed = GetPercentageString(GetPercentOfDefeated(oAID));
                            sB.Append("accounting for ").AppendRule(percentTriumphed).Append(" of their total defeats.");
                        }
                        else
                            sB.AppendLine()
                                .AppendLine().Append("They have never faced defeat at your hand.");
                    }
                }
                else
                    sB.AppendLine()
                        .AppendLine().Append("They have ").AppendRule("never").Append(" been defeated.");
                
                if (totalReclamations > 0)
                {
                    sB.AppendLine()
                        .AppendLine().AppendRule(totalReclamations.Things("time")).Append(", ").Append(RegentName).Append(" has reclaimed a run in which they've found themselves.");

                    if (haveID)
                    {
                        int playerDefeats = GetReclaimedValue(oAID);
                        if (playerDefeats > 0)
                        {
                            if (playerDefeats > 3
                                && playerDefeats == playerEncounters)
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("Woe is the name ").Append(RegentName).Append("! They have reclaimed ").AppendRule("every run")
                                    .Append(" you've encountered them within; A total of ").Append(playerDefeats.Things("time")).Append(", ");
                            }
                            else
                            {
                                sB.AppendLine()
                                    .AppendLine().Append(RegentName.Capitalize()).Append(" has successfully reclaimed ")
                                    .AppendRule(playerDefeats.ToString()).Append(" of your runs, ");
                            }
                            string percentDefeats = GetPercentageString(GetPercentOfReclaimed(oAID));
                            sB.Append("representing ").AppendRule(percentDefeats).Append(" of the total runs they've reclaimed.");
                        }
                        else
                            sB.AppendLine()
                            .AppendLine().Append("They have never reclaimed a run of yours.");
                    }
                }
                else
                    sB.AppendLine()
                        .AppendLine().Append("They have ").AppendRule("never").Append(" reclaimed a run as their own.");

                if (totalBroken > 0)
                {
                    sB.AppendLine()
                        .AppendLine().Append("By some bent of fate, ").Append(RegentName).Append(" has broken their fever ")
                        .AppendRule(totalBroken.Things("time")).Append(", and settled into a quieter life.");

                    if (haveID)
                    {
                        int playerBreaks = GetBrokenValue(oAID);
                        if (playerBreaks > 0)
                        {
                            if (playerBreaks > 3
                                && playerBreaks == playerEncounters)
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("Mystical is the aura of ").Append(OsseousAsh.Config.Handle).Append(", for ")
                                    .Append(Grammar.MakePossessive(RegentName)).Append(" fever has broken in ").AppendRule("every run")
                                    .Append(" of yours they've arrived in; ").Append(playerBreaks.Things("time")).Append(" in total, ");
                            }
                            else
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("In ").AppendRule(playerBreaks.ToString()).Append(" of your runs, ").Append(Grammar.MakePossessive(RegentName))
                                    .Append(" delusion was dispelled and their fever broken, ")
                                    ;
                            }
                            string percentBroken = GetPercentageString(GetPercentOfBroken(oAID));
                            sB.Append("making up ").AppendRule(percentBroken).Append(" of the worlds in which they've chosen a less dangerous path.");
                        }
                        else
                            sB.AppendLine()
                                .AppendLine().Append(RegentName.Capitalize()).Append(" remains convinced of your unimportance.");
                    }
                }/*
                else
                    sB.AppendLine()
                        .AppendLine().Append("They have ").AppendRule("never").Append(" reclaimed a run as their own.");*/
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
