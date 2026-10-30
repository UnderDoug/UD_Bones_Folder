using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionDataAsync<T, TResult> : PickOptionData<T, Task<TResult>>
    {
        public PickOptionDataAsync()
            : base()
        { }

        public PickOptionDataAsync(
            T Element,
            string Text,
            int Default,
            int Priority,
            IRenderable Icon,
            char Hotkey,
            Func<T, Task<TResult>> Callback
            )
            : base(
                  Element: Element,
                  Text: Text,
                  Default: Default,
                  Priority: Priority,
                  Icon: Icon,
                  Hotkey: Hotkey,
                  Callback: Callback)
        { }

        public PickOptionDataAsync(
            T Element,
            string Text,
            IRenderable Icon = null,
            char Hotkey = ' ',
            Func<T, Task<TResult>> Callback = null
            )
            : base(
                  Element: Element,
                  Text: Text,
                  Icon: Icon,
                  Hotkey: Hotkey,
                  Callback: Callback)
        { }

        public PickOptionDataAsync(PickOptionDataAsync<T, TResult> Source)
            : base(Source)
        { }

        public PickOptionDataAsync(PickOptionDataAsync<T, TResult> Source, Func<T, Task<TResult>> Callback)
            : base(Source, Callback)
        { }
    }
}
