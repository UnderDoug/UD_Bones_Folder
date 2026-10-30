using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionData<T, TResult> : IDisposable
    {
        public T Element;
        public string Text;
        public int Default; // highest is default
        public int Priority; // lower is earlier
        public IRenderable Icon;
        public char Hotkey;

        public Func<T, TResult> Callback;

        public PickOptionData()
        {
        }

        public PickOptionData(
            T Element,
            string Text,
            int Default,
            int Priority,
            IRenderable Icon,
            char Hotkey,
            Func<T, TResult> Callback
            )
            : base()
        {
            this.Element = Element;
            this.Text = Text;
            this.Default = Default;
            this.Priority = Priority;
            this.Icon = Icon;
            this.Hotkey = Hotkey;
            this.Callback = Callback;
        }

        public PickOptionData(
            T Element,
            string Text,
            IRenderable Icon = null,
            char Hotkey = ' ',
            Func<T, TResult> Callback = null
            )
            : this(
                  Element: Element,
                  Text: Text,
                  Default: 0,
                  Priority: 0,
                  Icon: Icon,
                  Hotkey: Hotkey,
                  Callback: Callback)
        { }

        public PickOptionData(PickOptionData<T, TResult> Source)
            : this(
                  Element: Source.Element,
                  Text: Source.Text,
                  Default: Source.Default,
                  Priority: Source.Priority,
                  Icon: Source.Icon,
                  Hotkey: Source.Hotkey,
                  Callback: Source.Callback)
        { }

        public PickOptionData(PickOptionData<T, TResult> Source, Func<T, TResult> Callback)
            : this(Source)
        {
            this.Callback = Callback;
        }

        public virtual TResult Invoke(T Element)
        {
            if (Callback != null)
            {
                // Utils.Log($"{Text.Strip()} -> {nameof(Invoke)}");
                return Callback.Invoke(Element);
            }
            return default;
        }

        public virtual TResult Invoke()
            => Invoke(Element)
            ;

        public void Dispose()
        {
            Element = default;
            Text = null;
            Icon = null;
            Hotkey = default;
            Callback = null;
        }
    }
}
