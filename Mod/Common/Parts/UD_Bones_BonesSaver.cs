using System;
using System.Linq;

using Qud.API;

using UD_Bones_Folder.Mod;

using XRL.Collections;
using XRL.World.AI;
using XRL.World.Effects;

namespace XRL.World.Parts
{
    [HasCallAfterGameLoaded]
    [PlayerMutator]
    [Serializable]
    public class UD_Bones_BonesSaver
        : IPlayerPart
        , IPlayerMutator
    {
        public static BonesManager BonesManager => BonesManager.System;

        public static string BonesName => "LunarRegent";


        [CallAfterGameLoaded]
        public static void PreparePlayer()
            => PreparePlayer(The.Player)
            ;

        public static void PreparePlayer(GameObject Player)
        {
            if (Player?.RequirePart<UD_Bones_BonesSaver>() is not UD_Bones_BonesSaver bonesSaver)
                MetricsManager.LogCallingModError($"Failed to add {nameof(UD_Bones_BonesSaver)} to player.");
        }



        public static GameObject AscendMoonKing(
            GameObject Player,
            Cell TargetCell
            )
        {
            if (!Player.CanBeReplicated(Player, BonesName, Temporary: false))
                return null;

            var moonKing = Player.DeepCopy();

            moonKing.RestorePristineHealth();

            moonKing.SetStringProperty(BonesName, The.Game.GameID);
            moonKing.SetStringProperty("UD_Bones_NoWrite", null, true);

            var brain = moonKing.Brain;
            brain.PartyLeader = null;
            brain.Hibernating = false;
            brain.Staying = false;
            brain.Passive = false;
            brain.Factions = "Mean-100,Playerhater-99";
            brain.Allegiance.Hostile = true;
            brain.Allegiance.Calm = false;

            brain.PerformEquip();

            if (Player != null)
            {
                brain.AddOpinion<OpinionMollify>(Player);
                Player.AddOpinion<OpinionMollify>(moonKing);
            }

            string regalTitle = UD_Bones_LunarRegent.GetRegalTitle(Player);

            moonKing.RequirePart<Honorifics>().Primary = regalTitle.WithColor("rainbow");

            if (TargetCell == null)
            {
                moonKing.Obliterate();
                return null;
            }

            if (moonKing.Render is Render render)
                render.Visible = false;

            var lunarRegentPart = moonKing.RequirePart<UD_Bones_LunarRegent>();

            lunarRegentPart.RegalTitle = regalTitle;
            lunarRegentPart.Onset();

            TargetCell.AddObject(moonKing);

            moonKing.MakeActive();

            return moonKing;
        }

        public void mutate(GameObject player)
            => PreparePlayer(player)
            ;

        public bool HandleDeathEvent(IDeathEvent E)
        {
            UnityEngine.Debug.Log(E.GetType().Name);
            if (E.Dying == ParentObject)
            {
                var moonKing = AscendMoonKing(ParentObject, ParentObject.CurrentCell);
                using var moonKingInventory = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(moonKing.Inventory?.Objects ?? Enumerable.Empty<GameObject>());
                foreach (var moonKingItem in moonKingInventory)
                {
                    var fragileObject = moonKingItem.RequirePart<UD_Bones_FragileRoyalObject>();

                    fragileObject.BonesID ??= The.Game.GameID;

                    if (moonKingItem.Equipped != null
                        || moonKingItem.GetBlueprint().InheritsFrom("Grenade")
                        || moonKingItem.GetBlueprint().InheritsFrom("Tonic")
                        || moonKingItem.GetBlueprint().InheritsFrom("Projectile")
                        || moonKingItem.GetBlueprint().InheritsFrom("Energy Cell")
                        || moonKingItem.GetBlueprint().InheritsFrom("BaseThrownWeapon"))
                        continue;

                    EquipmentAPI.DropObject(moonKingItem);
                }
                BonesManager.HoardBones(BonesName, E, moonKing);
                return true;
            }
            return false;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterDieEvent.ID
            ;

        public override bool HandleEvent(AfterDieEvent E)
        {
            HandleDeathEvent(E);
            return base.HandleEvent(E);
        }
    }
}
