using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

namespace UD_Bones_Folder.Mod.UI
{
    public struct PickOptionData<T>
    {
        public T Element;
        public string Text;
        public IRenderable Icon;
        public char Hotkey;

        public Func<T, bool> Callback;

        public PickOptionData(
            T Element,
            string Text,
            IRenderable Icon = null,
            char Hotkey = ' ',
            Func<T, bool> Callback = null
            )
        {
            this.Element = Element;
            this.Text = Text;
            this.Icon = Icon;
            this.Hotkey = Hotkey;
            this.Callback = Callback;
        }

        public PickOptionData(PickOptionData<T> Source)
            : this(Source.Element, Source.Text, Source.Icon, Source.Hotkey, Source.Callback)
        { }

        public PickOptionData(PickOptionData<T> Source, Func<T, bool> Callback)
            : this(Source)
        {
            this.Callback = Callback;
        }

        public readonly bool Invoke()
            => Callback?.Invoke(Element) is true;
    }
}
