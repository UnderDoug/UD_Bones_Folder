using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Moderation;
using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Serialization.PseudoTypes.LunarPartyPseudoAddresses;

namespace UD_Bones_Folder.Mod.Serialization.PseudoTypes
{
    [Serializable]
    public class PseudoGameObject : IComposite, IDisposable
    {
        public bool WantFieldReflection => false;

        public PseudoAddress Address;
        public GameObject GameObject;

        // Not Serialized
        protected Cell OriginalCell;

        // Not Serialized
        protected string OriginalActive;
        // Not Serialized
        protected string OriginalAbility;
        // Not Serialized
        protected string OriginalGameID;
        // Not Serialized
        protected string ReplacedRecallStoryID;

        // Not Serialized
        protected UD_Bones_Moderated ModeratedPart;

        public PseudoGameObject()
        { }

        public PseudoGameObject(PseudoCell Cell, GameObject GameObject, int Index)
        {
            // Utils.Log($"{1.Indent()}{nameof(PseudoGameObject)}..ctor({nameof(GameObject)}: {GameObject?.DebugName ?? "NO_OBJECT"})");
            Address = new(Cell, GameObject, Index);
            this.GameObject = GameObject;
        }

        public void Deconstruct(out PseudoAddress Address, out GameObject GameObject)
        {
            Address = this.Address;
            GameObject = this.GameObject;
        }

        public void SetSerializationProps()
        {
            OriginalActive = GameObject.GetStringProperty(SerializationExtensions.ACTIVE_OBJECT_PROPERTY);
            if (The.ActionManager.ActionQueue.Contains(GameObject))
                GameObject.SetStringProperty(SerializationExtensions.ACTIVE_OBJECT_PROPERTY, $"{true}");

            OriginalAbility = GameObject.GetStringProperty(SerializationExtensions.ABILITY_OBJECT_PROPERTY);
            if (The.ActionManager.AbilityObjects.Contains(GameObject))
                GameObject.SetStringProperty(SerializationExtensions.ABILITY_OBJECT_PROPERTY, $"{true}");

            OriginalGameID = GameObject.GetStringProperty(SerializationExtensions.GAME_ID_PROPERTY);
            GameObject.SetStringProperty(SerializationExtensions.GAME_ID_PROPERTY, The.Game.GameID);

            /*ReplacedRecallStoryID = GameObject.GetPropertyOrTag(Const.ORIGINAL_RECALL_STORY_PROP);
            if (ReplacedRecallStoryID != null)
            {
                GameObject.SetStringProperty("Story", ReplacedRecallStoryID);
                GameObject.RemoveStringProperty(Const.ORIGINAL_RECALL_STORY_PROP);
            }*/

            if (GameObject.TryGetPart(out ModeratedPart))
                GameObject.PartsList.Remove(ModeratedPart);
        }

        public void UnsetSerializationProps()
        {
            GameObject.SetStringProperty(SerializationExtensions.ACTIVE_OBJECT_PROPERTY, OriginalActive, true);
            GameObject.SetStringProperty(SerializationExtensions.ABILITY_OBJECT_PROPERTY, OriginalAbility, true);
            GameObject.SetStringProperty(SerializationExtensions.GAME_ID_PROPERTY, OriginalGameID, true);

            /*if (ReplacedRecallStoryID != null)
            {
                GameObject.SetStringProperty(Const.ORIGINAL_RECALL_STORY_PROP, ReplacedRecallStoryID);
                GameObject.SetStringProperty("Story", $"UD_Bones_Missing_Recall_Story {Math.Abs(GameObject.BaseID % 5)}");
            }*/

            if (ModeratedPart != null)
                GameObject.PartsList.Add(ModeratedPart);
        }

        public void UnsetCellForSerialization()
        {
            OriginalCell = GameObject?.CurrentCell;
            //OriginalCell?.RemoveObject(GameObject, System: true, Silent: true, Repaint: false, FlushTransient: false);

            if (GameObject?.Physics is Physics physics)
                physics._CurrentCell = null;
        }

        public void ResetCellForSerialization()
        {
            //OriginalCell?.AddObject(GameObject, System: true, Silent: true, Repaint: false, FlushTransient: false);

            if (GameObject?.Physics is Physics physics)
                physics._CurrentCell = OriginalCell;
        }

        public void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(Address);
            Writer.WriteGameObject(GameObject);
        }

        public void Read(SerializationReader Reader)
        {
            Address = Reader.ReadComposite<PseudoAddress>();
            GameObject = Reader.ReadGameObject();
        }

        public GameObject PerformExtraction(
            SaveBonesInfo BonesInfo,
            IEnumerable<GameObject> OriginObjects,
            IEnumerable<GameObject> DestinationObjects,
            out CrossGameObject CrossGameObject
            )
        {
            CrossGameObject = CrossGameObject.CreateFrom(GameObject);

            var clone = CrossGameObject.Clone;

            // Anything you want to do to objects, do it AFTER here
            // ####################################################

            clone.MakeReportable(BonesInfo);

            PseudoCell.TransmuteBrain(CrossGameObject, OriginObjects, DestinationObjects);
;
            clone.ApplyRegistrar();

            clone.SanitizeParts();

            clone.Body?.UpdateBodyParts();

            if (clone.Energy is Statistic energy)
                energy.BaseValue = 0;

            clone.TryModerate(BonesInfo);

            clone.TryFeverWarp(BonesInfo.ID);

            return clone;
        }

        public void Dispose()
        {
            Address = null;
            GameObject = null;
        }
    }
}
