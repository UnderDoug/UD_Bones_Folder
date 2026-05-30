using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public abstract class ILunarCourtierEvent<T> : ILunarObjectEvent<T>
        where T : ILunarCourtierEvent<T>, new()
    {
        public GameObject LunarRegent;

        public ILunarCourtierEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            LunarRegent = null;
        }

        protected override Event GetStringyEvent()
            => base.GetStringyEvent()
                ?.SetParameter(nameof(LunarRegent), LunarRegent)
            ;

        protected override bool UpdateStringyEvent()
        {
            if (!base.UpdateStringyEvent())
                return false;

            StringyEvent.SetParameter(nameof(LunarRegent), LunarRegent);
            return true;
        }

        protected override bool UpdateFromStringyEvent()
        {
            if (!base.UpdateFromStringyEvent())
                return false;

            LunarRegent = StringyEvent.GetGameObjectParameter(nameof(LunarRegent));
            return true;
        }

        protected static T FromPool(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            string Context
            )
        {
            if (FromPool(
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Context: Context) is not T E)
                return null;

            E.LunarRegent = LunarRegent;

            return E;
        }

        protected static T Process(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            out bool Success,
            string Context = null,
            bool SendToRegent = false
            )
        {
            Success = false;
            if (FromPool(
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                LunarRegent: LunarRegent,
                Context: Context) is not T E)
                return null;
            
            Success = true;

            if (Success)
                Success = E.ProcessForGame();

            if (Success)
                Success = Player?.IsPlayer() is true
                    ? E.ProcessForGameObject(ref Player)
                    : E.ProcessForGameObject(Player)
                    ;

            if (Success)
                Success = E.ProcessForGameObject(E.LunarObject);

            if (SendToRegent
                && Success)
                Success = E.ProcessForGameObject(E.LunarRegent);

            return E;
        }

        public static bool Check(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            string Context = null,
            bool SendToRegent = false
            )
            => Process(
                Player: Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                LunarRegent: LunarRegent,
                Success: out bool success,
                Context: Context,
                SendToRegent: SendToRegent) != null
            && success
            ;

        public static void Send(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            string Context = null,
            bool SendToRegent = false
            )
            => Check(
                Player: Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                LunarRegent: LunarRegent,
                Context: Context,
                SendToRegent: SendToRegent)
            ;

        public static bool Check(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            string Context = null,
            bool SendToRegent = false
            )
            => Process(
                Player: null,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                LunarRegent: LunarRegent,
                Success: out bool success,
                Context: Context,
                SendToRegent: SendToRegent) != null
            && success
            ;

        public static void Send(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            GameObject LunarRegent,
            string Context = null,
            bool SendToRegent = false
            )
            => Check(
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                LunarRegent: LunarRegent,
                Context: Context,
                SendToRegent: SendToRegent)
            ;
    }
}
