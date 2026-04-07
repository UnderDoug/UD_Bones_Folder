using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.Collections;
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
            foreach (string semanticTag in Cell?.SemanticTags ?? Enumerable.Empty<string>())
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
                if (Reader.ReadGameObject() is GameObject gameObject)
                {
                    if (gameObject.NeedsFeverWarped())
                        gameObject.SetStringProperty(nameof(UD_Bones_FeverWarped), $"{true}");
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
                throw new ArgumentNullException(nameof(Reader));

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

            var type = Reader.ReadTokenizedType();

            if (Activator.CreateInstance(type, nonPublic: true) is not Zone zone)
            {
                Utils.Error($"Failed to create instance of {nameof(Zone)} during Deserialization.");
                return null;
            }

            Reader.ReadTypeFields(zone, type);
            zone.Built = false;
            zone.ReadBonesZone(Reader);
            zone.Built = true;
            zone.BroadcastEvent(Event.New("ZoneLoaded"));
            return zone;
        }
    }
}
