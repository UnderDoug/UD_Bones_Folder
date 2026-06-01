using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;

using static UD_Bones_Folder.Mod.Serialization.PseudoTypes.LunarPartyPseudoAddresses;

namespace UD_Bones_Folder.Mod.Serialization.PseudoTypes
{
    [Serializable]
    public class PseudoCell : IComposite, IDisposable
    {
        public bool WantFieldReflection => false;

        // Not Serialized
        public string BonesID;

        // Not Serialized
        public PseudoZone ParentZone;

        public Location2D Location;

        public int X => Location?.X ?? -1;
        public int Y => Location?.Y ?? -1;

        public string PaintTile;
        public string PaintTileColor;
        public string PaintRenderString;
        public string PaintColorString;
        public string PaintDetailColor;

        public List<string> SemanticTags;

        public Rack<PseudoGameObject> Objects;

        public PseudoCell()
        {
            Objects = new();
        }

        public PseudoCell(PseudoZone ParentZone, Location2D Location)
            : this()
        {
            this.ParentZone = ParentZone;
            this.Location = Location;
        }

        public PseudoCell(PseudoZone ParentZone, int X, int Y)
            : this(ParentZone, new(X, Y))
        { }

        public PseudoCell(Location2D Location)
            : this()
        {
            this.Location = Location;
        }

        public PseudoCell(int X, int Y)
            : this(new(X, Y))
        { }

        protected void WriteObjects(SerializationWriter Writer)
        {
            Writer.WriteOptimized(Objects?.Count ?? 0);
            foreach (var pseudoGameObject in Objects.IteratorSafe())
                Writer.WriteComposite(pseudoGameObject);
        }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.Write(Location);

            Writer.WriteOptimized(PaintTile);
            Writer.WriteOptimized(PaintTileColor);
            Writer.WriteOptimized(PaintRenderString);
            Writer.WriteOptimized(PaintColorString);
            Writer.WriteOptimized(PaintDetailColor);

            Writer.Write(SemanticTags);

            WriteObjects(Writer);
        }

        protected void ReadObjects(SerializationReader Reader)
        {
            Objects = new(Math.Max(0, Reader.ReadOptimizedInt32()));
            for (int i = 0; i < Objects.Capacity; i++)
                Objects.Add(Reader.ReadComposite<PseudoGameObject>());
        }

        public virtual void Read(SerializationReader Reader)
        {
            Location = Reader.ReadLocation2D();

            PaintTile = Reader.ReadOptimizedString();
            PaintTileColor = Reader.ReadOptimizedString();
            PaintRenderString = Reader.ReadOptimizedString();
            PaintColorString = Reader.ReadOptimizedString();
            PaintDetailColor = Reader.ReadOptimizedString();

            SemanticTags = Reader.ReadList<string>();

            ReadObjects(Reader);
        }

        public PseudoCell SetParent(PseudoZone PseudoZone)
        {
            ParentZone = PseudoZone;
            BonesID = PseudoZone.BonesID;
            return this;
        }

        private static bool CheckValidPhysics(Cell Cell, GameObject GameObject, bool Silent = true)
        {
            if (GameObject.Physics == null
                || GameObject.Physics._CurrentCell != Cell)
            {
                if (!Silent)
                    Cell.LogInvalidPhysics(GameObject);
                return false;
            }
            return true;
        }

        public static PseudoCell FromCell(Cell Cell)
        {
            if (Cell == null)
                return null;

            var pseudoCell = new PseudoCell
            {
                Location = new(Cell.X, Cell.Y),
                PaintTile = Cell.PaintTile,
                PaintTileColor = Cell.PaintTileColor,
                PaintRenderString = Cell.PaintRenderString,
                PaintColorString = Cell.PaintColorString,
                PaintDetailColor = Cell.PaintDetailColor,
            };

            if (Cell.SemanticTags != null)
            {
                pseudoCell.SemanticTags = new();
                if (Cell.SemanticTags.Count > 0)
                    pseudoCell.SemanticTags.AddRange(Cell.SemanticTags);
            }

            int objectCounter = 0;
            foreach (var gameObject in Cell.LoopObjects())
            {
                if (!CheckValidPhysics(Cell, gameObject)
                    || gameObject.IsPlayer()
                    || !Cell.ShouldWrite(gameObject))
                    continue;

                pseudoCell.Objects ??= new();
                pseudoCell.Objects.Add(new(pseudoCell, gameObject, objectCounter++));
            }

            return pseudoCell;
        }

        public bool MatchesCellLocation(Cell Cell)
            => Cell != null
            && Cell.X == X
            && Cell.Y == Y
            ;

        public static void TransmuteBrain(
            GameObject Original,
            GameObject Clone,
            IEnumerable<GameObject> OriginObjects,
            IEnumerable<GameObject> DestinationObjects
            )
        {
            if (Original.Brain is not Brain originalBrain
                || Clone.Brain is not Brain cloneBrain)
                return;

            Clone.PartyLeader = Original.PartyLeader;
            if (!originalBrain.PartyMembers.IsNullOrEmpty())
                foreach ((int partyMemberID, PartyMember partyMember) in originalBrain.PartyMembers)
                    cloneBrain.PartyMembers[partyMemberID] = partyMember;
            try
            {
                if (!originalBrain.Allegiance.IsNullOrEmpty())
                    (originalBrain.Allegiance, cloneBrain.Allegiance) = (cloneBrain.Allegiance, originalBrain.Allegiance);

                TransferPartyInZone(Original, Clone, OriginObjects);
                TransferPartyInZone(Original, Clone, DestinationObjects);
            }
            catch (Exception x)
            {
                Utils.Error(nameof(TransmuteBrain), x);
            }
        }

