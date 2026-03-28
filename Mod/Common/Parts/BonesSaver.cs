using System;

using Bones.Mod;

namespace XRL.World.Parts
{
    [HasCallAfterGameLoaded]
    [PlayerMutator]
    [Serializable]
    public class BonesSaver
        : IPlayerPart
        , IPlayerMutator
    {
        public static BonesManager BonesManager => The.Game?.GetSystem<BonesManager>();

        public static string BonesName => "LunarRegent";

        [CallAfterGameLoaded]
        public static void PreparePlayer()
            => PreparePlayer(The.Player)
            ;

        public static void PreparePlayer(GameObject Player)
        {
            if (Player?.RequirePart<BonesSaver>() is not BonesSaver bonesSaver)
                MetricsManager.LogCallingModError($"Failed to add {nameof(BonesSaver)} to player.");
        }

        public void mutate(GameObject player)
            => PreparePlayer(player)
            ;

        public bool HandleDeathEvent(IDeathEvent E)
        {
            UnityEngine.Debug.Log(E.GetType().Name);
            if (E.Dying == ParentObject
                && ParentObject == The.Player)
            {
                BonesManager.HoardBones(BonesName, E);
                return true;
            }
            return false;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == KilledPlayerEvent.ID
            ;

        public override bool HandleEvent(KilledPlayerEvent E)
        {
            HandleDeathEvent(E);
            return base.HandleEvent(E);
        }
    }
}
