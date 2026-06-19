using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

using XRL.Collections;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionDataSet<T, TResult> : Rack<PickOptionData<T, TResult>>, IDisposable
    {
        public T SingleElement;

        /// <summary>
        /// When <see langword="true"/>, indicates that <see cref="SingleElement"/> should be overriden in the event an entry in this collection has an element assigned; otherwise,<br />
        /// when <see langword="false"/>, indicates that <see cref="SingleElement"/> should override each entry's element, irrespective of whether one is assigned to it.
        /// </summary>
        public bool RequireElement = true;

        public PickOptionDataSet()
            : base()
        {
            RequireElement = true;
        }

        public PickOptionDataSet(PickOptionDataSet<T, TResult> Source)
            : base(Source)
        {
            RequireElement = true;
        }

        public PickOptionDataSet(T SingleElement, bool RequireElement = true)
            : this()
        {
            this.SingleElement = SingleElement;
            this.RequireElement = RequireElement;
        }

        public IReadOnlyList<T> GetElements()
        {
            var elements = new List<T>();
            for (int i = 0; i < Count; i++)
            {
                if (RequireElement)
                {
                    var element = this.ElementAtOrDefault(i).Element;
                    if (Equals(element, default))
                        elements.Add(SingleElement);
                }
                else
                    elements.Add(SingleElement);
            }
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

        public bool HasHotkey(char Hotkey)
            => GetHotkeys().Contains(Hotkey)
            ;

        public char GetFirstAvailableHotkey(params char[] Hotkeys)
        {
            if (Hotkeys.IsNullOrEmpty())
                return ' ';

            foreach (var hotkey in Hotkeys)
                if (!HasHotkey(hotkey))
                    return hotkey;

            return ' ';
        }

        public TResult InvokeAt(int Index)
            => this[Index].Invoke()
            ;

        public TResult TryInvokeAt(int Index)
            => this.ElementAtOrDefault(Index).Invoke()
            ;

        public void Clear(bool Dispose)
        {
            if (Dispose)
                foreach (var element in this)
                    element.Dispose();

            Clear();
        }

        public void Dispose()
        {
            Clear(Dispose: true);
        }
    }
}
