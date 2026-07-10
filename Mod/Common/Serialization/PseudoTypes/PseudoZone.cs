using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.UI;

using XRL;
using XRL.Collections;
using XRL.Rules;
using XRL.World;
using XRL.World.AI.Pathfinding;
using XRL.World.Parts;
using XRL.World.ZoneParts;

namespace UD_Bones_Folder.Mod.Serialization.PseudoTypes
{
    [Serializable]
    public class PseudoZone : IComposite, IDisposable
    {
        public static XRL.Version MinVersion => new(0, 0, 3, 6);

        public const string EXTRACT_CONTEXT = "Extract";
        public const string RECLAIM_CONTEXT = "Reclaim";

        public bool WantFieldReflection => false;

        // Not Serialized
        public string BonesID;

        public string ZoneID;

        public ZoneRequest ZoneRequest => new(ZoneID);

        // Name
        public string BaseDisplayName;
        public string DisplayName;
        public string NameContext;
        public string DefiniteArticle;
        public string IndefiniteArticle;
        public bool HasProperName;
        public bool NamedByPlayer;
        public bool IncludeContextInZoneDisplay;
        public bool IncludeStratumInZoneDisplay;

        // Weather
        public bool HasWeather;
        public string WindDirections;
        public string WindDuration;
        public string WindSpeed;

        // Tier
        public int Tier;
        public int NewTier;

        // Terrain (for BonesSpec)
        public string ZoneTerrainType;
        public int RegionTier;
        public string TerrainTravelClass;

        public Dictionary<string, object> Properties;

        public IZonePart[] ZoneParts;

        public int Width;
        public int Height;

        /// <summary>
        /// Emulation of <see cref="Zone.Map"/> using <see cref="PseudoCell"/> instead, coords are [<see cref="Width"/> (<see cref="PseudoCell.X"/>)][<see cref="Height"/> (<see cref="PseudoCell.Y"/>)]
        /// </summary>
        public PseudoCell[][] Map;

        public LunarPartyPseudoAddresses LunarParty;

        public GameObject LunarRegent => GetAtAddress(LunarParty?.LunarRegent);

        // Not Serialized
        protected Dictionary<Cell, Rack<PseudoGameObject>> OriginalCells;
        protected Dictionary<GameObject, (string OriginalActive, string OriginalAbility, string OriginalGameID)> OriginalProps;

        public PseudoZone()
        { }

        #region Serialization
        #region Write
        #region Helpers
        protected void WriteNameData(SerializationWriter Writer)
        {
            Writer.WriteOptimized(BaseDisplayName);
            Writer.WriteOptimized(DisplayName);
            Writer.WriteOptimized(NameContext);
            Writer.WriteOptimized(DefiniteArticle);
            Writer.WriteOptimized(IndefiniteArticle);
            Writer.Write(HasProperName);
            Writer.Write(NamedByPlayer);
            Writer.Write(IncludeContextInZoneDisplay);
            Writer.Write(IncludeStratumInZoneDisplay);
        }

        protected void WriteWeatherData(SerializationWriter Writer)
        {
            Writer.Write(HasWeather);
            Writer.WriteOptimized(WindDirections);
            Writer.WriteOptimized(WindDuration);
            Writer.WriteOptimized(WindSpeed);
        }

        protected void WriteTierData(SerializationWriter Writer)
        {
            Writer.WriteOptimized(Tier);
            Writer.WriteOptimized(NewTier);
        }

        protected void WriteTerrainData(SerializationWriter Writer)
        {
            Writer.WriteOptimized(ZoneTerrainType);
            Writer.WriteOptimized(RegionTier);
            Writer.WriteOptimized(TerrainTravelClass);
        }

        protected void WriteProperties(SerializationWriter Writer)
        {
            if (Properties == null)
                Writer.WriteOptimized(-1);
            else
                Writer.WriteOptimized(Properties.Count);

            foreach ((string key, var value) in Properties.IteratorSafe())
            {
                Writer.WriteOptimized(key);
                Writer.WriteObject(value);
            }
        }

        public void WriteZonePart(SerializationWriter Writer, IZonePart ZonePart)
        {
            if (ZonePart == null)
                throw new ArgumentNullException(nameof(ZonePart));

            if (Writer == null)
                throw new ArgumentNullException(nameof(Writer));

            var block = Writer.StartBlock();
            var type = ZonePart.GetType();
            try
            {
                Writer.WriteTokenized(type);
                ZonePart.Write(null, Writer);
            }
            catch (Exception x)
            {
                block.Reset();
                MetricsManager.LogAssemblyError(type, "Skipping failed serialization of zone part '" + type.FullName + "': " + x);
            }
            finally
            {
                block.Dispose();
            }
        }

        protected void WriteZoneParts(SerializationWriter Writer)
        {
            if (ZoneParts == null)
                Writer.WriteOptimized(-1);
            else
                Writer.WriteOptimized(ZoneParts.Length);

            foreach (var zonePart in ZoneParts.IteratorSafe())
                WriteZonePart(Writer, zonePart);
        }

        protected void WritePseudoCells(SerializationWriter Writer)
        {
            Writer.WriteOptimized(Width);
            Writer.WriteOptimized(Height);

            foreach (var column in Map)
                foreach (var pseudoCell in column)
                    Writer.WriteComposite(pseudoCell);
        }
        #endregion

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteOptimized(ZoneID);

            WriteNameData(Writer);
            WriteWeatherData(Writer);
            WriteTierData(Writer);
            WriteTerrainData(Writer);

            WriteProperties(Writer);
            WriteZoneParts(Writer);

            WritePseudoCells(Writer);

            Writer.WriteComposite(LunarParty);
        }

        #endregion
        #region Read
        #region Helpers
        protected void ReadNameData(SerializationReader Reader)
        {
            BaseDisplayName = Reader.ReadOptimizedString();
            DisplayName = Reader.ReadOptimizedString();
            NameContext = Reader.ReadOptimizedString();
            DefiniteArticle = Reader.ReadOptimizedString();
            IndefiniteArticle = Reader.ReadOptimizedString();
            HasProperName = Reader.ReadBoolean();
            NamedByPlayer = Reader.ReadBoolean();
            IncludeContextInZoneDisplay = Reader.ReadBoolean();
            IncludeStratumInZoneDisplay = Reader.ReadBoolean();
        }