        public static void TransferPartyInZone(GameObject Original, GameObject Clone, IEnumerable<GameObject> ZoneObjects)
        {
            if (ZoneObjects == null)
                return;

            foreach (GameObject gameObject in ZoneObjects.IteratorSafe())
            {
                if (gameObject.PartyLeader != Original)
                    continue;

                if (gameObject.TryGetEffect<Proselytized>(out var proselytized))
                {
                    if (!Clone.HasPart<Persuasion_Proselytize>())
                        continue;

                    proselytized.Proselytizer = Clone;
                }

                if (gameObject.TryGetEffect<Beguiled>(out var beguiled))
                {
                    if (!Clone.HasPart<Beguiling>())
                        continue;

                    beguiled.Beguiler = Clone;
                }

                gameObject.PartyLeader = Clone;
                gameObject.Brain.Goals.Clear();
            }
        }

        public bool TryApplyToCell(
            Cell Cell,
            SaveBonesInfo BonesInfo,
            ref GameObject LunarRegent,
            ref Dictionary<string, LunarParty> LunarParties,
            ScopeDisposedList<CrossGameObject> CrossGameObjects,
            Predicate<GameObject> AddWhenNot = null,
            Predicate<GameObject> RemovalExclusions = null,
            bool IgnoreLocationMismatch = false
            )
        {
            if (Cell == null)
            {
                Utils.Error($"Attempted to apply PseudoCell to null {nameof(Cell)}", new ArgumentNullException(nameof(Cell)));
                return false;
            }
            if (!IgnoreLocationMismatch
                && !MatchesCellLocation(Cell))
            {
                Utils.Error(
                    Context: $"Attempted to apply PseudoCell to wrong {nameof(Cell)}",
                    X: new ArgumentException($"Expected {Location?.ToString() ?? "null"}, got {Cell?.Location?.ToString() ?? "null"}", nameof(Cell)));
                return false;
            }

            RemovalExclusions ??= go => false;

            Cell.Clear(
                Important: true,
                Combat: true,
                alsoExclude: RemovalExclusions.Invoke);

            Cell.PaintTile = PaintTile;
            Cell.PaintTileColor = PaintTileColor;
            Cell.PaintRenderString = PaintRenderString;
            Cell.PaintColorString = PaintColorString;
            Cell.PaintDetailColor = PaintDetailColor;

            if (Objects.IsNullOrEmpty())
                return true;

            for (int i = 0; i < Objects.Count; i++)
            {
                if (AddWhenNot?.Invoke(Objects[i]?.GameObject) is true)
                    continue;

                if (Objects[i].PerformExtraction(
                    BonesID: BonesID,
                    OriginObjects: ParentZone.YieldObjects(),
                    DestinationObjects: Cell.ParentZone.YieldObjects(),
                    CrossGameObject: out var crossGameObject) is GameObject bonesObject)
                {
                    string bonesObjectDebugName = bonesObject.DebugName;
                    string catchFlag = "top";
                    try
                    {
                        CrossGameObjects.Add(crossGameObject);

                        if (bonesObject.IsLunarRegent(BonesID))
                        {
                            if (LunarRegent != null
                                && LunarRegent != bonesObject)
                            {
                                Utils.Warn($"Probable error loading {nameof(LunarRegent)} {bonesObject.DebugName}, " +
                                    $"{nameof(LunarRegent)} already assigned {LunarRegent.DebugName}");
                            }
                            LunarRegent = bonesObject;
                        }

                        if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                        {
                            LunarParties ??= new();
                            if (!LunarParties.TryGetValue(lunarRegentPart.BonesID, out var lunarParty))
                                lunarParty = LunarParties[lunarRegentPart.BonesID] = new();

                            lunarParty.SetLunarRegent(bonesObject);
                        }
                        else
                        if (bonesObject.TryGetPart(out UD_Bones_LunarCourtier lunarCoutierPart))
                        {
                            LunarParties ??= new();
                            if (!LunarParties.TryGetValue(lunarCoutierPart.BonesID, out var lunarParty))
                                lunarParty = LunarParties[lunarCoutierPart.BonesID] = new();

                            lunarParty.LunarCourtiers ??= new();
                            lunarParty.LunarCourtiers.Add(bonesObject);
                        }

                        catchFlag = nameof(Cell.AddObject);
                        Cell.AddObject(bonesObject, System: true, Silent: true);

                        catchFlag = nameof(GameObject.MakeActive);
                        bonesObject.MakeActive();

                        catchFlag = nameof(GameObject.ForfeitTurn);
                        bonesObject.ForfeitTurn();
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{nameof(Objects)}[{i}] ({catchFlag}): {bonesObjectDebugName}", x);
                    }
                }
            }
            return true;
        }

        public void Dispose()
        {
            ParentZone = null;

            Location = null;

            PaintTile = null;
            PaintTileColor = null;
            PaintRenderString = null;
            PaintColorString = null;
            PaintDetailColor = null;

            SemanticTags = null;

            Objects = null;
        }
    }
}
