using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using Qud.API;
using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FragileLunarObject : UD_Bones_BaseLunarSubject
    {
        public bool WantsToDropOnLoad;

        public bool IsProtected;

        public bool WantsRemoveOnDamage;

        public static BallBag<Func<GameObject, bool>> GetDamageFuncBag()
            => new()
            {
                { MakeItBroken, 350 },
                { MakeItJackedUp, 200 },
                { MakeItRusted, 200 },
                { MakeItVeryDamaged, 75 },
                { MakeItDamaged, 35 },
                { MakeItDented, 10 },
                { LeaveItAlone, 1 },
            };

        public static bool MakeItBroken(GameObject Object)
            => Object?.HasEffect<Broken>() is not false
            || Object?.HasEffect<Rusted>() is not false
            || Object.ForceApplyEffect(new Broken())
            ;

        public static bool MakeItRusted(GameObject Object)
            => Object?.HasEffect<Rusted>() is not false
            || Object?.HasEffect<Broken>() is not false
            || Object.ForceApplyEffect(new Rusted())
            ;

        public static bool DamageItBetweenPercent(GameObject Object, int Low, int High)
        {
            if (Object?.GetStat("Hitpoints") is not Statistic hitpoints)
                return true;

            if (Object.GetPercentDamaged() >= Math.Max(Low, High))
                return true;

            double lowPercent = Math.Clamp(Low, 1, 99) / 100;
            double highPercent = Math.Clamp(High, 1, 99) / 100;

            Utils.GetMinMax(lowPercent, highPercent, out lowPercent, out highPercent);

            hitpoints.Penalty = 0;

            int high = (int)Math.Floor(hitpoints.BaseValue * highPercent);
            int low = (int)Math.Floor(hitpoints.BaseValue * lowPercent);
            int damage = Stat.RandomCosmetic(low, high);

            damage = Math.Min(hitpoints.BaseValue - 1, damage);

            return Object.TakeDamage(damage, "from =LunarShader:migrating:*= to this reality!", Attributes: "Unavoidable", Environmental: true);
        }

        public static bool MakeItDented(GameObject Object)
            => DamageItBetweenPercent(Object, 10, 25)
            ;

        public static bool MakeItDamaged(GameObject Object)
            => DamageItBetweenPercent(Object, 26, 50)
            ;

        public static bool MakeItVeryDamaged(GameObject Object)
            => DamageItBetweenPercent(Object, 50, 90)
            ;

        public static bool MakeItJackedUp(GameObject Object)
            => DamageItBetweenPercent(Object, 91, 99)
            ;

        public static bool LeaveItAlone(GameObject Object)
            => true
            ;

        public bool TryBeDropped()
        {
            if (!WantsToDropOnLoad)
                return false;

            EquipmentAPI.DropObject(ParentObject);

            bool wasDropped = ParentObject != null
                && ParentObject.Holder == null;

            if (wasDropped)
                WantsToDropOnLoad = false;

            return wasDropped;
        }

        public void AttemptDamage(bool Force = false, bool Remove = true)
        {
            if (ParentObject == null)
                    return;

            if (!Force)
            {
                if (IsProtected)
                    return;

                if (ParentObject.InInventory is not GameObject holder)
                    return;

                UD_Bones_BaseLunarPart lunarPart;

                if ((lunarPart = holder.GetPart<UD_Bones_LunarRegent>()) == null
                    && (lunarPart = holder.GetPart<UD_Bones_LunarReliquary>()) == null)
                    return;

                if (lunarPart == null)
                    return;

                if (BonesID != null
                    && lunarPart.BonesID != BonesID)
                    return;
            }

            var damageFuncs = GetDamageFuncBag();
            try
            {
                int attempts = 0;
                while (!damageFuncs.IsNullOrEmpty()
                    && attempts++ < (damageFuncs.Count * 2))
                    if (damageFuncs.PickOne().Invoke(ParentObject))
                        break;
            }
            finally
            {
                if (Remove)
                    ParentObject?.RemovePart(this);
            }
        }

        public override bool WantTurnTick()
            => true
            ;

        public override void TurnTick(long TimeTick, int Amount)
        {
            AttemptDamage(WantsRemoveOnDamage);
            base.TurnTick(TimeTick, Amount);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == EndTurnEvent.ID
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(EndTurnEvent E)
        {
            if (!TryBeDropped())
                AttemptDamage(WantsRemoveOnDamage);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(DroppedEvent E)
        {
            AttemptDamage(WantsRemoveOnDamage);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterBonesZoneLoadedEvent E)
        {
            if (!TryBeDropped())
                AttemptDamage(WantsRemoveOnDamage);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            if (!E.Entries.ContainsKey(nameof(UD_Bones_FragileLunarObject)))
            {
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), "Present", true);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(WantsToDropOnLoad), WantsToDropOnLoad);
                E.AddEntry(nameof(UD_Bones_FragileLunarObject), nameof(IsProtected), IsProtected);
            }
            return base.HandleEvent(E);
        }
    }
}
