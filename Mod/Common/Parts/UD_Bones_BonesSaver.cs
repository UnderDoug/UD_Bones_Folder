using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using HistoryKit;

using Qud.API;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.UI;

using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
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
        , IModEventHandler<GetLunarRegentEvent>
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

        private const string COMMAND_MAKE_BONES = "CMD_UD_Bones_MakeBones";

        private static bool WishContext;

        public static int MinimumLevelForBones => 5;

        public static BonesManager BonesManager => BonesManager.System;

        public bool? BonesMode = null;

        public Guid MakeBonesActivatedAbilityID;

        protected bool CheckAnnounce;

        public string PetBlueprint;

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
            if (Player == null)
                return null;

            TargetCell ??= Player.CurrentCell;

            if (!BeforeCreateLunarRegentEvent.Check(Player, out string blockedMessage, Context: nameof(AscendLunarRegent)))
            {
                string forReasons = null;
                if (!blockedMessage.IsNullOrEmpty())
                    forReasons = $" for the following reasons: {blockedMessage}";
                Utils.Info($"Creation of Lunar Regent blocked{forReasons}.");
                return null;
            }

            if (!Player.CanBeReplicated(Player, BonesManager.BonesFileName, Temporary: false))
                return null;

            GameObject lunarRegent = null;
            try
            {
                lunarRegent = GetLunarRegentEvent.GetFor(
                    Player: Player,
                    TargetCell: TargetCell,
                    LunarRegent: Player.DeepCopy(),
                    Context: nameof(AscendLunarRegent));
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GameObject.DeepCopy)} of {nameof(Player)} for Lunar Regent Ascention", x);
                return null;
            }

            if (lunarRegent == null)
                return null;

            if (TargetCell == null)
            {
                lunarRegent.Obliterate();
                return null;
            }

            TargetCell.AddObject(lunarRegent);

            lunarRegent.MakeActive();

            AfterCreatedLunarRegentEvent.Send(Player, lunarRegent, Context: nameof(AscendLunarRegent));

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
                        BonesManager.HoardBones(E, lunarRegent)?.Wait();
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

        private bool AddInstantDieAbility(bool Silent = false)
        {
            if (MakeBonesActivatedAbilityID.IsEmptyOrDefault())
            {
                MakeBonesActivatedAbilityID = ParentObject.AddActivatedAbility(
                    Name: "Make Bones",
                    Command: COMMAND_MAKE_BONES,
                    Class: "System",
                    Description: "Instantly die, making a bones file in the process",
                    Silent: Silent,
                    UITileDefault: new Renderable(
                        Tile: "Items/sw_bones_1.bmp,Items/sw_bones_2.bmp,Items/sw_bones_3.bmp,Items/sw_bones_4.bmp,Items/sw_bones_5.bmp,Items/sw_bones_6.bmp,Items/sw_bones_7.bmp,Items/sw_bones_8.bmp".CachedCommaExpansion().GetRandomElementCosmetic(),
                        RenderString: "X",
                        ColorString: "&y",
                        TileColor: "&y",
                        DetailColor: 'Y'));
            }
            return !MakeBonesActivatedAbilityID.IsEmptyOrDefault();
        }

        private bool RemoveInstantDieAbility()
            => MakeBonesActivatedAbilityID.IsEmptyOrDefault()
            || ParentObject.RemoveActivatedAbility(ref MakeBonesActivatedAbilityID)
            ;

        public static bool PreparePetForBonesMode(string PetBlueprint, GameObject Player)
        {
            if (PetBlueprint.IsNullOrEmpty())
                return false;

            if (Player == null)
                return false;

            Utils.Log($"{nameof(PreparePetForBonesMode)}({nameof(PetBlueprint)}: {PetBlueprint}, {nameof(Player)}: {Player?.DebugName ?? "NO_PLAYER"})");

            foreach (var companion in Player.GetCompanions(MaxDistance: 999999).IteratorSafe())
            {
                if (companion.Blueprint == PetBlueprint)
                {
                    Utils.SuppressPopupsWhile(delegate ()
                    {
                        if (Player.Level > companion.Level
                            && companion.TryGetPart(out Leveler companionLeveler)
                            && companion.CurrentCell is Cell companionCell
                            && companion.CurrentZone is Zone companionZone)
                        {
                            int targetLevel = Player.Level;
                            int minXP = Leveler.GetXPForLevel(targetLevel);
                            int maxXP = Leveler.GetXPForLevel(targetLevel + 1) - 1;
                            int xpToAward = Math.Min(Stat.RandomCosmetic(minXP, maxXP), Stat.RandomCosmetic(minXP, maxXP));

                            companion.GetStat("XP").BaseValue = xpToAward;

                            int visMapIndex = companionCell.X + (companionCell.Y * companionZone.Width);
                            var visMap = companionZone.VisibilityMap;
                            bool visibility = visMap[visMapIndex];
                            visMap[visMapIndex] = false;

                            while (companion.GetStatValue("XP") >= Leveler.GetXPForLevel(companion.Level - 1))
                                companionLeveler.LevelUp();

                            visMap[visMapIndex] = visibility;
                        }
                        BonesModeModule.GiveOverTierStuff(companion);
                        BonesModeModule.GiveTierStuff(companion);
                        BonesModeModule.PerformVeryIntelligentPointAssignment(companion);
                        BonesModeModule.GrowLimbs(companion);

                        companion.Brain?.PerformReequip(Silent: true, Initial: true);

                        using (var nonequippedItems = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(companion.GetInventory()))
                        {
                            for (int i = 0; i < nonequippedItems.Count; i++)
                            {
                                if (nonequippedItems[i] is GameObject nonequippedItem
                                    && nonequippedItem.Equipped == null)
                                {
                                    companion.Inventory?.RemoveObjectFromInventory(nonequippedItem, Silent: true);
                                    nonequippedItem?.Obliterate();
                                }
                            }
                        }
                        companion.ForeachEquippedObject(go => go.MakeUnderstood());
                    });

                    return true;
                }
            }

            return false;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(GetLunarRegentEvent.ID, EventOrder.EXTREMELY_EARLY);
            Registrar.Register(EarlyBeforeBeginTakeActionEvent.ID, EventOrder.EXTREMELY_LATE);
            Registrar.Register(EnteringZoneEvent.ID, EventOrder.EXTREMELY_LATE);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterDieEvent.ID
            || ID == CommandEvent.ID
            || ID == AfterPlayerBodyChangeEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public virtual bool HandleEvent(GetLunarRegentEvent E)
        {
            if (E.LunarObject is GameObject lunarRegent)
            {
                lunarRegent.GiveProperName(E.Player.GetReferenceDisplayName(WithoutTitles: true, Short: true), Force: true);

                lunarRegent.SetStringProperty("OriginalPlayerBody", null, RemoveIfNull: true);

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

                var lunarBrain = lunarRegent.Brain;
                lunarBrain.PartyLeader = null;
                lunarBrain.Hibernating = false;
                lunarBrain.Staying = false;
                lunarBrain.Passive = false;
                lunarBrain.Factions = "Mean-100,Playerhater-99";
                lunarBrain.Allegiance.Hostile = true;
                lunarBrain.Allegiance.Calm = false;

                if (lunarRegent.Render is Render render)
                    render.Visible = false;

                var lunarRegentPart = lunarRegent.RequirePart<UD_Bones_LunarRegent>();

                //lunarRegentPart.Onset();

                if (GameObject.Create("Lunar Face") is GameObject lunarRegentMask)
                {
                    if (!lunarRegent.ReceiveObject(lunarRegentMask))
                        lunarRegentMask?.Obliterate();
                    else
                    if (lunarRegentMask.TryGetPart(out UD_Bones_LunarFace lunarFace))
                        lunarFace.TryBeWorn();
                    else
                        Utils.Warn($"Strange unequippable {lunarRegentMask?.DebugName ?? "NO_MASK_OBJECT"} without {nameof(UD_Bones_LunarFace)} part...");
                }

                lunarBrain.PerformEquip();
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterPlayerBodyChangeEvent E)
        {
            if (!MakeBonesActivatedAbilityID.IsEmptyOrDefault())
            {
                E.OldBody.RemoveActivatedAbility(ref MakeBonesActivatedAbilityID);
                AddInstantDieAbility(Silent: true);
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EnteringZoneEvent E)
        {
            Utils.Log($"{nameof(EnteringZoneEvent)}: {E.Cell?.ParentZone?.ZoneID ?? "NO_ZONE_ID"}, from: {ParentObject?.CurrentZone?.ZoneID ?? "NO_ZONE_ID"}");
            if (!CheckAnnounce
                && E.Cell.ParentZone is Zone enteringZone)
            {
                if (enteringZone.HasZoneProperty("BonesID"))
                    CheckAnnounce = true;

                if (BonesManager.ZoneBones[enteringZone.ZoneID] is ZoneBonesAllocation allocation
                    && allocation.HasAssignedBones())
                    CheckAnnounce = true;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (CheckAnnounce)
            {
                AnnounceLunarRegentEvent.Send();
                CheckAnnounce = false;
            }
            if (BonesMode.HasValue)
            {
                bool instant = BonesMode.GetValueOrDefault();
                BonesMode = null;
                AddInstantDieAbility(Silent: instant);
                PreparePetForBonesMode(PetBlueprint, ParentObject);
                if (instant)
                {
                    if (ParentObject.CurrentZone?.ZoneID != JoppaWorldBuilder.ID_JOPPA)
                        MakeBones_WishHandler("die force");
                }
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == COMMAND_MAKE_BONES)
            {
                MakeBones_WishHandler("die ability");
            }
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
            return base.HandleEvent(E);
        }

        [WishCommand("make bones")]
        public static bool MakeBones_WishHandler()
            => MakeBones_WishHandler(null)
            ;

        private static bool IsObjectEligibleToBeKiller(GameObject Object)
        {
            if (Object == null)
                return false;

            if (Object.GetBlueprint() == null)
                return false;

            if (!Object.HasPart("Body")
                || !Object.HasPart("Combat"))
            {
                if (!Object.HasTagOrProperty("BodySubstitute"))
                    return false;
            }
            return true;
        }

        private static bool IsObjectStrictlyEligibleToBeKiller(GameObject Object)
        {
            if (!IsObjectEligibleToBeKiller(Object))
                return false;

            if (Object.GetBlueprint() is not GameObjectBlueprint model)
                return false;

            string objectSeed = $"{nameof(IsObjectStrictlyEligibleToBeKiller)}::{Object.BaseID}";
            int objectSeedIterator = 0;

            if (Object.HasTag("Merchant")
                && !BonesModeModule.SeededOddsIn10000(objectSeed, 2500, objectSeedIterator++))
                return false;

            if (model.InheritsFromSafe("Fungus")
                && !BonesModeModule.SeededOddsIn10000(objectSeed, 2500, objectSeedIterator++))
                return false;

            if (Object.IsPlayerLed()
                && !BonesModeModule.SeededOddsIn10000(objectSeed, 2500, objectSeedIterator++))
                return false;

            return true;
        }

        [WishCommand("make bones")]
        public static bool MakeBones_WishHandler(string Params)
        {
            if (The.Player.CurrentZone is not Zone currentZone)
                return false;

            if (The.Game?.GameID is not string gameID)
                return false;

            bool willDie = Params?.Contains("die") is true;

            bool noSave = (Params?.Contains("eligible") is true)
                || (Params?.Contains("-e") is true);

            bool noAsk = (Params?.Contains("force") is true)
                || (Params?.Contains("-f") is true);

            bool interesting = (Params?.Contains("interesting") is true)
                || (Params?.Contains("-i") is true);

            bool isAbility = (Params?.Contains("ability") is true)
                || (Params?.Contains("-a") is true);

            if (interesting)
            {
                BonesModeModule.SimulateNormalRunProgress(The.Player);
                BonesModeModule.SimulateBeingSomewhereCool(The.Player, false);
                if (Utils.TryGetEmbarkBuilderModule(out QudCustomizeCharacterModule customizeCharacterModule)
                    && customizeCharacterModule?.data?.pet is string petBlueprint)
                    PreparePetForBonesMode(petBlueprint, The.Player);
            }

            if (The.Player.Level < MinimumLevelForBones)
            {
                if (noSave
                    || Popup.ShowYesNoCancel($"The minimum level to makes bones is {MinimumLevelForBones}. You are level {The.Player.Level}.\n\n" +
                        $"Would you like your level increased to {MinimumLevelForBones} to complete this operation?"
                        ) == DialogResult.Yes)
                {
                    Utils.SuppressPopupsWhile(() => The.Player.AwardXP((Leveler.GetXPForLevel(MinimumLevelForBones) - The.Player.Stat("XP")) + 1));
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
            bool hadInstantDieAbility = false;
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
                    var seededGenerator = BonesModeModule.SeededGenerator(killerSeed);
                    if (BonesModeModule.SeededOddsIn10000(killerSeed, 8000))
                        killer = FuzzyFunctions.DoThisButRarelyDoThat(
                            Primary: () => The.Player.CurrentZone.GetObjects(go => go.IsCombatObject() && !go.IsPlayerLed())?.GetRandomElement(seededGenerator),
                            Secondary: () => The.Player.CurrentZone.GetObjects(go => go.IsCombatObject())?.GetRandomElement(seededGenerator),
                            Chance: "10")
                            .Invoke();

                    killer ??= FuzzyFunctions.DoThisButRarelyDoThat(
                        Primary: () => ZoneManager.instance.CachedObjects?.Values?.Where(IsObjectStrictlyEligibleToBeKiller)?.GetRandomElement(seededGenerator),
                        Secondary: () => ZoneManager.instance.CachedObjects?.Values?.Where(IsObjectEligibleToBeKiller)?.GetRandomElement(seededGenerator),
                        Chance: "20")
                        .Invoke();

                    killer ??= The.Player; 

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

                bool? originalAllowRealyDie = null;
                if (Options.DebugEnableIgnoreAllowReallyDie
                    && (noAsk
                        || isAbility)
                    && UI.Options.AllowReallydie
                    && willDie)
                {
                    originalAllowRealyDie = UI.Options.AllowReallydie;
                    UI.Options.AllowReallydie = false;
                }

                string extraDamageSeed = Utils.CallChain(nameof(MakeBones_WishHandler), nameof(UD_Bones_Folder.Mod.Extensions.GetNotResistedDamageTypes));
                string extraDamageType = The.Player.GetNotResistedDamageTypes().GetRandomElement(BonesModeModule.SeededGenerator(extraDamageSeed));
                if (!extraDamageType.IsNullOrEmpty())
                    extraDamageType = $" {extraDamageType}";

                if (The.Player.TryGetPart(out UD_Bones_BonesSaver bonesSaver))
                {
                    if (!bonesSaver.MakeBonesActivatedAbilityID.IsEmptyOrDefault())
                    {
                        hadInstantDieAbility = true;
                        bonesSaver.RemoveInstantDieAbility();
                    }
                }

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

                if (originalAllowRealyDie.HasValue)
                    UI.Options.AllowReallydie = originalAllowRealyDie.GetValueOrDefault();

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
                        if (hadInstantDieAbility
                            && The.Player.TryGetPart(out UD_Bones_BonesSaver bonesSaver))
                        {
                            bonesSaver.AddInstantDieAbility(Silent: true);
                        }

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
