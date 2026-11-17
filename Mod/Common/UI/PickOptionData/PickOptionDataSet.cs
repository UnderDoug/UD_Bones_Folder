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
        public static Comparer<PickOptionData<T, TResult>> PriorityComparer = Comparer<PickOptionData<T, TResult>>.Create(ComparePriority);

        public T SingleElement;

        /// <summary>
        /// When <see langword="true"/>, indicates that <see cref="SingleElement"/> should be overriden in the event an entry in this collection has an element assigned; otherwise,<br />
        /// when <see langword="false"/>, indicates that <see cref="SingleElement"/> should override each entry's element, irrespective of whether one is assigned to it.
        /// </summary>
        public bool PreferAssignedElement = true;

        public PickOptionDataSet()
            : base()
        {
            PreferAssignedElement = true;
        }

        public PickOptionDataSet(PickOptionDataSet<T, TResult> Source)
            : base(Source)
        {
            if (Source != null)
            {
                SingleElement = Source.SingleElement;
                PreferAssignedElement = Source.PreferAssignedElement;
            }
        }

        public PickOptionDataSet(T SingleElement, bool PreferAssignedElement = true)
            : this()
        {
            this.SingleElement = SingleElement;
            this.PreferAssignedElement = PreferAssignedElement;
        }

        public static int ComparePriority(PickOptionData<T, TResult> x, PickOptionData<T, TResult> y)
        {
            if (Utils.EitherNull(x, y, out int nullComp))
                return nullComp;

            return x.Priority.CompareTo(y.Priority);
        }

        public IEnumerable<PickOptionData<T, TResult>> YieldOrderedData()
        {
            var prioritisedData = new Dictionary<int, List<PickOptionData<T, TResult>>>();
            foreach (var data in this.IteratorSafe())
            {
                int key = data.Priority;
                if (!prioritisedData.ContainsKey(key))
                    prioritisedData[key] = new();

                var list = prioritisedData[key];


                list.Add(data);
            }
            foreach ((var _, var dataList) in prioritisedData.IteratorSafe())
                foreach (var data in dataList)
                    yield return data;

            prioritisedData?.Clear();
        }

        public override void Add(PickOptionData<T, TResult> Item)
        {
            base.Add(Item);
            Sort(PriorityComparer);
        }

        public IReadOnlyList<T> GetElements()
            => this
                .Select(d 
                    => !PreferAssignedElement
                        || Equals(d.Element, default)
                    ? SingleElement
                    : d.Element)
                .ToList()
            ;

        public IReadOnlyList<string> GetOptions()
            => this
                .Select(d => d.Text)
                .ToList()
            ;

        public IReadOnlyList<IRenderable> GetIcons()
            => this
                .Select(d => d.Icon)
                .ToList()
            ;

        public IReadOnlyList<char> GetHotkeys()
            => this
                .Select(d => d.Hotkey)
                .ToList()
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

        public override void Clear()
        {
            base.Clear();
        }

        public virtual void Dispose()
        {
            SingleElement = default;
            PreferAssignedElement = default;
            ClearItems(Dispose: true);
        }
    }
}
