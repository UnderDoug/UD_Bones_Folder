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
using XRL.Collections;
using Newtonsoft.Json.Linq;
using XRL.World.Text;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class BonesStats : StringMap<BonesStatSet>, IDisposable
    {
        public class KVPArrayConverter : JsonConverter<KeyValuePair<string, BonesStatSet>[]>
        {

            [ThreadStatic]
            private static bool _CannotRead;

            private bool CannotRead
            {
                get => _CannotRead;
                set => _CannotRead = value;
            }

            [ThreadStatic]
            private static bool _CannotWrite;

            private bool CannotWrite
            {
                get => _CannotWrite;
                set => _CannotWrite = value;
            }

            public override bool CanRead => !CannotRead;
            public override bool CanWrite => !CannotWrite;

            public KVPArrayConverter()
                : base()
            { }

            public override KeyValuePair<string, BonesStatSet>[] ReadJson(
                JsonReader Reader,
                Type ObjectType,
                KeyValuePair<string, BonesStatSet>[] ExistingValue,
                bool HasExistingValue,
                JsonSerializer Serializer
                )
            {
                try
                {
                    using (new Utils.PushValue<bool>(true, () => CannotRead, v => CannotRead = v))
                    {
                        if (Reader.TokenType == JsonToken.StartObject
                            && JObject.Load(Reader) is JObject jObject)
                        {
                            using var stats = PooledList<KeyValuePair<string, BonesStatSet>>.Get();
                            foreach ((var label, var token) in jObject)
                                if ((token as JArray)?.ToObject<BonesStat[]>() is BonesStat[] bonesStatArray)
                                    stats.Add(new(label, new(bonesStatArray)));

                            ExistingValue = new KeyValuePair<string, BonesStatSet>[stats.Count];
                            Array.Copy(stats.ToArray(), ExistingValue, stats.Count);
                            return ExistingValue;
                        }
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(KVPArrayConverter)} failed during {nameof(ReadJson)} -> {nameof(Utils.PushValue<bool>)}, {nameof(CannotRead)}", x);
                }
                return ExistingValue ?? new KeyValuePair<string, BonesStatSet>[0];
            }

            public override void WriteJson(
                JsonWriter Writer,
                KeyValuePair<string, BonesStatSet>[] Value,
                JsonSerializer Serializer
                )
            {
                if (Value == null)
                {
                    Serializer.Serialize(Writer, null);
                    return;
                }
                try
                {
                    using (new Utils.PushValue<bool>(true, () => CannotWrite, v => CannotWrite = v))
                    {
                        var stats = new JObject();

                        foreach ((var label, var value) in Value.IteratorSafe())
                        {
                            var array = (value ?? new()).ToArray();
                            var jArray = new JArray(array);
                            stats.Add(label, jArray);
                        }
                        Serializer.Serialize(Writer, stats);
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(KVPArrayConverter)} failed during {nameof(WriteJson)} -> {nameof(Utils.PushValue<bool>)}, {nameof(CannotWrite)}", x);
                }
            }
        }

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

        [JsonProperty]
        public string ID;

        [JsonProperty(nameof(LastEncountered))]
        protected long _LastEncountered;

        [JsonIgnore] public DateTime LastEncountered
        {
            get => new DateTime(_LastEncountered).ToLocalTime();
            set => _LastEncountered = value.ToUniversalTime().Ticks;
        }

        #region Official Stats

        // [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        [JsonIgnore] public BonesStatSet Encountered
        {
            get => this[nameof(Encountered)];
            set => this[nameof(Encountered)] = value;
        }

        // [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        [JsonIgnore] public BonesStatSet Defeated
        {
            get => this[nameof(Defeated)];
            set => this[nameof(Defeated)] = value;
        }

        // [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        [JsonIgnore] public BonesStatSet Reclaimed
        {
            get => this[nameof(Reclaimed)];
            set => this[nameof(Reclaimed)] = value;
        }

        // [JsonConverter(typeof(BonesStatSet.SetArrayConverter))]
        [JsonIgnore] public BonesStatSet Broken
        {
            get => this[nameof(Broken)];
            set => this[nameof(Broken)] = value;
        }

        #endregion

        // [JsonConverter(typeof(KVPArrayConverter))]
        [JsonProperty]
        private Dictionary<string, BonesStatSet> Stats
        {
            get => new(this);
            set
            {
                Clear();
                EnsureCapacity(value?.Count ?? 0);
                foreach (var pair in value.IteratorSafe())
                    this[pair.Key] = pair.Value;
            }
        }

        [JsonIgnore]
        public Dictionary<string, Func<Guid, bool>> IncrementMethodByByName => new()
        {
            { nameof(Encountered), IncrementEncountered },
            { nameof(Defeated), IncrementDefeated },
            { nameof(Reclaimed), IncrementReclaimed },
            { nameof(Broken), IncrementBroken },
        };

        public BonesStats()
            : base()
        {
            ID = The.Game?.GameID;
            Encountered = new();
            Defeated = new();
            Reclaimed = new();
            Broken = new();
        }

        public BonesStats(BonesStats Source)
            : this()
        {
            ID = Source?.ID;
            LastEncountered = Source.LastEncountered;
            EnsureCapacity(Source.Count);
            foreach ((var label, var statSet) in Source.IteratorSafe())
                this[label] = new(statSet);
        }

        public BonesStatSet RequireSet(string Name)
        {
            if (!ContainsKey(Name))
                this[Name] = new();

            return this[Name];
        }

        public bool IncrementStat(string Name, Guid UserID)
            => RequireSet(Name).IncrementStat(UserID);

        public bool IncrementEncountered(Guid UserID)
        {
            if (IncrementStat(nameof(Encountered), UserID))
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
            => IncrementStat(nameof(Defeated), UserID)
            ;

        public bool IncrementDefeated()
            => IncrementDefeated(UserID)
            ;

        public bool IncrementReclaimed(Guid UserID)
            => IncrementStat(nameof(Reclaimed), UserID)
            ;

        public bool IncrementReclaimed()
            => IncrementReclaimed(UserID)
            ;

        public bool IncrementBroken(Guid UserID)
            => IncrementStat(nameof(Broken), UserID)
            ;

        public bool IncrementBroken()
            => IncrementBroken(UserID)
            ;

        public int GetStatValue(string Name, Guid UserID)
            => RequireSet(Name).GetStatValue(UserID)
            ;

        public int GetEncounteredValue(Guid UserID)
            => GetStatValue(nameof(Encountered), UserID)
            ;

        public int GetDefeatedValue(Guid UserID)
            => GetStatValue(nameof(Defeated), UserID)
            ;

        public int GetReclaimedValue(Guid UserID)
            => GetStatValue(nameof(Reclaimed), UserID)
            ;

        public int GetBrokenValue(Guid UserID)
            => GetStatValue(nameof(Broken), UserID)
            ;

        public BonesStat GetStat(string Name, Guid UserID)
            => RequireSet(Name).GetStat(UserID)
            ;

        public BonesStat GetEncountered(Guid UserID)
            => GetStat(nameof(Encountered), UserID)
            ;

        public BonesStat GetDefeated(Guid UserID)
            => GetStat(nameof(Defeated), UserID)
            ;

        public BonesStat GetReclaimed(Guid UserID)
            => GetStat(nameof(Reclaimed), UserID)
            ;

        public BonesStat GetBroken(Guid UserID)
            => GetStat(nameof(Broken), UserID)
            ;

        public int GetStatTotal(string Name)
            => RequireSet(Name).GetStatTotal()
            ;

        public int GetEncounteredTotal()
            => GetStatTotal(nameof(Encountered))
            ;

        public int GetDefeatedTotal()
            => GetStatTotal(nameof(Defeated))
            ;

        public int GetReclaimedTotal()
            => GetStatTotal(nameof(Reclaimed))
            ;

        public int GetBrokenTotal()
            => GetStatTotal(nameof(Broken))
            ;

        public double GetPercentOfStat(string Name, Guid UserID)
            => GetStatTotal(Name) is int statTotal
                && statTotal != 0
            ? GetStatValue(Name, UserID) / (double)statTotal
            : 0
            ;

        public double GetPercentOfEncountered(Guid UserID)
            => GetPercentOfStat(nameof(Encountered), UserID)
            ;

        public double GetPercentOfDefeated(Guid UserID)
            => GetPercentOfStat(nameof(Defeated), UserID)
            ;

        public double GetPercentOfReclaimed(Guid UserID)
            => GetPercentOfStat(nameof(Reclaimed), UserID)
            ;

        public double GetPercentOfBroken(Guid UserID)
            => GetPercentOfStat(nameof(Broken), UserID)
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

        public void Dispose()
        {
            ID = null;
            _LastEncountered = 0L;

            foreach ((var _, var statSet) in this)
                statSet?.Dispose();

            Clear();
        }
    }

    public static class BonesStatsExtensions
    {
        public static StringBuilder AppendBonesStatsBlurb(this StringBuilder SB, BonesStats BonesStats, string RegentName)
            => SB.Append(BonesStats?.GetBlurb(RegentName))
            ;

        public static TextBuilder AppendBonesStatsBlurb(this TextBuilder TB, BonesStats BonesStats, string RegentName)
            => TB.Append(BonesStats?.GetBlurb(RegentName))
            ;
    }
}
