using System;
using System.Linq;
using System.Threading.Tasks;

using Qud.API;

using UD_Bones_Folder.Mod;

using XRL.Collections;
using XRL.Rules;
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

            if (TargetCell == null)
            {
                moonKing.Obliterate();
                return null;
            }

            if (moonKing.Render is Render render)
                render.Visible = false;

            var lunarRegentPart = moonKing.RequirePart<UD_Bones_LunarRegent>();

            lunarRegentPart.Onset();

            if (GameObject.Create("Lunar Regent Mask") is GameObject lunarRegentMask)
            {
                if (moonKing.ReceiveObject(lunarRegentMask))
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

        public static void BonesExceptionCleanUp(GameObject Player)
        {
            foreach (var bonesObject in Player?.CurrentZone.GetObjectsWithProperty(nameof(UD_Bones_BaseLunarPart.BonesID)) ?? Enumerable.Empty<GameObject>())
            {
                if (bonesObject.PartsList.Any(p => ((UD_Bones_BaseLunarPart)p).Persists))
                    continue;

                if (bonesObject.GetStringProperty(nameof(UD_Bones_BaseLunarPart.BonesID)) == The.Game.GameID)
                    bonesObject.Obliterate();
            }
        }

        public void BonesExceptionCleanUp()
            => BonesExceptionCleanUp(ParentObject)
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

                    bool success = true;
                    try
                    {
                        BonesManager.HoardBones(BonesName, E, moonKing)?.Wait();
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to serialize bones", x);
                        success = false;
                    }
                    finally
                    {
                        if (!success)
                            BonesExceptionCleanUp();
                    }
                    return success;
                }
            }
            return false;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterDieEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(AfterDieEvent E)
        {
            HandleDeathEvent(E);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(The.Game.GameID), The.Game.GameID);
            if (BonesManager.GetThisRunPendingSaveBonesInfo() is SaveBonesInfo saveBonesInfo)
                E.AddEntry(this, "Pending Bones", saveBonesInfo.GetDebugLines().Aggregate("", Utils.NewLineDelimitedAggregator));
            else
                E.AddEntry(this, "Pending Bones", "none");
            E.AddEntry(this, nameof(Options.DebugEnableNoHoarding), Options.DebugEnableNoHoarding);
            E.AddEntry(this, nameof(Options.DebugEnableNoExhuming), Options.DebugEnableNoExhuming);
            E.AddEntry(this, nameof(Options.DebugEnablePickingBones), Options.DebugEnablePickingBones);
            E.AddEntry(this, nameof(Options.DebugEnableNoCremation), Options.DebugEnableNoCremation);
            return base.HandleEvent(E);
        }

        [WishCommand("make bones")]
        public static bool MakeBones_WishHandler()
            => MakeBones_WishHandler(null)
            ;

        [WishCommand("make bones")]
        public static bool MakeBones_WishHandler(string Params)
        {
            if (The.Player.CurrentZone is not Zone currentZone)
                return false;

            if (The.Game?.GameID is not string gameID)
                return false;

            bool willDie = Params?.Contains("die") is true;

            if (willDie
                && Popup.ShowYesNo($"You have flagged that you would like to die to make these bones.\n\n" +
                $"Last chance to back out, this will atually, genuinely, end your run.") != DialogResult.Yes)
                return true;

            WishContext = true;
            GameObject projectile = null;
            try
            {
                if (BonesManager.TryGetSaveBonesByID(gameID, out var saveBonesInfo))
                {
                    var now = DateTime.Now;
                    var timeAgo = now - saveBonesInfo.SaveTimeValue;
                    if (Popup.ShowYesNo($"This game already has a bones file which is {timeAgo.ValueUnits():0.##} old.\n\n" +
                        $"Would you like to proceed, overwriting that file?") != DialogResult.Yes)
                        return true;
                }

                GameObject killer = null;
                GameObject weapon = null;
                try
                {
                    killer = ZoneManager.instance.CachedObjects?.Values?.Where(GO => GO.IsCombatObject())?.GetRandomElementCosmetic()
                        ?? The.Player;

                    var weapons = Event.NewGameObjectList();
                    killer?.Body?.ForeachDefaultBehavior(go => weapons.Add(go));
                    killer?.Body?.ForeachEquippedObject(go => { if (go.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is false) weapons.Add(go); });

                    weapon = weapons.GetRandomElementCosmetic();

                    if (weapon != null)
                    {
                        string projectileeBlueprint = null;
                        GetMissileWeaponProjectileEvent.GetFor(weapon, ref projectile, ref projectileeBlueprint);
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"Goofed getting random cached combat object and weapon", x);
                    killer = The.Player;
                    weapon = killer.GetPrimaryWeapon();
                    projectile = null;
                }

                bool? originalIDKFA = null;
                if (The.Core.IDKFA
                    && willDie)
                {
                    originalIDKFA = The.Core.IDKFA;
                    The.Core.IDKFA = false;
                }

                if (!willDie
                    || !The.Player.TakeDamage(
                        Amount: (int)(The.Player.GetStatValue("Hitpoints", 99999) * (Stat.Random(80, 120) / 100f)),
                        Message: "from %t desire it be so.",
                        Attributes: "Unavoidable Cosmic Umbral Vorpal Disintegration",
                        Owner: killer,
                        Attacker: killer,
                        Source: projectile ?? weapon,
                        Accidental: 25.in100()))
                    AfterDieEvent.Send(
                        Dying: The.Player,
                        Killer: killer,
                        Weapon: weapon,
                        Projectile: projectile,
                        Accidental: 25.in100(),
                        Reason: The.Player.Physics?.LastDeathReason,
                        ThirdPersonReason: The.Player.Physics?.LastThirdPersonDeathReason);

                if (originalIDKFA.HasValue)
                    The.Core.IDKFA = originalIDKFA.GetValueOrDefault();

                if (BonesManager.TryGetSaveBonesByID(gameID, out saveBonesInfo))
                {
                    if (!willDie)
                        BonesExceptionCleanUp(The.Player);

                    Popup.Show($"Created new bones file for {saveBonesInfo.Name} in {saveBonesInfo.DisplayDirectory}!");
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
