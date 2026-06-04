using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using Qud.API;
using UD_Bones_Folder.Mod.Events;
using System.Linq;
using UD_Bones_Folder.Mod.Parts;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FragileLunarObject
        : UD_Bones_BaseLunarSubject
        , IModEventHandler<AfterPseudoZoneLoadedEvent>
    {
        public bool WantsToBeDropped;

        public bool IsProtected;

        public bool WantsRemoveOnDamage;

        public bool Triggered;

        public override bool CanBeFragile => true;

        public UD_Bones_FragileLunarObject()
            : base()
        { }

        public static BallBag<Func<GameObject, bool>> GetDamageFuncBag()
            => new()
            {
                { MakeItBroken, 750 },
                { MakeItJackedUp, 500 },
                { MakeItRusted, 500 },
                { MakeItVeryDamaged, 275 },
                { MakeItDamaged, 135 },
                { MakeItDented, 10 },
                { LeaveItAlone, 1 },
            };

        public static bool IsBustedOrRusted(GameObject Object, out bool IsBusted, out bool IsRusted)
        {
            IsBusted = false;
            IsRusted = false;
            if (Object == null)
                return false;

            IsBusted = Object.HasEffect<Broken>();
            IsRusted = Object.HasEffect<Rusted>();

            return IsBusted
                || IsRusted
                ;
        }
        public static bool MakeItBroken(GameObject Object)
        {
            if (Object == null)
                return true;

            bool didBust = IsBustedOrRusted(Object, out bool wasBusted, out bool wasRusted)
                || Object.ForceApplyEffect(new Broken())
                ;

            /*Utils.Log($"{1.Indent()}{nameof(MakeItBroken)} - " +
                    $"{nameof(wasBusted)}: {wasBusted}, " +
                    $"{nameof(wasRusted)}: {wasRusted}; " +
                    $"{nameof(didBust)}: {didBust}");*/

            return didBust;
        }

        public static bool MakeItRusted(GameObject Object)
        {
            if (Object == null)
                return true;

            bool didRust = IsBustedOrRusted(Object, out bool wasBusted, out bool wasRusted)
                || Object.ForceApplyEffect(new Rusted())
                ;

            /*Utils.Log($"{1.Indent()}{nameof(MakeItRusted)} - " +
                    $"{nameof(wasBusted)}: {wasBusted}, " +
                    $"{nameof(wasRusted)}: {wasRusted}; " +
                    $"{nameof(didRust)}: {didRust}");*/

            return didRust;
        }

        public static bool DamageItBetweenPercent(GameObject Object, int Low, int High, string Source = null)
        {
            if (Object?.GetStat("Hitpoints") is not Statistic hitpoints)
                return true;

            if (Object.GetPercentDamaged() >= Math.Max(Low, High))
                return true;

            double lowPercent = Math.Clamp(Low, 1, 99) / 100.0;
            double highPercent = Math.Clamp(High, 1, 99) / 100.0;

            Utils.GetMinMax(lowPercent, highPercent, out lowPercent, out highPercent);

            hitpoints.Penalty = 0;

            int high = (int)Math.Floor(hitpoints.BaseValue * highPercent);
            int low = (int)Math.Floor(hitpoints.BaseValue * lowPercent);
            int damage = Stat.RandomCosmetic(low, high);
            int originalDamage = damage;
            damage = Math.Min(hitpoints.BaseValue - 1, damage);

            bool tookDamage = damage > 0
                && Object.TakeDamage(damage, "from =LunarShader:migrating:*= to this reality!", Attributes: "Unavoidable", Environmental: true);

            /*if (Source != null)
                Utils.Log($"{1.Indent()}{Source} - " +
                    $"{lowPercent:#,##0.00}-{highPercent:#,##0.00}; " +
                    $"{low}-{high}/{hitpoints.BaseValue}; " +
                    $"rolled: {damage} ({originalDamage}); " +
                    $"{nameof(tookDamage)}: {tookDamage}");*/

            return tookDamage;
        }

        public static bool MakeItDented(GameObject Object)
            => DamageItBetweenPercent(Object, 10, 25, nameof(MakeItDented))
            ;

        public static bool MakeItDamaged(GameObject Object)
            => DamageItBetweenPercent(Object, 26, 50, nameof(MakeItDamaged))
            ;

        public static bool MakeItVeryDamaged(GameObject Object)
            => DamageItBetweenPercent(Object, 51, 90, nameof(MakeItVeryDamaged))
            ;

        public static bool MakeItJackedUp(GameObject Object)
            => DamageItBetweenPercent(Object, 91, 99, nameof(MakeItJackedUp))
            ;

        public static bool LeaveItAlone(GameObject Object)
            => true
            ;

        public override bool SameAs(IPart p)
        {
            if (p is UD_Bones_FragileLunarObject f)
                return WantsToBeDropped == f.WantsToBeDropped
                    && WantsRemoveOnDamage == f.WantsRemoveOnDamage
                    && Triggered == f.Triggered
                    && IsProtected == f.IsProtected
                    ;

            return base.SameAs(p);
        }

        public bool TryBeDropped()
        {
            if (!WantsToBeDropped)
                return true;

            if (ParentObject?.InInventory?.IsLunarRegent(BonesID) is not true)
                return true;

            EquipmentAPI.DropObject(ParentObject);

            bool wasDropped = ParentObject != null
                && ParentObject.InInventory == null;

            if (wasDropped)
                WantsToBeDropped = false;

            return wasDropped;
        }

        public bool IsHeldByOriginRegent()
        {
            string logText = $"{1.Indent()}{nameof(IsHeldByOriginRegent)}({ParentObject?.DebugName ?? "NO_OBJECT"})";
            if (ParentObject.Holder is not GameObject holder)
            {
                //Utils.Log($"{logText} is not being held.");
                return false;
            }

            if (holder.PartsList?.FirstOrDefault(p => p is IFragileObjectHolderPart) is not IFragileObjectHolderPart lunarPart)
            {
                //Utils.Log($"{logText} holder ({holder?.DebugName ?? "NO_HOLDER"}) lacks requisite Part.");
                return false;
            }

            if (BonesID != null
                && lunarPart.BonesID != BonesID)
            {
                //Utils.Log($"{logText} holder ({holder?.DebugName ?? "NO_HOLDER"}) {nameof(lunarPart)} ({lunarPart.GetType().Name}) has non-matching {nameof(BonesID)}.");
                //Utils.Log($"{2.Indent()}{nameof(BonesID)}: {BonesID}, {nameof(lunarPart)}.{nameof(BonesID)}: {lunarPart.BonesID}, {nameof(The.Game.GameID)}: {The.Game.GameID}");
                return false;
            }

            //Utils.Log($"{logText} holder ({holder?.DebugName ?? "NO_HOLDER"}) {nameof(lunarPart)} ({lunarPart.GetType().Name}) has matching {nameof(BonesID)}: {BonesID}.");
            return true;
        }

        public void AttemptDamage(bool Force, bool Remove, bool Cascade, string Source = null)
        {
            if (ParentObject is not GameObject fragileObject)
                return;

            if (fragileObject.GetBlueprint().InheritsFromSafe("Projectile"))
            {
                Triggered = true;

                if (Remove)
                    fragileObject?.RemovePart(this);
                return;
            }

            if (!Force)
            {
                if (Triggered)
                {
                    if (Remove)
                        fragileObject?.RemovePart(this);
                    return;
                }

                if (IsProtected)
                    return;

                if (IsHeldByOriginRegent())
                    return;
            }

            var damageFuncs = GetDamageFuncBag();
            try
            {
                /*Utils.Log($"{nameof(AttemptDamage)}{(!Source.IsNullOrEmpty() ? $" ({Source})" : null)}: " +
                    $"{fragileObject?.DebugName ?? "NO_OBJECT"}");*/

                int attempts = 0;
                while (!damageFuncs.IsNullOrEmpty()
                    && attempts++ < (damageFuncs.Count * 2))
                {
                    if (damageFuncs.PickOne() is Func<GameObject, bool> damageFunc)
                    {
                        if (fragileObject.Count == 1
                            && damageFunc.Invoke(fragileObject))
                        {
                            //Utils.Log($"{2.Indent()}{nameof(attempts)}: {attempts}; SUCESS");
                            Triggered = true;
                            break;
                        }
                        else
                        {
                            if (fragileObject.Count > 1000)
                                fragileObject.Count = Stat.RandomCosmetic(750, 1000);

                            bool failed = false;
                            while (fragileObject.SplitFromStack() is GameObject splitObject)
                            {
                                if (!damageFunc.Invoke(splitObject))
                                {
                                    failed = true;
                                    break;
                                }

                                if (fragileObject.Count <= 1)
                                    break;
                            }

                            if (failed)
                                continue;

                            //Utils.Log($"{2.Indent()}{nameof(attempts)}: {attempts}; SUCESS");
                            Triggered = true;
                            break;
                        }
                    }
                }

                /*if (!Triggered)
                    Utils.Log($"{2.Indent()}{nameof(attempts)}: {attempts}; FAILED");*/

                if (Triggered
                    && Remove)
                    fragileObject?.RemovePart(this);

                if (Cascade
                    && fragileObject?.Holder is GameObject holder
                    && holder.HasPart<UD_Bones_LunarReliquary>())
                {
                    holder.FireEvent(Event.New(Const.LUNAR_RELIQUARY_TRIGGERED, "TriggeringObject", fragileObject));
                }
            }
            finally
            {
                damageFuncs?.Clear();
                if (Triggered
                    && Remove)
                    fragileObject?.RemovePart(this);
            }
        }

        public void AttemptDamage(bool Force = false, bool Cascade = true, string Source = null)
            => AttemptDamage(
                Force: Force,
                Remove: WantsRemoveOnDamage,
                Cascade: Cascade,
                Source: Source
                )
            ;

        public static bool IsOrIsHeldBy(GameObject FragileObject, GameObject Object)
            => Object == FragileObject
            || Object.Holder == FragileObject
            ;

        public override bool WantTurnTick()
            => true
            ;

        public override void TurnTick(long TimeTick, int Amount)
        {
            AttemptDamage(Source: nameof(TurnTick));
            base.TurnTick(TimeTick, Amount);
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("PerformDrop");
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == TidyLunarObjectsEvent.ID
            || ID == RepairedEvent.ID
            || ID == InventoryActionEvent.ID
            || ID == ModificationAppliedEvent.ID
            || ID == BeforeBeginTakeActionEvent.ID
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == AfterPseudoZoneLoadedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(TidyLunarObjectsEvent E)
        {
            if (GameObject.Validate(ParentObject))
            {
                ParentObject.PerformActionRecursively(delegate (GameObject go)
                {
                    go.RemovePart<UD_Bones_FragileLunarObject>();
                });
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(RepairedEvent E)
        {
            if (ParentObject is GameObject fragileObject
                && fragileObject == E.Subject)
            {
                fragileObject.RemovePart(this);

                if (fragileObject.Holder is GameObject holder
                    && holder.HasPart<UD_Bones_LunarReliquary>())
                    holder.FireEvent(Event.New(Const.LUNAR_RELIQUARY_TRIGGERED, "TriggeringObject", fragileObject));
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (ParentObject is GameObject fragileObject
                && (IsOrIsHeldBy(fragileObject, E.Item)
                    || IsOrIsHeldBy(fragileObject, E.ObjectTarget)))
            {
                bool holderIsReliquary = ParentObject.Holder is GameObject holder
                    && holder.HasPart<UD_Bones_LunarReliquary>();

                if (E.Command == "EmptyForDisassemble"
                    || E.Command == "Disassemble"
                    || E.Command == "DisassembleAll"
                    || E.Command == "LoadMagazineAmmo"
                    || E.Command == "UnloadMagazineAmmo")
                    AttemptDamage(Force: holderIsReliquary, Source: $"{nameof(InventoryActionEvent)}.{nameof(E.Command)} {E.Command} (trigger)");
                else
                if (E.Command != "Look"
                    && E.Command != "ReadStory"
                    && E.Command != "ShowEffects"
                    && E.Command != "ShowInternals"
                    && E.Command != OsseousAsh.ReportBonesInventoryAction.Command)
                    AttemptDamage(Force: holderIsReliquary, Source: $"{nameof(InventoryActionEvent)}.{nameof(E.Command)} {E.Command} (not safe)");
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ModificationAppliedEvent E)
        {
            if (ParentObject == E.Object)
                AttemptDamage(Source: nameof(BeforeBeginTakeActionEvent));

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeBeginTakeActionEvent E)
        {
            if (!TryBeDropped())
                AttemptDamage(Source: $"!TryBeDropped() -> {nameof(BeforeBeginTakeActionEvent)}");

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(DroppedEvent E)
        {
            AttemptDamage(Source: nameof(DroppedEvent));
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterBonesZoneLoadedEvent E)
        {
            if (!TryBeDropped())
                AttemptDamage(Source: $"!TryBeDropped() -> {nameof(AfterBonesZoneLoadedEvent)}");

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterPseudoZoneLoadedEvent E)
        {
            if (base.HandleEvent(E))
            {
                if (!TryBeDropped())
                    AttemptDamage(Source: $"!TryBeDropped() -> {nameof(AfterPseudoZoneLoadedEvent)}");

                return true;
            }

            return false;
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            if (!E.Entries.ContainsKey(nameof(UD_Bones_FragileLunarObject)))
            {
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), "Present", true);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(BonesID), BonesID);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(WantsToBeDropped), WantsToBeDropped);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(IsProtected), IsProtected);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(WantsRemoveOnDamage), WantsRemoveOnDamage);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(Triggered), Triggered);
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "PerformDrop"
                && E.GetGameObjectParameter("Object") == ParentObject)
                AttemptDamage(Source: $"{nameof(UD_Bones_LunarReliquary)}.PerformDrop");

            return base.FireEvent(E);
        }
    }
}
