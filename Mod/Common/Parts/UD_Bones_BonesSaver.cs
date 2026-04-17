using System;
using System.Linq;
using System.Threading.Tasks;

using Qud.API;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

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

        public static int MinimumLevelForBones => 5;

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

        public static GameObject AscendLunarRegent(
            GameObject Player,
            Cell TargetCell
            )
        {
            if (!Player.CanBeReplicated(Player, BonesName, Temporary: false))
                return null;

            var lunarRegent = Player.DeepCopy();

            using var lunarRegentInventoryList = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(
                items: lunarRegent.Inventory?.Objects ?? Enumerable.Empty<GameObject>());
            var lunarReliquary = GameObject.CreateUnmodified(Const.LUNAR_RELIQUARY_BLUEPRINT);
            Utils.Log($"{nameof(lunarReliquary)}: {lunarReliquary.DebugName ?? "NO_RELIQUARY"}");
            var reliquaryInventory = lunarReliquary?.Inventory;
            Utils.Log($"{1.Indent()}{nameof(reliquaryInventory)}: {(reliquaryInventory != null ? "inventory exists!" : "NO_INVENTORY")}");
            foreach (var lunarRegentItem in lunarRegentInventoryList)
            {
                lunarRegentItem.RequirePart<UD_Bones_FragileLunarObject>();

                if (lunarRegentItem.Equipped != null
                    || lunarRegentItem.GetBlueprint().InheritsFrom("Grenade")
                    || lunarRegentItem.GetBlueprint().InheritsFrom("Tonic")
                    || lunarRegentItem.GetBlueprint().InheritsFrom("Projectile")
                    || lunarRegentItem.GetBlueprint().InheritsFrom("Energy Cell")
                    || lunarRegentItem.GetBlueprint().InheritsFrom("BaseThrownWeapon"))
                    continue;

                if (lunarReliquary != null)
                {
                    lunarRegent.Inventory?.RemoveObject(lunarRegentItem);
                    reliquaryInventory.AddObject(lunarRegentItem, Silent: true);
                    continue;
                }
                lunarRegentItem.SetStringProperty($"{nameof(UD_Bones_FragileLunarObject)}.DropOnLoad", $"{true}");
            }

            if (lunarReliquary != null)
            {
                lunarRegent.Inventory.AddObject(lunarReliquary);
                if (!lunarRegent.Inventory.InventoryContains(lunarReliquary))
                {
                    Utils.Error($"Failed to give {lunarRegent?.DebugName?.Strip() ?? "NO_REGENT"} =subject.possessive= {nameof(lunarReliquary)}"
                        .StartReplace()
                        .AddObject(lunarRegent)
                        );
                    TargetCell.AddObject(lunarReliquary);
                }
            }

            lunarRegent.RestorePristineHealth();

            lunarRegent.RenderForUI("SaveGameInfo", true);

            /*
            var renderEvent = moonKing.RenderForUI("SaveGameInfo", true);

            Utils.Log($"{nameof(RenderEvent)}({nameof(renderEvent.getColorString)}: {renderEvent.getColorString()}, " +
                $"{nameof(renderEvent.GetForegroundColorChar)}: {renderEvent.GetForegroundColorChar()}, " +
                $"{nameof(renderEvent.GetDetailColorChar)}: {renderEvent.GetDetailColorChar()})");

            Utils.Log($"{nameof(Parts.Render)}({nameof(moonKing.Render.getColorString)}: {moonKing.Render.getColorString()}, " +
                $"{nameof(moonKing.Render.GetForegroundColorChar)}: {moonKing.Render.GetForegroundColorChar()}, " +
                $"{nameof(moonKing.Render.getDetailColor)}: {moonKing.Render.getDetailColor()})");
            */

            var brain = lunarRegent.Brain;
            brain.PartyLeader = null;
            brain.Hibernating = false;
            brain.Staying = false;
            brain.Passive = false;
            brain.Factions = "Mean-100,Playerhater-99";
            brain.Allegiance.Hostile = true;
            brain.Allegiance.Calm = false;

            if (Player != null)
            {
                brain.AddOpinion<OpinionMollify>(Player);
                Player.AddOpinion<OpinionMollify>(lunarRegent);
            }

            if (TargetCell == null)
            {
                lunarRegent.Obliterate();
                return null;
            }

            if (lunarRegent.Render is Render render)
                render.Visible = false;

            var lunarRegentPart = lunarRegent.RequirePart<UD_Bones_LunarRegent>();

            lunarRegentPart.Onset();

            if (GameObject.Create("Lunar Face") is GameObject lunarRegentMask)
            {
                if (lunarRegent.ReceiveObject(lunarRegentMask))
                {
                    if (lunarRegentMask.TryGetPart(out UD_Bones_LunarFace lunarFace))
                        lunarFace.TryBeWorn();
                }
                else
                    lunarRegentMask?.Obliterate();
            }

            brain.PerformEquip();

            TargetCell.AddObject(lunarRegent);

            lunarRegent.MakeActive();

            return lunarRegent;
        }

        public void mutate(GameObject player)
            => PreparePlayer(player)
            ;

        public static void BonesExceptionCleanUp(GameObject Player)
            => TidyLunarObjectsEvent.SendGameID(Player, Context: "Exception")
            ;

        public void BonesExceptionCleanUp()
            => BonesExceptionCleanUp(ParentObject)
            ;

        public bool HandleDeathEvent(IDeathEvent E)
        {
            if (!Options.DebugEnableNoHoarding
                || WishContext)
            {
                if (E.Dying == ParentObject
                    && ParentObject.Level >= MinimumLevelForBones
                    && ParentObject.CurrentZone is Zone currentZone
                    && !currentZone.IsWorldMap()
                    && !currentZone.GetZoneWorld().EqualsNoCase("Interior"))
                {
                    var moonKing = AscendLunarRegent(ParentObject, ParentObject.CurrentCell);
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

            if (The.Player.Level < MinimumLevelForBones)
            {
                if (Popup.ShowYesNoCancel($"The minimum level to makes bones is {MinimumLevelForBones}. You are level {The.Player.Level}.\n\n" +
                    $"Would you like your level increased to {MinimumLevelForBones} to complete this operation?") == DialogResult.Yes)
                    The.Player.AwardXP((Leveler.GetXPForLevel(MinimumLevelForBones) - The.Player.Stat("XP")) + 1);
                else
                    return true;
            }

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

                string extraDamageType = Stat.RandomCosmetic(0, 8) switch
                {
                    0 => " Fire",
                    1 => " Cold",
                    2 => " Electric",
                    3 => " Acid",
                    4 => " Bludgeoning",
                    _ => null,
                };

                if (!willDie
                    || !The.Player.TakeDamage(
                        Amount: (int)((The.Player.GetStat("Hitpoints")?.BaseValue ?? 99999) * (Stat.Random(110, 150) / 100f)),
                        Message: "from %t desire it be so.",
                        Attributes: $"Unavoidable Cosmic Umbral Vorpal{extraDamageType}",
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
                    {
                        if (currentZone.TryFindLunarRegent(saveBonesInfo.ID, out GameObject lunarRegent))
                        {
                            foreach (var zoneGO in currentZone.GetObjects())
                            {
                                if (zoneGO.Brain is Brain brain
                                    && brain.PartyLeader == lunarRegent)
                                    brain.SetPartyLeader(The.Player, Silent: true);
                            }
                        }
                        TidyLunarObjectsEvent.SendGameID(Context: "Wish");
                    }

                    Popup.Show($"Created new bones file for {saveBonesInfo.Name.StartReplace()} in {saveBonesInfo.DisplayDirectory}!");
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
