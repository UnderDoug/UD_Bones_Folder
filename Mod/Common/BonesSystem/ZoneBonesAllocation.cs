using System;
using System.Collections.Generic;
using System.Text;

using Qud.API;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.ZoneBuilders;
using XRL.World.ZoneParts;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [HasModSensitiveStaticCache]
    [Serializable]
    public class ZoneBonesAllocation : IComposite
    {
        public static BonesManager BonesManager => BonesManager.System;

        public static ZoneBonesAllocationSpec WantsNoneSpec;

        public static ZoneBonesAllocationSpec WantsPartySpec;

        public static ZoneBonesAllocationSpec WantsBubbleSpec;

        public enum AllocationTypes
        {
            None,
            Blocked,
            Party,
            Bubble,
            Zone,
        }

        public string ZoneID;
        public string BonesID;
        public AllocationTypes Type;

        [NonSerialized]
        public List<IZonePart> ZoneParts;

        [NonSerialized]
        public ZoneBuilderCollection ZoneBuilders;

        [NonSerialized]
        public List<IBaseJournalEntry> JournalEntries;

        public ZoneBonesAllocation()
        { }

        public ZoneBonesAllocation(
            string ZoneID,
            string BonesID,
            AllocationTypes Type = AllocationTypes.None
        ) : this()
        {
            this.ZoneID = ZoneID;
            this.BonesID = BonesID;
            this.Type = Type;
        }

        public ZoneBonesAllocation(
            string ZoneID,
            AllocationTypes Type = AllocationTypes.None
        ) : this(ZoneID, null, Type)
        {
        }

        protected ZoneBonesAllocation(
            Zone Zone,
            string BonesID,
            AllocationTypes Type = AllocationTypes.None
        ) : this(Zone?.ZoneID, BonesID, Type)
        { }

        protected ZoneBonesAllocation(
            Zone Zone,
            AllocationTypes Type = AllocationTypes.None
        ) : this(Zone, null, Type)
        { }

        public bool IsZone(Zone Zone)
            => Zone?.ZoneID is string zoneID
            && ZoneID == zoneID
            ;

        [ModSensitiveCacheInit]
        public static void Init()
        {
            Loading.LoadTask($"Caching Zone Bones Allocation Specs...", CacheZoneBonesSpecs);
        }

        public static bool CheckInit()
        {
            if (WantsNoneSpec == null
                || WantsPartySpec == null
                || WantsBubbleSpec == null)
                Init();

            if (WantsNoneSpec == null
                || WantsPartySpec == null
                || WantsBubbleSpec == null)
            {
                Utils.Warn($"{nameof(ZoneBonesAllocation)}.{nameof(Init)} failed to init {nameof(ZoneBonesAllocationSpec)}s during {nameof(CheckInit)}");
                return false;
            }

            return true;
        }

        public static void CacheZoneBonesSpecs()
        {
            WantsNoneSpec ??= new ZoneBonesAllocationSpec();
            WantsNoneSpec.ZoneProperties ??= new StringSet();
            WantsNoneSpec.ZoneProperties.AddRange(
                Range: new string[]
                {
                    ZoneBonesAllocationSpec.LoadTypeProp_None,
                });
            WantsNoneSpec.ZoneParts ??= new StringSet();
            WantsNoneSpec.ZoneBuilders ??= new StringSet();
            WantsNoneSpec.MapNoteAttributes ??= new StringSet();
            WantsNoneSpec.MapNoteCategories ??= new StringSet();
            WantsNoneSpec.InitConditionDelegates(AllocationTypes.None);

            WantsPartySpec ??= new ZoneBonesAllocationSpec();
            WantsPartySpec.ZoneProperties ??= new StringSet();
            WantsPartySpec.ZoneProperties.AddRange(
                Range: new string[]
                {
                    ZoneBonesAllocationSpec.LoadTypeProp_Party,
                });
            WantsPartySpec.ZoneParts ??= new StringSet();
            WantsPartySpec.ZoneBuilders ??= new StringSet();
            WantsPartySpec.ZoneBuilders.AddRange(
                Range: new string[]
                {
                    nameof(IsCheckpoint),
                    nameof(MapBuilder),
                    nameof(AddLocationFinder),
                    nameof(InteractWithAnObjectDynamicQuestTemplate_FabricateQuestGiver),
                    nameof(FindASpecificSiteDynamicQuestTemplate_FabricateQuestGiver),
                    nameof(Waterway),
                    nameof(IdolFight),
                    nameof(MinorRazedGoatfolkVillage),
                    nameof(WildWatervineMerchant),
                    nameof(SmokingAreaE),
                    nameof(SmokingAreaS),
                    nameof(SmokingAreaN),
                    nameof(RazedGoatfolkVillage),
                    nameof(FungalTrailKlanqHut),
                    nameof(FungalTrailBuilder),
                    nameof(PlaceAClam),
                    nameof(ShugBurrowBuilder),
                });
            WantsPartySpec.MapNoteAttributes ??= new StringSet();
            WantsPartySpec.MapNoteAttributes.AddRange(
                Range: new string[]
                {
                    "nephilim",
                    "special",
                    "oddity",

                    // previously "none"
                    "oboroqoru",
                    "rermadon",
                    "qasqon",
                    "shugruith",
                    "agolgot",
                    "bethsaida",
                });
            WantsPartySpec.MapNoteCategories ??= new StringSet();
            WantsPartySpec.MapNoteCategories.AddRange(
                Range: new string[]
                {
                    "Settlements",
                    "Oddities",
                    "Lairs",
                    "Ruins",
                });
            WantsPartySpec.InitConditionDelegates(AllocationTypes.Party);

            WantsBubbleSpec ??= new ZoneBonesAllocationSpec();
            WantsBubbleSpec.ZoneProperties ??= new StringSet();
            WantsBubbleSpec.ZoneProperties.AddRange(
                Range: new string[]
                {
                    "SkipTerrainBuilders",
                    ZoneBonesAllocationSpec.LoadTypeProp_Bubble,
                });
            WantsBubbleSpec.ZoneParts ??= new StringSet();
            WantsBubbleSpec.ZoneParts.AddRange(
                Range: new string[]
                {
                    nameof(AmbientStabilization),
                });
            WantsBubbleSpec.ZoneBuilders ??= new StringSet();
            WantsBubbleSpec.ZoneBuilders.AddRange(
                Range: new string[]
                {
                    nameof(Rustwells),
                    nameof(RedrockOutcrop),
                    nameof(Redrock),
                    nameof(SixDayTents),
                    nameof(SultanDungeon),
                    nameof(PlaceRelicBuilder),
                    nameof(CatacombsPublicus),
                    nameof(CryptOfLandlords),
                    nameof(CryptOfWarriors),
                });
            WantsBubbleSpec.MapNoteAttributes ??= new StringSet();
            WantsBubbleSpec.MapNoteAttributes.AddRange(
                Range: new string[]
                {
                    "historic",
                    "biome",
                });
            WantsBubbleSpec.MapNoteCategories ??= new StringSet();
            WantsBubbleSpec.MapNoteCategories.AddRange(
                Range: new string[]
                {
                    "Artifacts",
                    "Ruins with Becoming Nooks",
                    "Historic Sites",
                });
            WantsBubbleSpec.InitConditionDelegates(AllocationTypes.Bubble);
        }

        public static bool GetZoneIDSeededChanceForEncounter(Zone Z)
            => Stat.SeededRandom($"{BonesManager.SeededRandomPrefix}:{Z.ZoneID}", 1, Const.MYRIAD) <= BonesManager.ChancePermyriadForBones
            ;

        public static ZoneBonesAllocation GetForZone(Zone Zone, string BonesID = null)
        {
            if (Zone == null)
                return null;

            if (Zone.IsWorldMap())
                return new ZoneBonesAllocation(Zone);

            var allocation = new ZoneBonesAllocation(Zone, BonesID, AllocationTypes.None);
            if (GetZoneIDSeededChanceForEncounter(Zone))
                allocation.AssignType(Zone);

            if (allocation.Type == AllocationTypes.None)
                allocation.BonesID = null;

            return allocation;
        }

        public void AssignType(Zone Zone)
        {
            if (CheckInit())
            {
                string catchFlag = "none (weird error)";
                try
                {
                    Utils.Log($"{nameof(ZoneBonesAllocation)}.{nameof(GetForZone)}({nameof(Zone)}: {Zone.ZoneID}, {nameof(BonesID)}: {BonesID ?? "pre-alloc"})");

                    InitZoneParts(Zone);
                    InitZoneBuilders(Zone);
                    InitZoneMapNotes(Zone);

                    Type = AllocationTypes.Zone;

                    catchFlag = nameof(ZoneBonesAllocationSpec.LoadTypeProp_Zone);
                    if (Zone.HasZoneProperty(ZoneBonesAllocationSpec.LoadTypeProp_Zone))
                    {
                        return;
                    }

                    catchFlag = nameof(ZoneBonesAllocationSpec.LoadTypeProp_Bubble);
                    if (Zone.HasZoneProperty(ZoneBonesAllocationSpec.LoadTypeProp_Bubble))
                    {
                        Type = AllocationTypes.Bubble;
                        return;
                    }

                    catchFlag = nameof(ZoneBonesAllocationSpec.LoadTypeProp_Party);
                    if (Zone.HasZoneProperty(ZoneBonesAllocationSpec.LoadTypeProp_Party))
                    {
                        Type = AllocationTypes.Party;
                        return;
                    }

                    catchFlag = nameof(ZoneBonesAllocationSpec.LoadTypeProp_None);
                    if (Zone.HasZoneProperty(ZoneBonesAllocationSpec.LoadTypeProp_None))
                    {
                        Type = AllocationTypes.None;
                        return;
                    }

                    if (!(catchFlag = nameof(WantsNoneSpec)).IsNullOrEmpty()
                        && WantsNoneSpec.MeetsSpec(Zone, this, AllocationTypes.None))
                    {
                        Type = AllocationTypes.None;
                        return;
                    }

                    if (!(catchFlag = nameof(WantsPartySpec)).IsNullOrEmpty()
                        && WantsPartySpec.MeetsSpec(Zone, this, AllocationTypes.Party))
                    {
                        Type = AllocationTypes.Party;
                        return;
                    }

                    if (!(catchFlag = nameof(WantsBubbleSpec)).IsNullOrEmpty()
                        && WantsBubbleSpec.MeetsSpec(Zone, this, AllocationTypes.Bubble))
                    {
                        Type = AllocationTypes.Bubble;
                        return;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to check {nameof(Zone)} against spec {catchFlag}, forcing allocation to {nameof(AllocationTypes)}.{AllocationTypes.Party}", x);
                    Type = AllocationTypes.Party;
                }
                finally
                {
                    ClearZoneParts();
                    ClearZoneBuilders();
                    ClearZoneMapNotes();
                    Utils.Log($"{1.Indent()}{Zone.ZoneID}, {nameof(Type)}: {Type}");
                }
            }
        }

        public override string ToString()
            => $"{ZoneID}::{Type}{(BonesID.IsNullOrEmpty() ? null : $"{{{BonesID}}}")}";

        public bool IsNoBones()
            => Type == AllocationTypes.None
            ;

        public bool IsBlocked()
            => Type == AllocationTypes.Blocked
            ;

        public bool WantsBones()
            => !IsNoBones()
            && !IsBlocked()
            ;

        public bool HasAssignedBones()
            => !IsNoBones()
            && !IsBlocked()
            && !BonesID.IsNullOrEmpty()
            ;

        public void SetBlocked()
            => Type = AllocationTypes.Blocked
            ;

        protected void InitZoneParts(Zone Zone)
        {
            ZoneParts = new();
            if (!Zone.Parts.IsNullOrEmpty())
                ZoneParts.AddRange(Zone.Parts);

            Utils.Log($"{1.Indent()}{nameof(ZoneParts)}: {(ZoneParts?.Count)?.ToString() ?? "no parts"}");
            foreach (var part in ZoneParts.IteratorSafe())
                Utils.Log($"{2.Indent()}: {part.Name}");
        }

        protected void ClearZoneParts()
        {
            ZoneParts?.Clear();
            ZoneParts = null;
        }

        protected void InitZoneBuilders(Zone Zone)
        {
            ZoneBuilders = new ZoneBuilderCollection();

            if (The.ZoneManager.ZoneBuilders.TryGetValue(Zone.ZoneID, out ZoneBuilderCollection builders))
                ZoneBuilders.AddRange(builders);

            var request = new ZoneRequest(Zone.ZoneID)
            {
                World = Zone.ResolveWorldBlueprint()
            };
            var zoneBlueprints = request.World?.GetBlueprintsFor(request);

            if (!Zone.HasZoneProperty("SkipTerrainBuilders"))
                foreach (var zoneBlueprint in zoneBlueprints.IteratorSafe())
                    if (!zoneBlueprint.Builders.IsReadOnlyNullOrEmpty())
                        ZoneBuilders.AddRange(zoneBlueprint.Builders);

            Utils.Log($"{1.Indent()}{nameof(ZoneBuilders)}: {(ZoneBuilders?.Count)?.ToString() ?? "no collection"}");
            foreach (var builder in ZoneBuilders.IteratorSafe())
                Utils.Log($"{2.Indent()}: {builder.Class}");
        }

        protected void ClearZoneBuilders()
        {
            ZoneBuilders?.Clear();
            ZoneBuilders = null;
        }

        protected void InitZoneMapNotes(Zone Zone)
        {
            JournalEntries = new(JournalAPI.GetMapNotesForZone(Zone.ZoneID).IteratorSafe());
            Utils.Log($"{1.Indent()}{nameof(JournalEntries)}: {JournalEntries.Count}");
        }

        protected void ClearZoneMapNotes()
        {
            JournalEntries?.Clear();
            JournalEntries = null;
        }
    }
}
