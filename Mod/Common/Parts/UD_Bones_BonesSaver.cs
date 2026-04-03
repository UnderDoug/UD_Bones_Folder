using System;
using System.Linq;

using Qud.API;

using UD_Bones_Folder.Mod;

using XRL.Collections;
using XRL.UI;
using XRL.Wish;
using XRL.World.AI;
using XRL.World.Effects;

using Options = UD_Bones_Folder.Mod.Options;

namespace XRL.World.Parts
{
    [HasCallAfterGameLoaded]
    [PlayerMutator]
    [HasWishCommand]
    [Serializable]
    public class UD_Bones_BonesSaver
        : IPlayerPart
        , IPlayerMutator
    {
        private static bool WishContext;

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

            if (GameObject.Create("Lunar Regent Mask") is GameObject lunarRegentMask)
            {
                if (!moonKing.ReceiveObject(lunarRegentMask))
                {
                    if (lunarRegentMask.TryGetPart(out UD_Bones_LunarFace lunarFace))
                        lunarFace.TryBeWorn();
                }
                else
                    lunarRegentMask?.Obliterate();
            }

            TargetCell.AddObject(moonKing);

            moonKing.MakeActive();

            return moonKing;
        }

        public void mutate(GameObject player)
            => PreparePlayer(player)
            ;

        public bool HandleDeathEvent(IDeathEvent E)
        {
            if (!Options.DebugEnableNoHoarding
                || WishContext)
            {
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

        [WishCommand("make bones")]
        public static bool MakeBones_WishHandler()
        {
            if (The.Player.CurrentZone is not Zone currentZone)
                return false;

            if (The.Game?.GameID is not string gameID)
                return false;

            WishContext = true;
            GameObject projectile = null;
            try
            {
                if (BonesManager.TryGetSaveBonesByID(gameID, out var saveBonesInfo))
                {
                    var now = DateTime.Now;
                    var timeAgo = now - saveBonesInfo.SaveTimeValue;
                    if (Popup.ShowYesNo($"This game already has a bones file which is {timeAgo.ValueUnits():D2} old.\n\n" +
                        $"Would you like to proceed, overwriting that file?") != DialogResult.Yes)
                        return true;
                }

                var killer = ZoneManager.instance.CachedObjects.Values?.GetRandomElementCosmetic()
                    ?? The.Player;

                var weapons = Event.NewGameObjectList();
                killer?.Body?.ForeachDefaultBehavior(go => weapons.Add(go));
                killer?.Body?.ForeachEquippedObject(go => weapons.Add(go));

                var weapon = weapons.GetRandomElementCosmetic();

                if (weapon != null)
                {
                    string projectileeBlueprint = null;
                    GetMissileWeaponProjectileEvent.GetFor(weapon, ref projectile, ref projectileeBlueprint);
                }

                AfterDieEvent.Send(
                    Dying: The.Player,
                    Killer: killer,
                    Weapon: weapon,
                    Projectile: projectile,
                    Accidental: 25.in100(),
                    Reason: The.Player.Physics.LastDeathReason,
                    ThirdPersonReason: The.Player.Physics.LastThirdPersonDeathReason);

                if (BonesManager.TryGetSaveBonesByID(gameID, out saveBonesInfo))
                {
                    foreach ((var bonesObject, var lunarPart) in currentZone.GetObjectsAndPartsWithPartDescendedFrom<UD_Bones_BaseLunarPart>())
                        if (lunarPart.BonesID == gameID)
                            bonesObject.Obliterate();

                    Popup.Show($"Created new bones file for {saveBonesInfo.Name} in {DataManager.SanitizePathForDisplay(saveBonesInfo.Directory)}!");
                    return true;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"MakeBones_WishHandler", x);
            }
            finally
            {
                projectile?.Obliterate();
                WishContext = false;
            }
            Popup.Show($"Ran into an issue creating bones. Check the player log for errors!");
            return false;
        }
    }
}
