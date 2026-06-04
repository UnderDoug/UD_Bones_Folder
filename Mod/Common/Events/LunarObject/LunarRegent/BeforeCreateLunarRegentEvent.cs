using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.Language;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class BeforeCreateLunarRegentEvent : ILunarObjectEvent<BeforeCreateLunarRegentEvent>
    {
        protected List<string> Messages;
        protected bool Blocked;

        public BeforeCreateLunarRegentEvent()
        { }

        public override void Reset()
        {
            base.Reset();
            Messages?.Clear();
            Messages = null;
            Blocked = false;
        }

        public void BlockCreation(string Reason = null)
        {
            Blocked = true;
            if (!Reason.IsNullOrEmpty())
            {
                Messages ??= new();
                Messages.Add(Reason);
            }
        }

        public void UnblockCreation()
        {
            Blocked = false;
        }

        public bool IsBlocked()
            => Blocked
            ;

        public IReadOnlyList<string> PreviewMessages()
            => Messages
            ;

        public string GetMessagesAndList()
        {
            if (!Blocked
                || Messages.IsNullOrEmpty())
                return null;

            Messages.RemoveAll(s => s.IsNullOrEmpty());
            return Grammar.MakeAndList(Messages);
        }

        protected override Event GetStringyEvent()
            => GetStringyEvent(
                ForEvent: this,
                EventParams: new KeyValuePair<string, object>[1]
                {
                    new(nameof(Context), Context),
                })
            ;


        protected static BeforeCreateLunarRegentEvent Configure(string Context)
        {
            if (Instance is not BeforeCreateLunarRegentEvent E)
                return null;

            Instance.Reset();

            E.Context = Context;

            E.StringyEvent = E.GetStringyEvent();

            return E;
        }

        protected static BeforeCreateLunarRegentEvent Process(
            GameObject Player,
            out bool Success,
            string Context = null
            )
        {
            Success = false;
            if (Configure(
                Context: Context) is not BeforeCreateLunarRegentEvent E)
                return null;

            Success = true;

            if (Success)
                Success = E.ProcessForGame();

            if (Success)
                Success = Player?.IsPlayer() is true
                    ? E.ProcessForGameObject(ref Player)
                    : E.ProcessForGameObject(Player)
                    ;

            return E;
        }

        public static bool Check(
            GameObject Player,
            out string BlockedReason,
            string Context = null
            )
        {
            BlockedReason = null;
            if (Process(
                Player: Player,
                Success: out bool success,
                Context: Context) is not BeforeCreateLunarRegentEvent E)
                return false;

            if (!success)
            {
                BlockedReason = E.GetMessagesAndList();
                return false;
            }

            return true;
        }
    }
}
