using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod;
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

        public PseudoGameObject()
        { }

        public PseudoGameObject(PseudoCell Cell, GameObject GameObject, int Index)
        {
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
        }

        public void UnsetSerializationProps()
        {
            GameObject.SetStringProperty(SerializationExtensions.ACTIVE_OBJECT_PROPERTY, OriginalActive, true);
            GameObject.SetStringProperty(SerializationExtensions.ABILITY_OBJECT_PROPERTY, OriginalAbility, true);
            GameObject.SetStringProperty(SerializationExtensions.GAME_ID_PROPERTY, OriginalGameID, true);
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

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(Address);
            Writer.WriteGameObject(GameObject);
        }

        public virtual void Read(SerializationReader Reader)
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

            // Anything you want to do to objects, do it AFTER here
            // ####################################################

            int serializedBaseID = 0;
            CrossGameObject.Clone.PerformActionRecursively(
                Action: delegate (GameObject go)
                {
                    if (!go.HasPart<UD_Bones_ReportBones>())
                    {
                        if (serializedBaseID == 0)
                            serializedBaseID = GameObject.BaseID;
                        else
                            serializedBaseID = go.BaseID;

                        go.AddPart(new UD_Bones_ReportBones
                        {
                            LoadedBonesID = BonesInfo.ID,
                            SerializedBaseID = serializedBaseID,
                        });
                    }
                });

            PseudoCell.TransmuteBrain(CrossGameObject.Original, CrossGameObject.Clone, OriginObjects, DestinationObjects);
;
            CrossGameObject.Clone.ApplyRegistrar();

            if (CrossGameObject.Clone.TryGetPart(out GivesRep givesRep))
                givesRep.wasParleyed = false;

            if (CrossGameObject.Clone.Energy is Statistic energy)
                energy.BaseValue = 0;

            CrossGameObject.Clone.TryModerate(BonesInfo);

            CrossGameObject.Clone.TryFeverWarp(BonesInfo.ID);

            return CrossGameObject.Clone;
        }

        public void Dispose()
        {
            Address = null;
            GameObject = null;
        }
    }
}
