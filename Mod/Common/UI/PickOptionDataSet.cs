using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

using XRL.Collections;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionDataSet<T> : Rack<PickOptionData<T>>
    {
        public PickOptionDataSet()
            : base()
        { }
        public PickOptionDataSet(PickOptionDataSet<T> Source)
            : base(Source)
        { }

        public IReadOnlyList<T> GetElements()
        {
            var elements = new List<T>();
            for (int i = 0; i < Count; i++)
                elements.Add(this.ElementAtOrDefault(i).Element);

            return elements;
        }

        public IReadOnlyList<string> GetOptions()
        {
            var options = new List<string>();
            for (int i = 0; i < Count; i++)
                options.Add(this.ElementAtOrDefault(i).Text);

            return options;
        }

        public IReadOnlyList<IRenderable> GetIcons()
        {
            var icons = new List<IRenderable>();
            for (int i = 0; i < Count; i++)
                icons.Add(this.ElementAtOrDefault(i).Icon);

            return icons;
        }

        public IReadOnlyList<char> GetHotkeys()
        {
            var hotkeys = new List<char>();
            for (int i = 0; i < Count; i++)
                hotkeys.Add(this.ElementAtOrDefault(i).Hotkey);

            return hotkeys;
        }

        public void InvokeAt(int Index)
            => this[Index].Invoke()
            ;

        public void TryInvokeAt(int Index)
            => this.ElementAtOrDefault(Index).Invoke();
    }
}
