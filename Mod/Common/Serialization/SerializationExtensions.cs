using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

using HarmonyLib;

using XRL;
using XRL.Collections;
using XRL.Core;
using XRL.UI;
using XRL.World;
using XRL.World.AI.Pathfinding;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.ZoneParts;

using static XRL.World.Cell;

namespace UD_Bones_Folder.Mod
{
    [HarmonyPatch]
    public static class SerializationExtensions
    {
        public const string SERIALIZED_PROPERTY = Const.MOD_PREFIX + "Serialized_";
        public const string GAME_ID_PROPERTY = SERIALIZED_PROPERTY + "GameID";
        public const string ACTIVE_OBJECT_PROPERTY = SERIALIZED_PROPERTY + "ActiveObject";
        public const string ABILITY_OBJECT_PROPERTY = SERIALIZED_PROPERTY + "ActiveObject";

        public static void WriteBonesCell(
            this Cell Cell,
            SerializationWriter Writer)
        {
            if (Cell == null)
                throw new ArgumentNullException(nameof(Cell));

            if (Writer == null)
                throw new ArgumentNullException(nameof(Writer));

            var cellObjects = Cell.Objects;

            int writeObjectsCount = 0;
            int writeBlueprintsCount = 0;

            for (int i = 0; i < cellObjects.Count; i++)
            {
                if (cellObjects[i] is GameObject gameObject)
                {
                    if (The.ActionManager.ActionQueue.Contains(gameObject))
                        gameObject.SetStringProperty(ACTIVE_OBJECT_PROPERTY, $"{true}");
                    
                    if (The.ActionManager.AbilityObjects.Contains(gameObject))
                        gameObject.SetStringProperty(ABILITY_OBJECT_PROPERTY, $"{true}");
                    
                    gameObject.SetStringProperty(GAME_ID_PROPERTY, The.Game.GameID);

                    if (gameObject.Physics == null
                        || gameObject.Physics._CurrentCell != Cell)
                    {
                        Cell.LogInvalidPhysics(gameObject);
                        cellObjects.RemoveAt(i--);
                    }
                    else
                    if (gameObject.IsPlayer())
                        continue;
                    else
                    if (!Cell.ShouldWrite(gameObject))
                        writeBlueprintsCount++;
                    else
                        writeObjectsCount++;
                }
            }
            Writer.Write(writeObjectsCount);
            for (int i = 0; i < cellObjects.Count; i++)
                if (Cell.IsEligibleForWrite(cellObjects, i, out var gameObject))
                    Writer.WriteGameObject(gameObject);

            Writer.Write(writeBlueprintsCount);
            for (int i = 0; i < cellObjects.Count; i++)
                if (Cell.IsEligibleForBlueprintWrite(cellObjects, i, out var gameObject))
                    Writer.Write(gameObject.Blueprint);

            Writer.Write(Cell.PaintTile);
            Writer.Write(Cell.PaintTileColor);
            Writer.Write(Cell.PaintRenderString);
            Writer.Write(Cell.PaintColorString);
            Writer.Write(Cell.PaintDetailColor);

            Writer.Write(Cell.SemanticTags?.Count ?? 0);
            foreach (string semanticTag in Cell?.SemanticTags.IteratorSafe())
                Writer.Write(semanticTag);
        }

        private static bool IsEligibleForWrite(this Cell Cell, ObjectRack ObjectRack, int ObjectIndex, out GameObject Object)
        {
            Object = ObjectRack[ObjectIndex];
            return Cell.ShouldWrite(Object)
                && (Object == null 
                    || !Object.IsPlayer());
        }

        private static bool IsEligibleForBlueprintWrite(this Cell Cell, ObjectRack ObjectRack, int ObjectIndex, out GameObject Object)
        {
            Object = ObjectRack[ObjectIndex];
            return !Cell.ShouldWrite(Object)
                && (Object == null
                    || !Object.IsPlayer());
        }

        public static void LogInvalidPhysics(this Cell Cell, GameObject Object)
        {
            Cell actualCell = Object.Physics?._CurrentCell;
            Utils.Error($"Invalid physics object '{Object.DebugName}' in {Cell.ParentZone?._ZoneID}, " +
                $"cell [{Cell.X},{Cell.Y},{Cell.HasStairs()}], " +
                $"actual [{actualCell?.X ?? (-1)},{actualCell?.Y ?? (-1)},{actualCell?.HasStairs()}] in {actualCell?.ParentZone?.ZoneID}");
        }

