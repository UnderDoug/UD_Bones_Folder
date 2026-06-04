using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class GetLunarRegentEvent : ILunarObjectEvent<GetLunarRegentEvent>
    {
        public GameObject Player;

        public Cell TargetCell;

        public GetLunarRegentEvent()
        { }

        public override void Reset()
        {
            base.Reset();
            Player = null;
            TargetCell = null;
        }

        protected override Event GetStringyEvent()
            => GetStringyEvent(
                ForEvent: this,
                EventParams: new KeyValuePair<string, object>[4]
                {
                    new(nameof(Player), Player),
                    new(nameof(TargetCell), TargetCell),
                    new(nameof(LunarObject), LunarObject),
                    new(nameof(Context), Context),
                })
            ;

        protected override bool UpdateStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            StringyEvent.SetParameter(nameof(Player), Player);
            StringyEvent.SetParameter(nameof(TargetCell), TargetCell);
            StringyEvent.SetParameter(nameof(LunarObject), LunarObject);
            StringyEvent.SetParameter(nameof(Context), Context);

            return true;
        }

        protected override bool UpdateFromStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            Player = StringyEvent.GetGameObjectParameter(nameof(Player));
            TargetCell = StringyEvent.GetParameter<Cell>(nameof(TargetCell));
            LunarObject = StringyEvent.GetGameObjectParameter(nameof(LunarObject));
            Context = StringyEvent.GetStringParameter(nameof(Context));

            return true;
        }

        protected static GetLunarRegentEvent Configure(
            GameObject Player,
            Cell TargetCell,
            GameObject LunarRegent,
            string Context
            )
        {
            if (Instance is not GetLunarRegentEvent E)
                return null;

            Instance.Reset();

            E.Player = Player;
            E.TargetCell = TargetCell;
            E.LunarObject = LunarRegent;
            E.Context = Context;

            E.StringyEvent = E.GetStringyEvent();

            return E;
        }
        protected static GetLunarRegentEvent Process(
            GameObject Player,
            Cell TargetCell,
            GameObject LunarRegent,
            out bool Success,
            string Context = null
            )
        {
            Success = false;
            if (Configure(
                Player: Player,
                TargetCell: TargetCell,
                LunarRegent: LunarRegent,
                Context: Context) is not GetLunarRegentEvent E)
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

            return E;
        }

        public static GameObject GetFor(
            GameObject Player,
            Cell TargetCell,
            GameObject LunarRegent,
            string Context = null
            )
            => The.Game == null
                || Process(
                    Player: Player,
                    TargetCell: TargetCell,
                    LunarRegent: LunarRegent,
                    Success: out bool success,
                    Context: Context) is not GetLunarRegentEvent E
                || !success
            ? null
            : E.LunarObject
            ;
    }
}
