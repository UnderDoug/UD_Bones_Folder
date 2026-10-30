using System;
using System.Collections.Generic;
using System.Text;

using XRL;

namespace UD_Bones_Folder.Mod.Logging
{
    public class Indent : IDisposable
    {
        public static int MaxIndent = 16;

        protected string Channel;

        protected int BaseValue;

        protected int LastValue;

        protected int Factor;

        protected char Char;

        protected Indent()
        {
            BaseValue = 0;
            LastValue = 0;
            Factor = 2;
            Char = ' ';
        }

        protected Indent(string Channel)
            : this()
        {
            Channel ??= "";
            this.Channel = Channel;
            if (Debug.HaveIndents(Channel))
            {
                BaseValue = CapIndent(Debug.LastIndent.LastValue);
                LastValue = CapIndent(Debug.LastIndent.LastValue);
                Factor = Debug.LastIndent.Factor;
                Char = Debug.LastIndent.Char;
            }
            Debug.PushToIndents(Channel, this);
        }

        public Indent(string Channel, int Offset) 
            : this(Channel) 
        {
            BaseValue = CapIndent(BaseValue + Offset);
            LastValue = BaseValue;
        }

        public Indent(ModInfo ModInfo, int Offset) 
            : this(ModInfo?.ID, Offset) 
        { }

        public Indent(int Offset) 
            : this(Debug.GetCallingMod(), Offset) 
        { }

        public Indent this[int Indent]
        {
            get
            {
                if (DebugMethodRegistry.GetDoDebug(Debug.CallingMethodName()))
                    LastValue = CapIndent(BaseValue + Indent);

                return this;
            }
            protected set => SetIndent(value + Indent);
        }

        protected int CapIndent(int Indent)
            => Math.Clamp(Indent, BaseValue, MaxIndent)
            ;

        protected int CapIndent()
            => CapIndent(LastValue)
            ;

        public Indent ResetIndent()
            => ResetIndent(out _)
            ;

        public Indent ResetIndent(out Indent Indent)
        {
            LastValue = BaseValue;
            return Indent = this;
        }

        public Indent SetIndent(int Offset)
        {
            BaseValue = CapIndent(Offset);
            LastValue = BaseValue;
            return this;
        }

        public int GetBaseValue()
            => BaseValue
            ;

        public override string ToString()
            => Char.ThisManyTimes(CapIndent() * Factor)
            ;

        public static implicit operator int(Indent Operand)
            => Operand.LastValue
            ;

        public Indent DiscardIndent()
            => Debug.DiscardIndent(Channel, this)
            ;

        public void Clear()
        {
            Channel = null;
            BaseValue = 0;
            LastValue = 0;
            Factor = 0;
            Char = default;
        }

        public void Dispose()
        {
            do
            {
                DiscardIndent();
            }
            while (Debug.HasIndent(this, out Channel));
        }
    }
}