        private static GameObject ReadGameObjectMetricsOff(
            this SerializationReader Reader,
            string Forensics = null
            )
        {
            GameObject go = null;
            OptionallyPerformSilently(() => go = Reader.ReadGameObject(Forensics));
            return go;
        }

        public static Cell ReadBonesCell(
            this SerializationReader Reader,
            int x,
            int y,
            Zone ParentZone
            )
        {
            if (Reader == null)
                throw new ArgumentNullException(nameof(Reader));

            var cell = new Cell(ParentZone)
            {
                X = x,
                Y = y
            };


            int writtenObjects = Reader.ReadInt32();
            for (int i = 0; i < writtenObjects; i++)
            {
                if (Reader.ReadGameObjectMetricsOff() is GameObject gameObject)
                {
                    if (gameObject.NeedsFeverWarped(out bool tileOnly, out bool hasBadWord, out _, out _))
                    {
                        gameObject.SetStringProperty(nameof(UD_Bones_FeverWarped), $"{true}");
                        gameObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::TileOnly", $"{tileOnly}");
                        gameObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::HasBadWord", $"{hasBadWord}");
                    }
                    cell.Objects.Add(gameObject);
                    Reader.Locations[gameObject] = cell;
                }
            }

            int blueprintObjects = Reader.ReadInt32();
            for (int j = 0; j < blueprintObjects; j++)
                if (Reader.ReadString() is string writtenBlueprint
                    && GameObject.CreateUnmodified(writtenBlueprint) is GameObject newObject)
                    cell.AddObjectWithoutEvents(newObject);

            cell.PaintTile = Reader.ReadString();
            cell.PaintTileColor = Reader.ReadString();
            cell.PaintRenderString = Reader.ReadString();
            cell.PaintColorString = Reader.ReadString();
            cell.PaintDetailColor = Reader.ReadString();

            int semanticTagsCount = Reader.ReadInt32();
            if (semanticTagsCount > 0)
            {
                cell.SemanticTags = new(semanticTagsCount);
                for (int k = 0; k < semanticTagsCount; k++)
                {
                    if (Reader.ReadString() is string semanticTag)
                        cell.SemanticTags.Add(semanticTag);
                }
            }
            return cell;
        }

        public static GameObject AddObjectWithoutEvents(
            this Cell Cell,
            GameObject GameObject,
            List<GameObject> Tracking = null
            )
        {
            if (Cell == null)
                throw new ArgumentNullException(nameof(Cell));

            if (GameObject == null)
                throw new ArgumentNullException(nameof(GameObject));

            var physics = GameObject.Physics;
            if (physics != null
                && !physics.EnterCell(Cell))
                return GameObject;

            Cell.Objects.Add(GameObject);
            Tracking?.Add(GameObject);

            return GameObject;
        }

        public static void WriteBonesZonePart(this SerializationWriter Writer, IZonePart ZonePart)
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
                ZonePart.Write(ZonePart.ParentZone, Writer);
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

        public static IZonePart ReadBonesZonePart(this SerializationReader Reader, Zone Zone)
        {
            if (Reader == null)
                throw new ArgumentNullException(nameof(Reader));

            Reader.StartBlock(out var Position, out var Length);
            if (Length == 0)
                return null;

            if (Zone == null)
                throw new ArgumentNullException(nameof(Zone));

            Type type = null;
            IZonePart zonePart = null;
            try
            {
                type = Reader.ReadTokenizedType();
                zonePart = Activator.CreateInstance(type) as IZonePart;
                zonePart.ParentZone = Zone;
                zonePart.Read(Zone, Reader);
            }
            catch (Exception exception)
            {
                if (zonePart == null
                    || !zonePart.ReadError(exception, Reader, Position, Length))
                    Reader.SkipBlock(exception, type, Position, Length);
            }

            return zonePart;
        }

        public static void WriteBonesZone(
            this Zone Zone,
            SerializationWriter Writer
            )
        {
            if (Zone == null)
                throw new ArgumentNullException(nameof(Zone));

            if (Writer == null)
                throw new ArgumentNullException(nameof(Writer));

            for (int i = 0; i < Zone.Width; i++)
                for (int j = 0; j < Zone.Height; j++)
                    Zone.Map[i][j].WriteBonesCell(Writer);

            int partsCount = Zone?.Parts?.Count ?? 0;
            Writer.WriteOptimized(partsCount);
            for (int i = 0; i < partsCount; i++)
                if (Zone.Parts[i] is IZonePart zonePart)
                    IZonePart.Save(zonePart, Writer);
        }

