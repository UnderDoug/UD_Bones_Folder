using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

namespace UD_Bones_Folder.Mod.UI
{
    public class CascadablePickOptionDataAsync<T> : PickOptionDataAsync<T, UIUtils.CascadableResult>
    {
        public CascadablePickOptionDataAsync()
            : base()
        { }

        public CascadablePickOptionDataAsync(
            T Element,
            string Text,
            int Default,
            int Priority,
            IRenderable Icon,
            char Hotkey,
            Func<T, Task<UIUtils.CascadableResult>> Callback
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

        public CascadablePickOptionDataAsync(
            T Element,
            string Text,
            IRenderable Icon = null,
            char Hotkey = ' ',
            Func<T, Task<UIUtils.CascadableResult>> Callback = null
            )
            : base(Element, Text, Icon, Hotkey, Callback)
        { }

        public CascadablePickOptionDataAsync(PickOptionDataAsync<T, UIUtils.CascadableResult> Source)
            : base(Source)
        { }

        public CascadablePickOptionDataAsync(PickOptionDataAsync<T, UIUtils.CascadableResult> Source, Func<T, Task<UIUtils.CascadableResult>> Callback)
            : base(Source, Callback)
        { }
    }
}
