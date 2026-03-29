using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FragileRoyalObject : IScribedPart
    {
        public string BonesID = null;

        public static BallBag<Func<GameObject, bool>> GetDamageFuncBag()
            => new()
            {
                { MakeItBroken, 100 },
                { MakeItJackedUp, 100 },
                { MakeItRusted, 100 },
                { MakeItVeryDamaged, 100 },
                { MakeItDamaged, 100 },
                { MakeItDented, 100 },
                { LeaveItAlone, 100 },
            };

        public override void Attach()
        {
            base.Attach();
            BonesID ??= The.Game.GameID;
        }

        public static bool MakeItBroken(GameObject Object)
            => Object?.HasEffect<Broken>() is not false
            || Object.ForceApplyEffect(new Broken())
            ;

        public static bool MakeItRusted(GameObject Object)
            => Object?.HasEffect<Rusted>() is not false
            || Object.ForceApplyEffect(new Rusted())
            ;

        public static int GetPercentage(int Value, int Total)
            => Value / Total * 100
            ;

        public static int GetPercentCurrentHP(Statistic Hitpoints)
            => Hitpoints.Name == nameof(Hitpoints)
            ? GetPercentage(Hitpoints.Value, Hitpoints.BaseValue)
            : -1
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

            return Object.TakeDamage(damage, "");
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

        public override bool WantTurnTick()
            => true;

        public override void TurnTick(long TimeTick, int Amount)
        {
            if (ParentObject != null)
            {
                if (ParentObject.InInventory is not GameObject holder
                    || !holder.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
                    || lunarRegent.BonesID != BonesID)
                {
                    /*
                     * Return to this.
                     * 
                    var damageFuncs = GetDamageFuncBag();

                    int attempts = 0;
                    while (!damageFuncs.IsNullOrEmpty()
                        && attempts++ < damageFuncs.Count)
                        if (damageFuncs.PickOne()?.Invoke(ParentObject) is not false)
                            break;
                    */
                    ParentObject.RemovePart(this);
                }
            }
            base.TurnTick(TimeTick, Amount);
        }
    }
}