        public static void WriteBonesZone(
            this SerializationWriter Writer,
            Zone Zone
            )
        {
            if (Zone == null)
                throw new ArgumentNullException(nameof(Zone));

            if (Writer == null)
                throw new ArgumentNullException(nameof(Writer));

            var type = Zone.GetType();
            Writer.WriteTokenized(type);
            Writer.WriteTypeFields(Zone, type);
            Zone.WriteBonesZone(Writer);
        }

        public static Zone ReadBonesZone(this Zone Zone, SerializationReader Reader)
        {
            if (Zone == null)
                throw new ArgumentNullException(nameof(Zone));

            if (Reader == null)
                throw new ArgumentNullException(nameof(Reader));

            int width = Zone.Width;
            int height = Zone.Height;

            Zone.FloodMap = new int[80, 25];

            var map = Zone.Map = new Cell[width][];
            var missileMap = Zone.MissileMap = new MissileMapType[width][];

            for (int i = 0; i < width; i++)
            {
                map[i] = new Cell[height];
                missileMap[i] = new MissileMapType[height];
            }

            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                    map[i][j] = Reader.ReadBonesCell(i, j, Zone);

            Zone.LightMap = new LightLevel[0];
            Zone.ExploredMap = new bool[0];
            Zone.FakeUnexploredMap = new bool[0];
            Zone.VisibilityMap = new bool[0];
            Zone.ReachableMap = new bool[0, 0];
            Zone.NavigationMap = new NavigationWeight[0, 0];

            int partsCount = Reader.ReadOptimizedInt32();
            if (partsCount > 0)
            {
                Zone.Parts = new List<IZonePart>(partsCount);
                for (int i = 0; i < partsCount; i++)
                {
                    if (IZonePart.Load(Zone, Reader) is IZonePart zonePart)
                        Zone.Parts.Add(zonePart);
                }
            }
            return Zone;
        }

        public static Zone ReadBonesZone(this SerializationReader Reader)
        {
            if (Reader == null)
                throw new ArgumentNullException(nameof(Reader));

            bool forceMetricsOff = Globals.ForceMetricsOff;
            Globals.ForceMetricsOff = false;

            Type type = null;

            OptionallyPerformSilently(() => type = Reader.ReadTokenizedType());

            Globals.ForceMetricsOff = forceMetricsOff;

            if (Activator.CreateInstance(type, nonPublic: true) is not Zone zone)
            {
                Utils.Error($"Failed to create instance of {nameof(Zone)} during Deserialization.");
                return null;
            }

            OptionallyPerformSilently(() => Reader.ReadTypeFields(zone, type));
            zone.Built = false;
            zone.ReadBonesZone(Reader);
            zone.Built = true;
            zone.BroadcastEvent(Event.New("ZoneLoaded"));
            return zone;
        }

        public static void PerformWithoutMetrics(Action Action)
        {
            bool forceMetricsOff = Globals.ForceMetricsOff;
            Globals.ForceMetricsOff = false;
            try
            {
                Action?.Invoke();
            }
            finally
            {
                Globals.ForceMetricsOff = forceMetricsOff;
            }
        }

        public static void OptionallyPerformWithoutMetrics(Action Action, bool WithoutMetricsWhen)
        {
            if (WithoutMetricsWhen)
                PerformWithoutMetrics(Action);
            else
                Action?.Invoke();
        }

        public static void OptionallyPerformWithoutMetrics(Action Action)
            => OptionallyPerformWithoutMetrics(Action, WithoutMetricsWhen: true) // Change true to an eventual new option.
            ;

        public static void PerformWithoutLogging(Action Action)
        {
            bool forceLoggingOff = UnityEngine.Debug.unityLogger.logEnabled;
            UnityEngine.Debug.unityLogger.logEnabled = false;
            try
            {
                Action?.Invoke();
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logEnabled = forceLoggingOff;
            }
        }

        public static void OptionallyPerformWithoutLogging(Action Action, bool WithoutLoggingWhen)
        {
            if (WithoutLoggingWhen)
                PerformWithoutLogging(Action);
            else
                Action?.Invoke();
        }

