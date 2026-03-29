using System;
using System.Linq;

using Qud.API;

using UD_Bones_Folder.Mod;

using XRL.Collections;
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
        public static UD_Bones_BonesManager BonesManager => UD_Bones_BonesManager.System;

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

        public void mutate(GameObject player)
            => PreparePlayer(player)
            ;

        public bool HandleDeathEvent(IDeathEvent E)
        {
            UnityEngine.Debug.Log(E.GetType().Name);
            if (E.Dying == ParentObject)
            {
                var moonKing = UD_Bones_BonesManager.AscendMoonKing(ParentObject, ParentObject.CurrentCell);
                using var moonKingInventory = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(moonKing.Inventory?.Objects ?? Enumerable.Empty<GameObject>());
                foreach (var moonKingItem in moonKingInventory)
                {
                    moonKingItem.RequirePart<UD_Bones_FragileRoyalObject>();

                    if (moonKingItem.Equipped != null
                        || moonKingItem.GetBlueprint().InheritsFrom("Grenade")
                        || moonKingItem.GetBlueprint().InheritsFrom("Tonic")
                        || moonKingItem.GetBlueprint().InheritsFrom("Projectile")
                        || moonKingItem.GetBlueprint().InheritsFrom("Energy Cell")
                        || moonKingItem.GetBlueprint().InheritsFrom("BaseThrownWeapon"))
                        continue;

                    EquipmentAPI.DropObject(moonKingItem);
                }
                BonesManager.HoardBones(BonesName, E);
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
