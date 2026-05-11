using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Qud.API;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.UI;

using XRL.Collections;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.AI;
using XRL.World.Effects;
using XRL.World.WorldBuilders;

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
        public static List<string> DamageTexts = new()
        {
            "from %t desire it be so.",
            "from immense cringe.",
            "from getting damagemogged by %t attackmaxxing.",
            "from remembering something embarassing you did years ago.",
            "from remembering something embarassing you did just recently.",
            "from the strain of getting to Qud.",
            "from %t disapproving stare.",
        };

        private static bool WishContext;

        public static int MinimumLevelForBones => 5;

        public static BonesManager BonesManager => BonesManager.System;

        public static string BonesName => "LunarRegent";

        public bool BonesMode = false;

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

            GameObject lunarRegent = null;
            try
            {
                lunarRegent = Player.DeepCopy();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GameObject.DeepCopy)} of {nameof(Player)} for Lunar Regent Ascention", x);
                return null;
            }

            using var lunarRegentInventoryList = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(
                items: lunarRegent.Inventory?.Objects ?? Enumerable.Empty<GameObject>());

            var lunarReliquary = GameObject.CreateUnmodified(Const.LUNAR_RELIQUARY_BLUEPRINT);
            var reliquaryInventory = lunarReliquary?.Inventory;

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
            if (E.Killer != null
                && E.Killer.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                lunarRegentPart.IncrementReclaimed();

            if (!Options.DebugEnableNoHoarding
                || WishContext)
            {
                if (E.Dying == ParentObject
                    && ParentObject.Level >= MinimumLevelForBones
                    && ParentObject.CurrentZone is Zone currentZone
                    && !currentZone.IsWorldMap()
                    && !currentZone.GetZoneWorld().EqualsNoCase("Interior"))
                {
                    if (AscendLunarRegent(ParentObject, ParentObject.CurrentCell) is not GameObject lunarRegent)
                    {
                        Utils.Warn($"Failed to ascend player to the Lunar Throne, unable to save bones.");
                        return false;
                    }
                    bool success = true;
                    try
                    {
                        BonesManager.HoardBones(BonesName, E, lunarRegent)?.Wait();
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

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(BeforeTakeActionEvent.ID, EventOrder.EXTREMELY_LATE);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterDieEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(BeforeTakeActionEvent E)
        {
            if (BonesMode)
            {
                BonesMode = false;
                if (ParentObject.CurrentZone?.ZoneID != JoppaWorldBuilder.ID_JOPPA)
                    MakeBones_WishHandler("die -f");
            }
            else
                ParentObject.UnregisterEvent(this, BeforeTakeActionEvent.ID);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterDieEvent E)
        {
            HandleDeathEvent(E);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(The.Game.GameID), The.Game.GameID);
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
            bool noSave = Params?.Contains("eligible") is true;
            bool noAsk = Params?.Contains("-f") is true;

            if (The.Player.Level < MinimumLevelForBones)
            {
                if (noSave
                    || Popup.ShowYesNoCancel($"The minimum level to makes bones is {MinimumLevelForBones}. You are level {The.Player.Level}.\n\n" +
                        $"Would you like your level increased to {MinimumLevelForBones} to complete this operation?"
                        ) == DialogResult.Yes)
                {
                    bool originalPopupSuppress = Popup.Suppress;
                    Popup.Suppress = true;
                    The.Player.AwardXP((Leveler.GetXPForLevel(MinimumLevelForBones) - The.Player.Stat("XP")) + 1);
                    Popup.Suppress = originalPopupSuppress;
                    // Add any other eligibility enforcing code here.
                }
                else
                    return true;
            }
            if (noSave)
                return true;

            if (willDie
                && !noAsk
                && Popup.ShowYesNo($"You have flagged that you would like to die to make these bones.\n\n" +
                    $"Last chance to back out, this will atually, genuinely, end your run."
                    ) != DialogResult.Yes)
                return true;

            WishContext = true;
            GameObject projectile = null;
            try
            {
                if (BonesManager.TryGetSaveBonesByID(gameID, out var saveBonesInfo))
                {
                    var now = DateTime.Now;
                    var timeAgo = now - saveBonesInfo.SaveTimeValue;
                    if (Popup.ShowYesNo($"This game already has a bones file which is {timeAgo.ValueUnits()} old.\n\n" +
                        $"Would you like to proceed, overwriting that file?") != DialogResult.Yes)
                        return true;
                }

                GameObject killer = null;
                GameObject weapon = null;
                try
                {
                    string killerSeed = Utils.CallChain(nameof(MakeBones_WishHandler), nameof(killer));
                    if (BonesModeModule.SeededOddsIn10000(killerSeed, 8000))
                        killer = The.Player.CurrentZone.GetObjects(go => go.IsCombatObject()).GetRandomElement(BonesModeModule.SeededGenerator(killerSeed));

                    killer ??= ZoneManager.instance.CachedObjects?.Values?.Where(GO => GO.IsCombatObject())?.GetRandomElementCosmetic()
                        ?? The.Player;

                    var weapons = Event.NewGameObjectList();
                    killer?.Body?.ForeachDefaultBehavior(go => weapons.Add(go));
                    killer?.Body?.ForeachEquippedObject(go => { if (go.GetPart<MeleeWeapon>()?.IsImprovisedWeapon() is false) weapons.Add(go); });

                    string weaponSeed = Utils.CallChain(nameof(MakeBones_WishHandler), nameof(weapon));
                    weapon = weapons.GetRandomElement(BonesModeModule.SeededGenerator(weaponSeed));

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

                string extraDamageSeed = Utils.CallChain(nameof(MakeBones_WishHandler), nameof(UD_Bones_Folder.Mod.Extensions.GetNotResistedDamageTypes));
                string extraDamageType = The.Player.GetNotResistedDamageTypes().GetRandomElement(BonesModeModule.SeededGenerator(extraDamageSeed));
                if (!extraDamageType.IsNullOrEmpty())
                    extraDamageType = $" {extraDamageType}";

                string accidentalSeed = Utils.CallChain(nameof(MakeBones_WishHandler), nameof(accidentalSeed));
                bool isAccidental = BonesModeModule.SeededOddsIn10000(accidentalSeed, 2500);
                if (!willDie
                    || !The.Player.TakeDamage(
                        Amount: (int)((The.Player.GetStat("Hitpoints")?.BaseValue ?? 99999) * (Stat.Random(135, 225) / 100f)),
                        Message: DamageTexts.GetRandomElementCosmetic(),
                        Attributes: $"IgnoreResist Expected Unavoidable Cosmic Umbral Vorpal{extraDamageType}",
                        Owner: killer,
                        Attacker: killer,
                        Source: projectile ?? weapon,
                        Accidental: isAccidental))
                    AfterDieEvent.Send(
                        Dying: The.Player,
                        Killer: killer,
                        Weapon: weapon,
                        Projectile: projectile,
                        Accidental: isAccidental,
                        Reason: The.Player.Physics?.LastDeathReason,
                        ThirdPersonReason: The.Player.Physics?.LastThirdPersonDeathReason);

                if (originalIDKFA.HasValue)
                    The.Core.IDKFA = originalIDKFA.GetValueOrDefault();

                if (BonesManager.TryGetSaveBonesByID(gameID, out saveBonesInfo, b => b.FileLocationData.Type <= FileLocationData.LocationType.Synced))
                {
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
                try
                {
                    if (!willDie
                        || The.Player.IsAlive
                        || !The.Player.IsDying)
                    {
                        if (currentZone.TryFindLunarRegent(The.Game?.GameID, out GameObject lunarRegent, out UD_Bones_LunarRegent lunarRegentPart))
                            lunarRegentPart.Persists = false;

                        TidyLunarObjectsEvent.SendGameID(Context: "Wish");

                        if (lunarRegent != null)
                            lunarRegent?.Obliterate();
                    }
                }
                catch (Exception x)
                {
                    Utils.Error(nameof(TidyLunarObjectsEvent), x);
                }
                projectile?.Obliterate();
                WishContext = false;
            }
            Popup.Show($"Ran into an issue creating bones. Check the player log for errors!");
            return false;
        }
    }
}