        public static void OptionallyPerformWithoutLogging(Action Action)
            => OptionallyPerformWithoutLogging(Action, WithoutLoggingWhen: true) // Change true to an eventual new option.
            ;

        public static void PerformSilently(Action Action)
            => PerformWithoutMetrics(()
                => PerformWithoutLogging(Action))
            ;

        public static void OptionallyPerformSilently(Action Action, bool SilentlyWhen)
        {
            if (SilentlyWhen)
                PerformSilently(Action);
            else
                Action?.Invoke();
        }

        public static void OptionallyPerformSilently(Action Action)
            => OptionallyPerformSilently(Action, SilentlyWhen: true) // Change true to an eventual new option.
            ;

        public static void StartMetricsOff(
            this SerializationReader Reader,
            bool SerializePlayer = false
            )
            => OptionallyPerformSilently(() => Reader.Start(SerializePlayer))
            ;

        public static void FinalizeReadMetricsOff(this SerializationReader Reader)
            => OptionallyPerformSilently(() => Reader.FinalizeRead())
            ;

        public static void WriteCompositeHashSet<T>(this SerializationWriter Writer, HashSet<T> CompositeHashSet)
            where T : IComposite, new()
        {
            Writer.WriteOptimized(CompositeHashSet?.Count ?? -1);
            foreach (var item in CompositeHashSet.IteratorSafe())
                Writer.WriteComposite(item);
        }

        public static void WriteStringHashSet(this SerializationWriter Writer, HashSet<string> StringHashSet)
        {
            Writer.WriteOptimized(StringHashSet?.Count ?? -1);
            foreach (var item in StringHashSet.IteratorSafe())
                Writer.WriteOptimized(item);
        }

        public static void WriteIntHashSet(this SerializationWriter Writer, HashSet<int> IntHashSet)
        {
            Writer.WriteOptimized(IntHashSet?.Count ?? -1);
            foreach (var item in IntHashSet.IteratorSafe())
                Writer.WriteOptimized(item);
        }

        public static void WriteHashSet(this SerializationWriter Writer, HashSet<object> HashSet)
        {
            Writer.WriteOptimized(HashSet?.Count ?? -1);
            foreach (var item in HashSet.IteratorSafe())
                Writer.WriteObject(item);
        }

        public static void WriteSpecialHashSet<T>(
            this SerializationWriter Writer,
            HashSet<T> SpecialHashSet,
            Action<SerializationWriter, T> WriteEach
            )
        {
            Writer.WriteOptimized(SpecialHashSet?.Count ?? -1);
            foreach (var item in SpecialHashSet.IteratorSafe())
                WriteEach.Invoke(Writer, item);
        }

        public static HashSet<T> ReadCompositeHashSet<T>(this SerializationReader Reader)
            where T : IComposite, new()
        {
            int count = Reader.ReadOptimizedInt32();
            if (count < 0)
                return null;

            var output = new HashSet<T>(count);
            for (int i = 0; i < count; i++)
                output.Add(Reader.ReadComposite<T>());

            return output;
        }

        public static HashSet<string> ReadStringHashSet(this SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count < 0)
                return null;

            var output = new HashSet<string>(count);
            for (int i = 0; i < count; i++)
                output.Add(Reader.ReadOptimizedString());

            return output;
        }

        public static HashSet<int> ReadIntHashSet(this SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count < 0)
                return null;

            var output = new HashSet<int>(count);
            for (int i = 0; i < count; i++)
                output.Add(Reader.ReadOptimizedInt32());

