using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionData<T, TResult>
    {
        public T Element;
        public string Text;
        public IRenderable Icon;
        public char Hotkey;

        public Func<T, TResult> Callback;

        public PickOptionData()
        {
        }

        public PickOptionData(
            T Element,
            string Text,
            IRenderable Icon = null,
            char Hotkey = ' ',
            Func<T, TResult> Callback = null
            )
        {
            this.Element = Element;
            this.Text = Text;
            this.Icon = Icon;
            this.Hotkey = Hotkey;
            this.Callback = Callback;
        }

        public PickOptionData(PickOptionData<T, TResult> Source)
            : this(Source.Element, Source.Text, Source.Icon, Source.Hotkey, Source.Callback)
        { }

        public PickOptionData(PickOptionData<T, TResult> Source, Func<T, TResult> Callback)
            : this(Source)
        {
            this.Callback = Callback;
        }

        public virtual TResult Invoke()
        {
            if (Callback != null)
            {
                // Utils.Log($"{Text.Strip()} -> {nameof(Invoke)}");
                return Callback.Invoke(Element);
            }
            return default;
        }
    }
}