        protected void ReadWeatherData(SerializationReader Reader)
        {
            HasWeather = Reader.ReadBoolean();
            WindDirections = Reader.ReadOptimizedString();
            WindDuration = Reader.ReadOptimizedString();
            WindSpeed = Reader.ReadOptimizedString();
        }

        protected void ReadTierData(SerializationReader Reader)
        {
            Tier = Reader.ReadOptimizedInt32();
            NewTier = Reader.ReadOptimizedInt32();
        }

        protected void ReadTerrainData(SerializationReader Reader)
        {
            ZoneTerrainType = Reader.ReadOptimizedString();
            RegionTier = Reader.ReadOptimizedInt32();
            TerrainTravelClass = Reader.ReadOptimizedString();
        }

        protected void ReadProperties(SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count >= 0)
            {
                Properties = new(count);
                for (int i = 0; i < count; i++)
                    Properties.Add(Reader.ReadOptimizedString(), Reader.ReadObject());
            }
        }

        protected IZonePart ReadZonePart(SerializationReader Reader)
        {
            if (Reader == null)
                throw new ArgumentNullException(nameof(Reader));

            Reader.StartBlock(out var Position, out var Length);
            if (Length == 0)
                return null;

            Type type = null;
            IZonePart zonePart = null;
            try
            {
                type = Reader.ReadTokenizedType();
                zonePart = Activator.CreateInstance(type) as IZonePart;
                zonePart.Read(null, Reader);
            }
            catch (Exception exception)
            {
                if (zonePart == null
                    || !zonePart.ReadError(exception, Reader, Position, Length))
                    Reader.SkipBlock(exception, type, Position, Length);
            }
            return zonePart;
        }

        protected void ReadZoneParts(SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();

            if (count >= 0)
            {
                ZoneParts = new IZonePart[count];
                for (int i = 0; i < count; i++)
                    ZoneParts[i] = ReadZonePart(Reader);
            }
        }

        protected void ReadPseudoCells(SerializationReader Reader)
        {
            Width = Reader.ReadOptimizedInt32();
            Height = Reader.ReadOptimizedInt32();

            Map = new PseudoCell[Width][];
            for (int i = 0; i < Width; i++)
            {
                Map[i] = new PseudoCell[Height];
                for (int j = 0; j < Height; j++)
                {
                    if (Reader.ReadComposite<PseudoCell>() is not PseudoCell pseudoCell)
                        pseudoCell = new();

                    pseudoCell.ParentZone = this;
                    Map[i][j] = pseudoCell;
                }
            }
        }
        #endregion

        public virtual void Read(SerializationReader Reader)
        {
            ZoneID = Reader.ReadOptimizedString();

            ReadNameData(Reader);
            ReadWeatherData(Reader);
            ReadTierData(Reader);
            ReadTerrainData(Reader);

            ReadProperties(Reader);
            ReadZoneParts(Reader);

            ReadPseudoCells(Reader);

            LunarParty = Reader.ReadComposite<LunarPartyPseudoAddresses>();
        }

        #endregion
        #endregion

        public static PseudoZone FromZone(Zone Zone)
        {
            if (Zone == null)
                return null;

            var zoneTerrain = Zone.GetTerrainObject();
            
            var pseudoZone = new PseudoZone
            {
                ZoneID = Zone.ZoneID,

                BaseDisplayName = Zone.BaseDisplayName,
                DisplayName = Zone.DisplayName,
                NameContext = Zone.NameContext,
                DefiniteArticle = Zone.DefiniteArticle,
                IndefiniteArticle = Zone.IndefiniteArticle,
                HasProperName = Zone.HasProperName,
                NamedByPlayer = Zone.NamedByPlayer,
                IncludeContextInZoneDisplay = Zone.IncludeContextInZoneDisplay,
                IncludeStratumInZoneDisplay = Zone.IncludeStratumInZoneDisplay,

                HasWeather = Zone.HasWeather,
                WindDirections = Zone.WindDirections,
                WindDuration = Zone.WindDuration,
                WindSpeed = Zone.WindSpeed,

                Tier = Zone.Tier,
                NewTier = Zone.NewTier,

                ZoneTerrainType = zoneTerrain?.GetTagOrStringProperty("Terrain", BonesSpec.MissingTerrainType),
                RegionTier = int.Parse(zoneTerrain.GetTag("RegionTier", "1")),
                TerrainTravelClass = zoneTerrain?.GetPart<TerrainTravel>()?.TravelClass ?? "none",

                Properties = The.ZoneManager.ZoneProperties.GetValue(Zone.ZoneID),

                Width = Zone.Width,
                Height = Zone.Height,
            };

            if (Zone.Parts != null)
            {
                pseudoZone.ZoneParts = new IZonePart[Zone.Parts.Count];
                for (int i = 0; i < pseudoZone.ZoneParts.Length; i++)
                    pseudoZone.ZoneParts[i] = Zone.Parts[i]?.DeepCopy(null);
            }

            pseudoZone.Map = new PseudoCell[pseudoZone.Width][];
            for (int i = 0; i < pseudoZone.Width; i++)
            {
                pseudoZone.Map[i] = new PseudoCell[pseudoZone.Height];
                for (int j = 0; j < pseudoZone.Height; j++)
                {
                    if (Zone.GetCell(i, j) is not Cell cell)
                    {
                        Utils.Error($"Failed to get {nameof(Cell)} from [{i}, {j}]", new ArgumentOutOfRangeException("Coordinates must be for valid cell location"));
                        continue;
                    }

                    if (PseudoCell.FromCell(cell) is not PseudoCell pseudoCell)
                        pseudoCell = new(i, j);

                    pseudoZone.Map[i][j] = pseudoCell;

                    foreach ((var address, var gameObject) in pseudoCell.Objects.IteratorSafe())
                    {
                        if (gameObject.IsLunarRegent(The.Game.GameID))
                        {
                            pseudoZone.LunarParty ??= new();
                            pseudoZone.LunarParty.LunarRegent = address;
                        }
                        else
                        if (gameObject.IsLunarCourtier(The.Game.GameID))
                        {
                            pseudoZone.LunarParty ??= new();
                            pseudoZone.LunarParty.LunarCourtiers ??= new(PseudoAddress.EqualityComparer);
                            pseudoZone.LunarParty.LunarCourtiers.Add(address);
                        }
                    }
                }
            }

            return pseudoZone;
        }

        public void PrepForFinalizeWrite()
        {
            foreach (var pseudoGameObject in YieldPseudoGameObjects())
            {
                pseudoGameObject.SetSerializationProps();
                pseudoGameObject.UnsetCellForSerialization();
            }
        }

        public void UnprepForFinalizeWrite()
        {
            foreach (var pseudoGameObject in YieldPseudoGameObjects())
            {
                pseudoGameObject.ResetCellForSerialization();
                pseudoGameObject.UnsetSerializationProps();
            }
        }

        public static PseudoZone Load(SerializationReader Reader, SaveBonesInfo BonesInfo)
        {
            if (Reader.ReadComposite<PseudoZone>() is not PseudoZone pseudoZone)
                return null;

            string bonesID = BonesInfo.ID;
            pseudoZone.BonesID = bonesID;

            foreach (var pseudoCell in pseudoZone.LoopCells())
                pseudoCell.SetParent(pseudoZone);

            return pseudoZone;
        }

        public IEnumerable<PseudoCell> LoopCells()
        {
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    yield return Map[j][i];
        }

        public IEnumerable<PseudoGameObject> YieldPseudoGameObjects()
        {
            foreach (var column in Map.IteratorSafe())
                foreach (var cell in column.IteratorSafe())
                    foreach (var pseudoGameObject in cell.Objects.IteratorSafe())
                        yield return pseudoGameObject;
        }

        public IEnumerable<PseudoAddress> YieldPseudoAddresss()
        {
            foreach (var pseudoGameObject in YieldPseudoGameObjects())
                yield return pseudoGameObject.Address;
        }

        public IEnumerable<GameObject> YieldObjects()
        {
            foreach (var pseudoGameObject in YieldPseudoGameObjects())
                yield return pseudoGameObject.GameObject;
        }

        private static bool TrySetPrivateFieldNaughty<T>(Zone Zone, string FieldName, T Value)
        {
            if (Zone == null)
            {
                Utils.Warn($"{nameof(TrySetPrivateFieldNaughty)} called on null {nameof(Zone)}");
                return false;
            }
            if (FieldName.IsNullOrEmpty())
            {
                Utils.Warn($"{nameof(TrySetPrivateFieldNaughty)} called with null or empty {nameof(FieldName)}");
                return false;
            }
            try
            {
                if (Zone.GetType().GetField(FieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) is not FieldInfo field)
                {
                    Utils.Warn($"{nameof(TrySetPrivateFieldNaughty)} cannot set non-existent field {FieldName}");
                    return false;
                }

                if (!field.IsPrivate)
                    Utils.Warn($"{nameof(TrySetPrivateFieldNaughty)} called on non-private field {FieldName}");

                if (!field.FieldType.InheritsFrom(typeof(T)))
                {
                    Utils.Warn($"{nameof(TrySetPrivateFieldNaughty)} attempted to set private field {FieldName} of type {field.FieldType} to value of non-derived type {typeof(T)}");
                    return false;
                }

                field.SetValue(Zone, Value);

                return Equals((T)field.GetValue(Zone), Value);
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(TrySetPrivateFieldNaughty)} failed to set private field {FieldName}", x);
                return false;
            }
        }

        public LunarPartyPseudoAddresses GetLunarPartyPseudoAddresses()
        {
            string bonesID = BonesID ?? The.Game?.GameID;

            if (Map.IsNullOrEmpty()
                || bonesID.IsNullOrEmpty())
                return null;

            var lunarParty = new LunarPartyPseudoAddresses();

            foreach ((var address, var gameObject) in YieldPseudoGameObjects())
            {
                if (gameObject.IsLunarRegent(bonesID))
                    lunarParty.LunarRegent = address;
                else
                if (gameObject.IsLunarCourtier(bonesID))
                {
                    lunarParty.LunarCourtiers ??= new(PseudoAddress.EqualityComparer);
                    lunarParty.LunarCourtiers.Add(address);
                }
            }

            if (lunarParty.LunarRegent == null)
                return null;

            return lunarParty;
        }

        public bool TryGetLunarPartyPseudoAddresses(out LunarPartyPseudoAddresses LunarPartyPseudoAddresses)
            => (LunarPartyPseudoAddresses = GetLunarPartyPseudoAddresses()) != null
            ;

        public LunarParty GetLunarParty()
        {
            string bonesID = BonesID ?? The.Game?.GameID;

            if (Map.IsNullOrEmpty())
                return null;

            if (bonesID.IsNullOrEmpty())
                return null;

            LunarParty lunarParty = null;

            LunarParty?.TryRetrieveLunarParty(this, out lunarParty);

            if (lunarParty == null)
            {
                lunarParty = new();
                foreach (var gameObject in YieldObjects())
                {
                    if (gameObject.IsLunarRegent(bonesID))
                        lunarParty.LunarRegent = gameObject;
                    else
                    if (gameObject.IsLunarCourtier(bonesID))
                    {
                        lunarParty.LunarCourtiers ??= new();
                        lunarParty.LunarCourtiers.Add(gameObject);
                    }
                }
            }

            if (lunarParty.LunarRegent == null)
            {
                lunarParty.Dispose();
                return null;
            }

            return lunarParty;
        }

        public bool TryGetLunarParty(out LunarParty LunarParty)
            => (LunarParty = GetLunarParty()) != null
            ;

        public GameObject GetAtAddress(
            PseudoAddress Address,
            bool Extract = false,
            SaveBonesInfo BonesInfo = null,
            IEnumerable<GameObject> DestinationObjects = null
            )
        {
            if (Map.IsNullOrEmpty()
                || Address.IsNullOrInvalid())
                return null;

            int X = Address.X;
            int Y = Address.Y;
            int I = Address.I;

            if (Map.Length < X
                || Map[X] is not PseudoCell[] column)
                return null;

            if (column.Length < Y
                || column[Y] is not PseudoCell cell)
                return null;

            if (cell.Objects == null
                || cell.Objects.Count < I
                || cell.Objects[I] is not PseudoGameObject pseudoGameObject)
                return null;

            if (!Extract)
                return pseudoGameObject.GameObject;
            else
            if (BonesInfo != null)
                return pseudoGameObject.PerformExtraction(BonesInfo, YieldObjects(), DestinationObjects.IteratorSafe(), out _);
            else
                return null;
        }

        public CoalescibleSet<GameObject> GetAtAddresses(
            IEnumerable<PseudoAddress> Addresses,
            bool Extract = false,
            SaveBonesInfo BonesInfo = null
            )
        {
            if (Addresses.IsNullOrEmpty())
                return null;

            CoalescibleSet<GameObject> gameObjectSet = null;

            foreach (var address in Addresses)
            {
                if (GetAtAddress(
                    Address: address,
                    Extract: Extract,
                    BonesInfo: BonesInfo,
                    DestinationObjects: gameObjectSet) is GameObject gameObject)
                {
                    gameObjectSet ??= new();
                    gameObjectSet.Add(gameObject);
                }
            }
            return gameObjectSet;
        }

        public static void ProcessContext(ref string Context, string MainContext)
        {
            if (!Context.IsNullOrEmpty())
                Context = $"{MainContext} {Context}";
            else
                Context = MainContext;
        }

        public LunarParty ExtractLunarParty(
            SaveBonesInfo BonesInfo,
            out bool Blocked,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null,
            string Context = null
            )
        {
            Blocked = false;
            if (Map.IsNullOrEmpty()
                || LunarParty?.LunarRegent == null)
                return null;

            var lunarParty = new LunarParty
            {
                LunarRegent = GetAtAddress(LunarParty.LunarRegent, Extract: true, BonesInfo),
                LunarCourtiers = GetAtAddresses(LunarParty.LunarCourtiers, Extract: true, BonesInfo),
            };

            if (lunarParty.LunarRegent == null)
            {
                lunarParty.Obliterate();
                return null;
            }

            ProcessContext(ref Context, EXTRACT_CONTEXT);

            if (!EarlyBeforeLoadLunarRegentEvent.Check(
                BonesInfo: BonesInfo,
                LunarObject: lunarParty.LunarRegent,
                Context: Context)
                || !BeforeLoadLunarRegentEvent.Check(
                    BonesInfo: BonesInfo,
                    LunarObject: lunarParty.LunarRegent,
                    Context: Context))
            {
                Blocked = true;
                lunarParty.Obliterate();

                return null;
            }
            ProcPreLoad?.Invoke(lunarParty.LunarRegent);

            LoadLunarRegentEvent.Send(
                BonesInfo: BonesInfo,
                LunarObject: lunarParty.LunarRegent,
                Context: Context);
            AfterLoadedLunarRegentEvent.Send(
                BonesInfo: BonesInfo,
                LunarObject: lunarParty.LunarRegent,
                Context: Context);

            ProcPostLoad?.Invoke(lunarParty.LunarRegent);

            using (var courtiersToRemove = ScopeDisposedList<GameObject>.GetFromPool())
            {
                foreach (var lunarCourtier in lunarParty.LunarCourtiers.IteratorSafe())
                {
                    if (!EarlyBeforeLoadLunarCourtierEvent.Check(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: Context)
                        || !BeforeLoadLunarCourtierEvent.Check(
                            BonesInfo: BonesInfo,
                            LunarObject: lunarCourtier,
                            LunarRegent: lunarParty.LunarRegent,
                            Context: Context))
                    {
                        courtiersToRemove.Add(lunarCourtier);
                        continue;
                    }

                    ProcPreLoad?.Invoke(lunarCourtier);

                    LoadLunarCourtierEvent.Send(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: Context);
                    AfterLoadedLunarCourtierEvent.Send(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: Context);

                    ProcPostLoad?.Invoke(lunarCourtier);
                }
                foreach (var courtierToRemove in courtiersToRemove)
                {
                    lunarParty.LunarCourtiers?.Remove(courtierToRemove);
                    courtierToRemove?.Obliterate();
                }
            }
            return lunarParty;
        }

        public bool TryExtractLunarParty(
            SaveBonesInfo BonesInfo,
            out LunarParty LunarParty,
            out bool Blocked,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null,
            string Context = null
            )
        {
            if ((LunarParty = ExtractLunarParty(
                BonesInfo: BonesInfo,
                Blocked: out Blocked,
                ProcPreLoad: ProcPreLoad,
                ProcPostLoad: ProcPostLoad,
                Context: Context)) != null
                && !Blocked)
                return true;

            LunarParty?.Obliterate();
            return false;
        }

        public PseudoCell GetCell(int X, int Y)
        {
            if (Map.IsNullOrEmpty())
                return null;

            if (X < 0
                || X >= Width)
                throw new ArgumentOutOfRangeException(nameof(X), $"Must be gte 0 and lt {nameof(PseudoZone)}.{nameof(Width)}");

            if (Map[X] is not PseudoCell[] column)
                return null;

            if (Y < 0
                || Y >= Height)
                throw new ArgumentOutOfRangeException(nameof(Y), $"Must be gte 0 and lt {nameof(PseudoZone)}.{nameof(Height)}");

            if (Y >= column.Length)
                return null;

            return column[Y];
        }

        public PseudoCell GetCell(Location2D Location)
            => Location == null
            ? throw new ArgumentNullException(nameof(Location)) 
            : GetCell(Location.X, Location.Y)
            ;

        public PseudoCell GetCell(Cell Cell)
            => Cell == null
            ? throw new ArgumentNullException(nameof(Cell))
            : GetCell(Cell.Location)
            ;

        public PseudoCell GetCell(PseudoAddress Address)
            => Address == null
            ? throw new ArgumentNullException(nameof(Address))
            : GetCell(Address.X, Address.Y)
            ;

        public bool TryGetCell(int X, int Y, out PseudoCell PseudoCell)
        {
            PseudoCell = null;
            if (Map.IsNullOrEmpty())
                return false;

            if (X < 0
                || X >= Width
                || Y < 0
                || Y >= Height)
                return false;

            return (PseudoCell = GetCell(X, Y)) != null;
        }

        public bool TryGetCell(Location2D Location, out PseudoCell PseudoCell)
            => TryGetCell(Location?.X ?? -1, Location?.Y ?? -1, out PseudoCell)
            ;

        public bool TryGetCell(Cell Cell, out PseudoCell PseudoCell)
            => TryGetCell(Cell?.Location, out PseudoCell)
            ;

        private bool ApplyTypeZoneInternal(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ref GameObject LunarRegent,
            Dictionary<string, LunarParty> LunarParties,
            ScopeDisposedList<CrossGameObject> CrossGameObjects,
            Predicate<GameObject> AddWhenNot = null,
            bool IgnoreLocationMismatch = false)
        {
            if (Zone.Width != Width
                || Zone.Height != Height)
            {
                Utils.Error(
                    Context: $"Attempted to apply PseudoZone to {nameof(Zone)} with disparate dimensions",
                    X: new ArgumentException($"Expected [{nameof(Zone.Width)}: {Zone.Width}, {nameof(Zone.Height)}: {Zone.Height}], " +
                        $"got [{nameof(Width)}: {Width}, {nameof(Height)}: {Height}]", nameof(Zone)));
                return false;
            }

            Zone.BaseDisplayName = BaseDisplayName;
            Zone.DisplayName = DisplayName;

            Zone.NameContext = NameContext;
            Zone.DefiniteArticle = DefiniteArticle;
            Zone.IndefiniteArticle = IndefiniteArticle;
            Zone.HasProperName = HasProperName;
            Zone.NamedByPlayer = NamedByPlayer;
            Zone.IncludeContextInZoneDisplay = IncludeContextInZoneDisplay;
            Zone.IncludeStratumInZoneDisplay = IncludeStratumInZoneDisplay;

            Zone.HasWeather = HasWeather;
            Zone.WindDirections = WindDirections;
            Zone.WindDuration = WindDuration;
            Zone.WindSpeed = WindSpeed;

            Zone.Tier = Tier;

            string newTierPrivateField = $"_{nameof(NewTier)}";
            if (!TrySetPrivateFieldNaughty(Zone, newTierPrivateField, NewTier))
                Utils.Warn($"Failed to set private field {newTierPrivateField} to {NewTier} for Zone {Zone.DebugName}");

            Zone.FloodMap = new int[Width, Height];

            var missileMap = Zone.MissileMap = new MissileMapType[Width][];
            for (int i = 0; i < Width; i++)
            {
                Zone.MissileMap[i] = new MissileMapType[Height];
                for (int j = 0; j < Height; j++)
                    Zone.MissileMap[i][j] = MissileMapType.Empty;
            }

            Zone.LightMap = new LightLevel[Width * Height];
            Zone.ExploredMap = new bool[Width * Height];
            Zone.FakeUnexploredMap = null;
            Zone.VisibilityMap = new bool[Width * Height];
            Zone.ReachableMap = new bool[Width, Height];

            Zone.NavigationMap = new NavigationWeight[Width, Height];
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    Zone.NavigationMap[j, i] = new NavigationWeight();

            foreach ((var key, var value) in Properties.IteratorSafe())
                The.ZoneManager.SetZoneProperty(Zone.ZoneID, key, value);

            if (!Zone.Parts.IsNullOrEmpty())
            {
                using (var zoneParts = ScopeDisposedList<IZonePart>.GetFromPoolFilledWith(Zone.Parts))
                {
                    foreach (var zonePart in zoneParts)
                        Zone.RemovePart(zonePart);
                }
            }

            foreach (var zonePart in ZoneParts.IteratorSafe())
            {
                zonePart.ParentZone = Zone;
                Zone.AddPart(zonePart, true);
            }

            // Event.PinCurrentPool();
            foreach (var cell in Zone.LoopCells())
            {
                // Event.ResetPool();
                try
                {
                    if (!TryGetCell(cell, out PseudoCell pseudoCell))
                    {
                        Utils.Warn($"Failed to get {nameof(PseudoCell)} for {nameof(Cell)} for Zone {Zone.DebugName} @[{cell?.DebugName ?? "NO_CELL"}]");
                        continue;
                    }
                    if (!pseudoCell.TryApplyToCell(
                        Cell: cell,
                        BonesInfo: BonesInfo,
                        LunarRegent: ref LunarRegent,
                        LunarParties: ref LunarParties,
                        CrossGameObjects: CrossGameObjects,
                        AddWhenNot: AddWhenNot,
                        RemovalExclusions: IsObjectSpecial,
                        IgnoreLocationMismatch: IgnoreLocationMismatch))
                    {
                        Utils.Warn($"Failed to Apply {nameof(PseudoCell)} to {nameof(Cell)} for Zone {Zone.DebugName} @[{cell?.DebugName ?? "NO_CELL"}]");
                        continue;
                    }
                }
                finally
                {
                    // Event.ResetToPin();
                }
            }
            return LunarRegent != null;
        }

        public static bool IsObjectSpecial(GameObject Object)
            => Object.HasPart<GameUnique>()
            || Object.HasPropertyOrTag("QuestGiver")
            || Object.IsImportant()
            || Object.IsInteresting()
            || Object.HasPart<GivesRep>()
            ;

        private bool ApplyTypeBubbleInternal(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            int Radius,
            ref GameObject LunarRegent,
            Dictionary<string, LunarParty> LunarParties,
            ScopeDisposedList<CrossGameObject> CrossGameObjects,
            Predicate<GameObject> AddWhenNot = null,
            bool IgnoreLocationMismatch = false)
        {
            using var bubble = ScopeDisposedList<PseudoCell>.GetFromPoolFilledWith(GetRegentBubble(Radius));

            using var availableZoneCells = ScopeDisposedList<Cell>.GetFromPoolFilledWith(Zone.LoopCells());

            using var protectedObjects = ScopeDisposedList<GameObject>.GetFromPool();
            foreach (var zoneObject in Zone.YieldObjects())
            {
                if (IsObjectSpecial(zoneObject))
                {
                    protectedObjects.Add(zoneObject);
                    if (!availableZoneCells.IsNullOrEmpty())
                    {
                        var cellsNearObject = zoneObject.CurrentCell.GetCellsInACosmeticCircleSilent(Radius);
                        availableZoneCells.RemoveAll(c => cellsNearObject.Contains(c));
                    }
                }
            }

            availableZoneCells.RemoveAll(delegate (Cell cell)
            {
                return cell.X < Radius - 1
                    || cell.X > cell.ParentZone.Width - Radius
                    || cell.Y < Radius - 1
                    || cell.Y > cell.ParentZone.Height - Radius
                    ;
            });

            Cell targetCell = availableZoneCells.GetRandomElement(Stat.GetSeededRandomGenerator($"{Const.MOD_PREFIX}{Zone.ZoneID}"));

            if (targetCell == null)
            {
                availableZoneCells.Clear();
                availableZoneCells.AddRange(Zone.LoopCells());
                availableZoneCells.RemoveAll(delegate (Cell cell)
                {
                    return cell.X < Radius
                        || cell.X >= cell.ParentZone.Width - Radius
                        || cell.Y < Radius
                        || cell.Y >= cell.ParentZone.Height - Radius
                        ;
                });
                targetCell = availableZoneCells.GetRandomElement(Stat.GetSeededRandomGenerator($"{Const.MOD_PREFIX}{Zone.ZoneID}"));
            }

            if (targetCell == null)
            {
                Utils.Error($"Couldn't find a target cell in zone {Zone.ZoneID} to place Bones Bubble.", new InvalidOperationException($"{nameof(targetCell)} is null"));
                return false;
            }

            using var targetBubble = ScopeDisposedList<Cell>.GetFromPoolFilledWith(targetCell.GetCellsInACosmeticCircleSilent(Radius));

            if (targetBubble.Count != bubble.Count)
            {
                Utils.Error(Context:
                    $"Bubble ({targetBubble.Count}) and target bubble ({bubble.Count}) have different counts",
                    X: new IndexOutOfRangeException($"Bubbles must have same element count"));
                return false;
            }

            // Event.PinCurrentPool();
            for (int i = 0; i < bubble.Count; i++)
            {
                // Event.ResetPool();
                try
                {
                    if (targetBubble[i] is Cell cell
                    && bubble[i] is PseudoCell pseudoCell)
                    {
                        if (!pseudoCell.TryApplyToCell(
                            Cell: cell,
                            BonesInfo: BonesInfo,
                            LunarRegent: ref LunarRegent,
                            LunarParties: ref LunarParties,
                            CrossGameObjects: CrossGameObjects,
                            AddWhenNot: AddWhenNot,
                            RemovalExclusions: IsObjectSpecial,
                            IgnoreLocationMismatch: IgnoreLocationMismatch))
                        {
                            Utils.Warn($"Failed to Apply {nameof(PseudoCell)} to {nameof(Cell)} for Zone {Zone.DebugName} @[{cell?.DebugName ?? "NO_CELL"}]");
                            continue;
                        }
                    }
                }
                finally
                {
                    // Event.ResetToPin();
                }
            }

            if (LunarRegent?.CurrentCell is Cell lunarCell
                && !lunarCell.IsReachable())
            {

                Cell nearestReachableCell = null;
                foreach (var cell in Zone.LoopCells())
                {
                    if (!cell.IsReachable())
                        continue;

                    nearestReachableCell ??= cell;
                    int distanceToCell = cell.CosmeticDistanceTo(lunarCell.X, lunarCell.Y);
                    int currentDistanceToCell = nearestReachableCell.CosmeticDistanceTo(lunarCell.X, lunarCell.Y);
                    if (currentDistanceToCell < distanceToCell)
                        nearestReachableCell = cell;
                }

                if (nearestReachableCell != null)
                {
                    var findpath = new FindPath(nearestReachableCell, lunarCell);

                    var lunarRegent = LunarRegent;
                    foreach (var step in findpath?.Steps.IteratorSafe())
                    {
                        if (step.IsSolidFor(lunarRegent))
                            step.Clear(Combat: true);

                        step.ForeachAdjacentCell(delegate (Cell c)
                        {
                            if (step.IsSolidFor(lunarRegent)
                                && 1750.in10000())
                                c.Clear(Combat: true);
                        });
                    }
                }
            }

            return LunarRegent != null;
        }

        private bool ApplyTypePartyInternal(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ref GameObject LunarRegent,
            Dictionary<string, LunarParty> LunarParties,
            ScopeDisposedList<CrossGameObject> CrossGameObjects,
            Predicate<GameObject> AddWhenNot = null,
            bool IgnoreLocationMismatch = false)
        {
            if (LunarParty == null)
                return false;

            var rnd = Stat.GetSeededRandomGenerator($"{Const.MOD_PREFIX}{Zone.ZoneID}");

            var destinationCell = Zone.GetEmptyReachableCellsNInFromEdge(N: 3).GetRandomElement(rnd)
                ?? Zone.GetEmptyReachableCells().GetRandomElement(rnd)
                ?? Zone.GetEmptyCellsNInFromEdge(N: 3).GetRandomElement(rnd)
                ?? Zone.GetEmptyCells().GetRandomElement(rnd)
                ?? Zone.GetCells().GetRandomElement(rnd);

            if (!GetCell(LunarParty.LunarRegent).TryApplyToCell(
                Cell: destinationCell,
                BonesInfo: BonesInfo,
                LunarRegent: ref LunarRegent,
                LunarParties: ref LunarParties,
                CrossGameObjects: CrossGameObjects,
                AddWhenNot: AddWhenNot,
                RemovalExclusions: IsObjectSpecial,
                IgnoreLocationMismatch: IgnoreLocationMismatch))
            {
                Utils.Warn($"Failed to Apply {nameof(PseudoCell)} to {nameof(Cell)} for Zone {Zone.DebugName} @[{destinationCell?.DebugName ?? "NO_CELL"}]");
                return false;
            }
            
            if (LunarRegent?.CurrentCell is Cell lunarCell
                && !lunarCell.IsReachable())
            {

                Cell nearestReachableCell = null;
                foreach (var cell in Zone.LoopCells())
                {
                    if (!cell.IsReachable())
                        continue;

                    nearestReachableCell ??= cell;
                    int distanceToCell = cell.CosmeticDistanceTo(lunarCell.X, lunarCell.Y);
                    int currentDistanceToCell = nearestReachableCell.CosmeticDistanceTo(lunarCell.X, lunarCell.Y);
                    if (currentDistanceToCell < distanceToCell)
                        nearestReachableCell = cell;
                }

                if (nearestReachableCell != null)
                {
                    var findpath = new FindPath(nearestReachableCell, lunarCell);

                    var lunarRegent = LunarRegent;
                    foreach (var step in findpath?.Steps.IteratorSafe())
                    {
                        if (step.IsSolidFor(lunarRegent))
                            step.Clear(Combat: true);

                        step.ForeachAdjacentCell(delegate (Cell c)
                        {
                            if (step.IsSolidFor(lunarRegent)
                                && 1750.in10000())
                                c.Clear(Combat: true);
                        });
                    }
                }
            }

            if (LunarParty.LunarCourtiers is CoalescibleSet<PseudoAddress> lunarCourtiers)
            {
                var partyCount = lunarCourtiers.Count;
                var cellsList = destinationCell.GetConnectedSpawnLocations(partyCount * 2);
                foreach (var lunarCourtier in lunarCourtiers)
                {
                    if (cellsList.IsNullOrEmpty())
                    {
                        cellsList ??= new();

                        var prospectiveCell = destinationCell.getClosestEmptyCell()
                            ?? destinationCell.getClosestPassableCell();

                        if (prospectiveCell != null)
                            cellsList.Add(prospectiveCell);
                    }

                    while (!cellsList.IsNullOrEmpty())
                    {
                        var courtierCell = cellsList.GetRandomElement();
                        if (!GetCell(lunarCourtier).TryApplyToCell(
                            Cell: courtierCell,
                            BonesInfo: BonesInfo,
                            LunarRegent: ref LunarRegent,
                            LunarParties: ref LunarParties,
                            CrossGameObjects: CrossGameObjects,
                            AddWhenNot: AddWhenNot,
                            RemovalExclusions: IsObjectSpecial,
                            IgnoreLocationMismatch: IgnoreLocationMismatch))
                        {
                            cellsList.Remove(courtierCell);
                        }
                        else
                            break;
                    }
                }
            }

            return LunarRegent != null;
        }

        public bool TryApplyToZone(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ZoneBonesAllocation.AllocationTypes Type,
            out GameObject LunarRegent,
            out bool Blocked,
            Predicate<GameObject> AddWhenNot = null,
            bool IgnoreLocationMismatch = false,
            string Context = null
            )
        {
            LunarRegent = null;
            Blocked = false;

            ProcessContext(ref Context, RECLAIM_CONTEXT);

            if (!BeforePseudoZoneLoadedEvent.Check(Zone, BonesID, this, Context))
            {
                Blocked = true;
                return false;
            }
            if (!TryGetLunarParty(out _))
            {
                Utils.Error(
                    Context: $"Attempted to apply PseudoZone without {nameof(LunarRegent)} to null {nameof(Zone)}",
                    X: new InvalidOperationException($"Loading bones Zone must have {nameof(LunarRegent)}"));
                //return false;
            }
            if (Zone == null)
            {
                Utils.Error($"Attempted to apply PseudoZone to null {nameof(Zone)}", new ArgumentNullException(nameof(Zone)));
                return false;
            }

            var lunarParties = new Dictionary<string, LunarParty>();
            using var crossGameObjects = ScopeDisposedList<CrossGameObject>.GetFromPool();

            bool applied = Type switch
            {
                ZoneBonesAllocation.AllocationTypes.Zone => ApplyTypeZoneInternal(
                    Zone: Zone,
                    BonesInfo: BonesInfo,
                    LunarRegent: ref LunarRegent,
                    LunarParties: lunarParties,
                    CrossGameObjects: crossGameObjects,
                    AddWhenNot: AddWhenNot,
                    IgnoreLocationMismatch: IgnoreLocationMismatch),

                ZoneBonesAllocation.AllocationTypes.Bubble => ApplyTypeBubbleInternal(
                    Zone: Zone,
                    BonesInfo: BonesInfo,
                    Radius: Stat.SeededRandom($"{nameof(ApplyTypeBubbleInternal)}::{BonesID}:Radius", 4, 8),
                    LunarRegent: ref LunarRegent,
                    LunarParties: lunarParties,
                    CrossGameObjects: crossGameObjects,
                    AddWhenNot: AddWhenNot,
                    IgnoreLocationMismatch: true),

                ZoneBonesAllocation.AllocationTypes.Party => ApplyTypePartyInternal(
                    Zone: Zone,
                    BonesInfo: BonesInfo,
                    LunarRegent: ref LunarRegent,
                    LunarParties: lunarParties,
                    CrossGameObjects: crossGameObjects,
                    AddWhenNot: AddWhenNot,
                    IgnoreLocationMismatch: true),

                _ => false,
            };

            if (LunarRegent == null
                || !EarlyBeforeLoadLunarRegentEvent.Check(
                    Player: The.Player,
                    BonesInfo: BonesInfo,
                    LunarObject: LunarRegent,
                    Context: Context)
                || !BeforeLoadLunarRegentEvent.Check(
                    Player: The.Player,
                    BonesInfo: BonesInfo,
                    LunarObject: LunarRegent,
                    Context: Context))
            {
                Blocked = LunarRegent != null;

                foreach ((var _, var lunarParty) in lunarParties.IteratorSafe())
                    lunarParty.Dispose();
                lunarParties?.Clear();

                crossGameObjects?.Clear();

                return false;
            }
            //Utils.Log($"{nameof(TryApplyToZone)}: {nameof(LoadLunarRegentEvent)}");
            LoadLunarRegentEvent.Send(
                Player: The.Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarRegent,
                Context: Context);

            //Utils.Log($"{nameof(TryApplyToZone)}: {nameof(AfterLoadedLunarRegentEvent)}");
            AfterLoadedLunarRegentEvent.Send(
                Player: The.Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarRegent,
                Context: Context);

            //Utils.Log($"foreach ((var regentID, var lunarParty) in lunarParties.IteratorSafe())");
            foreach ((var regentID, var lunarParty) in lunarParties.IteratorSafe())
            {
                if (lunarParty.LunarRegent == null)
                    continue;

                if (regentID != BonesID)
                    continue;

                //Utils.Log($"{1.Indent()}foreach (var lunarCourtier in lunarParty.LunarCourtiers.IteratorSafe())");
                using (var courtiersToRemove = ScopeDisposedList<GameObject>.GetFromPool())
                {
                    foreach (var lunarCourtier in lunarParty.LunarCourtiers.IteratorSafe())
                    {
                        //Utils.Log($"{1.Indent()}{nameof(lunarCourtier)}: {lunarCourtier?.DebugName ?? "NO_COURTIER"}");
                        //Utils.Log($"{2.Indent()}{nameof(TryApplyToZone)}: {nameof(EarlyBeforeLoadLunarCourtierEvent)} & {nameof(BeforeLoadLunarCourtierEvent)}");
                        if (!EarlyBeforeLoadLunarCourtierEvent.Check(
                            BonesInfo: BonesInfo,
                            LunarObject: lunarCourtier,
                            LunarRegent: LunarRegent,
                            Context: Context)
                            || !BeforeLoadLunarCourtierEvent.Check(
                                BonesInfo: BonesInfo,
                                LunarObject: lunarCourtier,
                                LunarRegent: LunarRegent,
                                Context: Context))
                        {
                            courtiersToRemove.Add(lunarCourtier);
                            continue;
                        }

                        //Utils.Log($"{2.Indent()}{nameof(TryApplyToZone)}: {nameof(LoadLunarCourtierEvent)}");
                        LoadLunarCourtierEvent.Send(
                            BonesInfo: BonesInfo,
                            LunarObject: lunarCourtier,
                            LunarRegent: LunarRegent,
                            Context: Context);

                        //Utils.Log($"{2.Indent()}{nameof(TryApplyToZone)}: {nameof(AfterLoadedLunarCourtierEvent)}");
                        AfterLoadedLunarCourtierEvent.Send(
                            BonesInfo: BonesInfo,
                            LunarObject: lunarCourtier,
                            LunarRegent: LunarRegent,
                            Context: Context);
                    }
                    foreach (var courtierToRemove in courtiersToRemove)
                    {
                        lunarParty.LunarCourtiers?.Remove(courtierToRemove);
                        courtierToRemove?.Obliterate();
                    }
                }
            }

            foreach (var crossGameObject in crossGameObjects.IteratorSafe())
            {
                if (crossGameObject.Clone is not GameObject clone
                    || crossGameObject.Original is not GameObject original)
                    continue;

                clone.ForfeitTurn();

                if (crossGameObject.Clone == LunarRegent)
                    continue;

                if (lunarParties.IsNullOrEmpty())
                    continue;

                if (!lunarParties.TryGetValue(BonesID, out var lunarParty)
                    || (lunarParty.LunarCourtiers?.All(courtier => courtier != crossGameObject.Clone) is not false))
                    continue;

                //Transmutation.TransmuteBrain(original, clone);
            }

            foreach ((var _, var lunarParty) in lunarParties.IteratorSafe())
                lunarParty.Dispose();
            lunarParties?.Clear();

            crossGameObjects?.Clear();

            if (LunarRegent != null)
            {
                if (!Zone.DisplayName.Contains("feverish"))
                    Zone.DisplayName = $"feverish {Zone.DisplayName}";
            }

            AfterPseudoZoneLoadedEvent.Send(Zone, BonesID, LunarRegent, this, Context);
            return LunarRegent != null;
        }

        public IEnumerable<PseudoCell> GetRegentBubble(int Radius, Predicate<PseudoCell> Where = null)
        {
            if (LunarParty?.LunarRegent is not PseudoAddress regentAddress)
                yield break;

            if (Radius < 0)
                throw new ArgumentOutOfRangeException(nameof(Radius), "Must be positive number");

            int startingX = Math.Clamp(regentAddress.X, Radius, Width - Radius);
            int startingY = Math.Clamp(regentAddress.Y, Radius, Height - Radius);

            if (GetCell(startingX, startingY) is not PseudoCell startingCell)
                yield break;

            foreach (var pseudoCell in GetCellsInACosmeticCircle(startingCell, Radius))
                if (Where?.Invoke(pseudoCell) is not false)
                    yield return pseudoCell;
        }

        public IEnumerable<PseudoCell> GetCellsInACosmeticCircle(int X, int Y, int Radius)
        {
            if (X < 0
                || Y < 0)
                yield break;

            int yRadius = (int)Math.Max(1.0, Radius * 0.66);
            float radiusSquared = Radius * Radius;
            int minX = X - Radius;
            int maxX = X + Radius;
            int minY = Y - yRadius;
            int maxY = Y + yRadius;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float xD = Math.Abs(x - X);
                    float yD = Math.Abs(y - Y) * 1.3333f;
                    float d = (xD * xD) + (yD * yD);

                    if (radiusSquared > d 
                        && GetCell(x, y) != null)
                        yield return GetCell(x, y);
                }
            }
        }

        public IEnumerable<PseudoCell> GetCellsInACosmeticCircle(PseudoCell PseudoCell, int Radius)
            => GetCellsInACosmeticCircle(PseudoCell?.X ?? -1, PseudoCell?.Y ?? -1, Radius)
            ;

        public void Dispose()
        {
            ZoneID = null;

            BaseDisplayName = null;
            DisplayName = null;
            NameContext = null;
            DefiniteArticle = null;
            IndefiniteArticle = null;
            HasProperName = false;
            NamedByPlayer = false;
            IncludeContextInZoneDisplay = false;
            IncludeStratumInZoneDisplay = false;

            HasWeather = false;
            WindDirections = null;
            WindDuration = null;
            WindSpeed = null;

            Tier = 0;
            NewTier = 0;

            Properties = null;
            ZoneParts = null;
            Width = default;
            Height = default;
            Map = null;
        }
    }
}
