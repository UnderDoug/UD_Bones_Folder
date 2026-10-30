using System;
using System.Collections.Generic;
using System.Linq;
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
            if (Source != null)
            {
                SingleElement = Source.SingleElement;
                RequireElement = Source.RequireElement;
            }
        }

        public PickOptionDataSet(T SingleElement, bool RequireElement = true)
            : this()
        {
            this.SingleElement = SingleElement;
            this.RequireElement = RequireElement;
        }

        public IReadOnlyList<T> GetElements()
            => this.Aggregate(
                seed: new Dictionary<int, List<T>>(),
                func: (a, n) =>
                {
                    int key = n.Priority;
                    if (!a.ContainsKey(key))
                        a[key] = new();

                    var list = a[key];

                    var element = n.Element;
                    if (!RequireElement
                        || Equals(element, default))
                        element = SingleElement;
                    list.Add(element);
                    return a;
                })
                .Values
                .Aggregate(
                    seed: new List<T>(Count),
                    func: (a, n) =>
                    {
                        a.AddRange(n);
                        return a;
                    })
            ;

        public IReadOnlyList<string> GetOptions()
            => this.Aggregate(
                seed: new List<string>(Count),
                func: (a, n) =>
                {
                    a.Add(n.Text);
                    return a;
                })
            ;

        public IReadOnlyList<IRenderable> GetIcons()
            => this.Aggregate(
                seed: new List<IRenderable>(Count),
                func: (a, n) =>
                {
                    a.Add(n.Icon);
                    return a;
                })
            ;

        public IReadOnlyList<char> GetHotkeys()
            => this.Aggregate(
                seed: new List<char>(Count),
                func: (a, n) =>
                {
                    a.Add(n.Hotkey);
                    return a;
                })
            ;

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
            => this[Index].Invoke(GetElements().ElementAtOrDefault(Index))
            ;

        public TResult TryInvokeAt(int Index)
            => this.ElementAtOrDefault(Index).Invoke(GetElements().ElementAtOrDefault(Index))
            ;

        public int GetDefaultIndex()
            => IndexOf(this.Aggregate(
                seed: (PickOptionData<T, TResult>)null,
                func: (a, n) => a is null || n.Default > a.Default ? n : a))
            ;

        public virtual void ClearItems(bool Dispose)
        {
            if (Dispose)
                foreach (var element in this)
                    element?.Dispose();

            ClearItems();
        }

        public virtual void ClearItems()
        {
            Clear();
        }

        public virtual void Dispose()
        {
            SingleElement = default;
            RequireElement = default;
            ClearItems(Dispose: true);
        }
    }
}
