using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using XRL;
using XRL.Language;
using XRL.World;
using XRL.World.Parts;

using UD_Bones_Folder.Mod.OsseousAsh;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesStats
    {
        private static Configuration Config => Configuration.Instance;
        public static Guid UserID => Config?.ID ?? Guid.Empty;
        public static string Handle => Config?.Handle;
        public static string MissingLunarRegent => "this =LunarShader:Moon Sovran:*=, lost to time,";

        public static string[] StatNames = new string[4]
        {
            nameof(Encountered),
            nameof(Defeated),
            nameof(Reclaimed),
            nameof(Broken),
        };

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

        public bool IncrementStat(ref BonesStatSet StatSet, Guid UserID)
            => (StatSet ??= new()).IncrementStat(UserID);

        public bool IncrementEncountered(Guid UserID)
        {
            if (IncrementStat(ref Encountered, UserID))
            {
                LastEncountered = DateTime.Now.ToUniversalTime();
                return true;
            }
            return false;
        }

        public bool IncrementEncountered()
            => IncrementEncountered(UserID)
            ;

        public bool IncrementDefeated(Guid UserID)
            => IncrementStat(ref Defeated, UserID)
            ;

        public bool IncrementDefeated()
            => IncrementDefeated(UserID)
            ;

        public bool IncrementReclaimed(Guid UserID)
            => IncrementStat(ref Reclaimed, UserID)
            ;

        public bool IncrementReclaimed()
            => IncrementReclaimed(UserID)
            ;

        public bool IncrementBroken(Guid UserID)
            => IncrementStat(ref Broken, UserID)
            ;

        public bool IncrementBroken()
            => IncrementBroken(UserID)
            ;

        public int GetStatValue(ref BonesStatSet StatSet, Guid UserID)
            => (StatSet ??= new()).GetStatValue(UserID)
            ;

        public int GetEncounteredValue(Guid UserID)
            => GetStatValue(ref Encountered, UserID)
            ;

        public int GetDefeatedValue(Guid UserID)
            => GetStatValue(ref Defeated, UserID)
            ;

        public int GetReclaimedValue(Guid UserID)
            => GetStatValue(ref Reclaimed, UserID)
            ;

        public int GetBrokenValue(Guid UserID)
            => GetStatValue(ref Broken, UserID)
            ;

        public BonesStat GetStat(ref BonesStatSet StatSet, Guid UserID)
            => (StatSet ??= new()).GetStat(UserID)
            ;

        public BonesStat GetEncountered(Guid UserID)
            => GetStat(ref Encountered, UserID)
            ;

        public BonesStat GetDefeated(Guid UserID)
            => GetStat(ref Defeated, UserID)
            ;

        public BonesStat GetReclaimed(Guid UserID)
            => GetStat(ref Reclaimed, UserID)
            ;

        public BonesStat GetBroken(Guid UserID)
            => GetStat(ref Broken, UserID)
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

        public double GetPercentOfStat(ref BonesStatSet StatSet, Guid UserID)
            => GetStatTotal(ref StatSet) is int statTotal
                && statTotal != 0
            ? GetStatValue(ref StatSet, UserID) / (double)statTotal
            : 0
            ;

        public double GetPercentOfEncountered(Guid UserID)
            => GetPercentOfStat(ref Encountered, UserID)
            ;

        public double GetPercentOfDefeated(Guid UserID)
            => GetPercentOfStat(ref Defeated, UserID)
            ;

        public double GetPercentOfReclaimed(Guid UserID)
            => GetPercentOfStat(ref Reclaimed, UserID)
            ;

        public double GetPercentOfBroken(Guid UserID)
            => GetPercentOfStat(ref Broken, UserID)
            ;

        private string GetPercentageString(double Amount)
            => $"{Amount * 100:#,##0.0}%";

        public string GetBlurb(string RegentName)
        {
            var sB = Event.NewStringBuilder();
            var userID = UserID;
            bool haveID = userID != Guid.Empty;

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
                    playerEncounters = GetEncounteredValue(userID);

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
                            string percentEncountered = GetPercentageString(GetPercentOfEncountered(userID));
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
                        int playerTriumphs = GetDefeatedValue(userID);
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
                            string percentTriumphed = GetPercentageString(GetPercentOfDefeated(userID));
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
                        int playerDefeats = GetReclaimedValue(userID);
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
                            string percentDefeats = GetPercentageString(GetPercentOfReclaimed(userID));
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
                        int playerBreaks = GetBrokenValue(userID);
                        if (playerBreaks > 0)
                        {
                            if (playerBreaks > 3
                                && playerBreaks == playerEncounters)
                            {
                                sB.AppendLine()
                                    .AppendLine().Append("Mystical is the aura of ").Append(Handle).Append(", for ")
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
                            string percentBroken = GetPercentageString(GetPercentOfBroken(userID));
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