            return output;
        }

        public static HashSet<object> ReadHashSet(this SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count < 0)
                return null;

            var output = new HashSet<object>(count);
            for (int i = 0; i < count; i++)
                output.Add(Reader.ReadObject());

            return output;
        }

        public static HashSet<T> ReadSpecialHashSet<T>(
            this SerializationReader Reader,
            Func<SerializationReader, T> ReadEach
            )
        {
            int count = Reader.ReadOptimizedInt32();
            if (count < 0)
                return null;

            var output = new HashSet<T>(count);
            for (int i = 0; i < count; i++)
                output.Add(ReadEach.Invoke(Reader));

            return output;
        }

        public static void WriteOptimized(this SerializationWriter Writer, IDictionary<string, string> Dictionary)
        {
            if (Dictionary == null)
            {
                Writer.WriteOptimized(-1);
                return;
            }

            Writer.Write(Dictionary.Keys.Count);
            foreach (KeyValuePair<string, string> item in Dictionary)
            {
                Writer.WriteOptimized(item.Key);
                Writer.WriteOptimized(item.Value);
            }
        }

        public static void WriteCompositeValues<K, V>(this SerializationWriter Writer, IDictionary<K, V> Dictionary)
            where V : IComposite, new()
        {
            if (Dictionary == null)
            {
                Writer.WriteOptimized(-1);
                return;
            }

            Writer.Write(Dictionary.Keys.Count);
            foreach (KeyValuePair<K, V> item in Dictionary)
            {
                Writer.WriteObject(item.Key);
                Writer.WriteComposite(item.Value);
            }
        }

        public static void WriteCompositeKeys<K, V>(this SerializationWriter Writer, IDictionary<K, V> Dictionary)
            where K : IComposite, new()
        {
            if (Dictionary == null)
            {
                Writer.WriteOptimized(-1);
                return;
            }

            Writer.Write(Dictionary.Keys.Count);
            foreach (KeyValuePair<K, V> item in Dictionary)
            {
                Writer.WriteComposite(item.Key);
                Writer.WriteObject(item.Value);
            }
        }

        public static void WriteComposite<K, V>(this SerializationWriter Writer, IDictionary<K, V> Dictionary)
            where K : IComposite, new()
            where V : IComposite, new()
        {
            if (Dictionary == null)
            {
                Writer.WriteOptimized(-1);
                return;
            }

            Writer.Write(Dictionary.Keys.Count);
            foreach (KeyValuePair<K, V> item in Dictionary)
            {
                Writer.WriteComposite(item.Key);
                Writer.WriteComposite(item.Value);
            }
        }

        public static void WriteComposite<V>(this SerializationWriter Writer, IDictionary<string, V> Dictionary)
            where V : IComposite, new()
        {
            if (Dictionary == null)
            {
                Writer.WriteOptimized(-1);
                return;
            }

            Writer.Write(Dictionary.Keys.Count);
            foreach (KeyValuePair<string, V> item in Dictionary)
            {
                Writer.WriteOptimized(item.Key);
                Writer.WriteComposite(item.Value);
            }
        }

        public static Dictionary<string, string> ReadOptimizedStringDictionary(this SerializationReader Reader)
        {
            int count = Reader.ReadOptimizedInt32();
            if (count == -1)
                return null;

           var dictionary = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
                dictionary[Reader.ReadOptimizedString()] = Reader.ReadOptimizedString();

            return dictionary;
        }

        public static Dictionary<K, V> ReadCompositeValues<K, V>(this SerializationReader Reader)
            where V : IComposite, new()
        {
            int count = Reader.ReadOptimizedInt32();
            if (count == -1)
                return null;

           var dictionary = new Dictionary<K, V>(count);
            for (int i = 0; i < count; i++)
                dictionary[(K)Reader.ReadObject()] = Reader.ReadComposite<V>();

            return dictionary;
        }

        public static Dictionary<K, V> ReadCompositeKeys<K, V>(this SerializationReader Reader)
            where K : IComposite, new()
        {
            int count = Reader.ReadOptimizedInt32();
            if (count == -1)
                return null;

            var dictionary = new Dictionary<K, V>(count);
            for (int i = 0; i < count; i++)
                dictionary[Reader.ReadComposite<K>()] = (V)Reader.ReadObject();

            return dictionary;
        }

        public static Dictionary<K, V> ReadComposite<K, V>(this SerializationReader Reader)
            where K : IComposite, new()
            where V : IComposite, new()
        {
            int count = Reader.ReadOptimizedInt32();
            if (count == -1)
                return null;

            var dictionary = new Dictionary<K, V>(count);
            for (int i = 0; i < count; i++)
                dictionary[Reader.ReadComposite<K>()] = Reader.ReadComposite<V>();

            return dictionary;
        }

        public static Dictionary<string, V> ReadComposite<V>(this SerializationReader Reader)
            where V : IComposite, new()
        {
            int count = Reader.ReadOptimizedInt32();
            if (count == -1)
                return null;

            var dictionary = new Dictionary<string, V>(count);
            for (int i = 0; i < count; i++)
                dictionary[Reader.ReadOptimizedString()] = Reader.ReadComposite<V>();

            return dictionary;
        }
    }
}
