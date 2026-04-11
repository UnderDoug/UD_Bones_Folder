using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.UI;

using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod
{
    [XRL.HasModSensitiveStaticCache]
    public static class UnityExtensions
    {
        public class UnityElement
        {
            public static int IDCounter;
            public static string RootID
                => $"{0.ToString().PadLeft(IDCounter.ToString().Length, '0')}";

            public int ID;

            public string IDString
                => $"{ID.ToString().PadLeft(IDCounter.ToString().Length, '0')}";

            public Component Component;
            public UnityElement Root;

            private UnityElement _Parent;
            public UnityElement Parent
            {
                get => _Parent;
                set
                {
                    if (_Parent != null
                        && !_Parent.SameAs(value))
                    {
                        _Parent.DirectChildren.Remove(this);
                    }
                    _Parent = value;
                    if (_Parent != null)
                    {
                        _Parent.DirectChildren ??= new();
                        _Parent.DirectChildren.Add(this);
                        /*
                        Utils.Log($"[{IDString ?? "null"}]" +
                            $".{nameof(Parent)}" +
                            $"._set({_Parent?.IDString ?? "null"}, " +
                            $"{nameof(Component)} is {Component?.GetType()?.Name ?? "null"} ({Component?.name ?? "null"}))");
                        Utils.Log($"{1.Indent()}{nameof(DirectChildren)}.{nameof(DirectChildren.Count)}: {(_Parent.DirectChildren?.Count)?.ToString() ?? "null"}");
                        */
                    }
                }
            }

            public List<UnityElement> DirectChildren;

            private IEnumerable<string> _Extras;
            public IEnumerable<string> Extras => _Extras ??= Component.GetTreeExtras();

            public bool IsRoot
                => Root == this
                && Parent == null;

            public int Depth
                => !IsRoot
                ? Parent.Depth + 1
                : 0
                ;

            public UnityElement()
            {
                ID = ++IDCounter;
                DirectChildren ??= new();
            }
            public UnityElement(Component Component)
                : this()
            {
                this.Component = Component;
            }

            public UnityElement SetParent(UnityElement Parent)
            {
                Root ??= Parent?.Root ?? Parent ?? this;
                if (Parent != null)
                    this.Parent = Parent;
                // Utils.Log($"{nameof(SetParent)}(new {nameof(Parent)} is {Parent?.IDString ?? "null"}, {nameof(this.Parent)} is {this.Parent?.IDString ?? "null"})");
                return this;
            }

            public string TypeNameString()
                => $"{Component?.GetType()?.Name ?? "NO_TYPE"} | {Component?.gameObject?.name ?? "NO_NAME"}";

            public string IDStrings()
                => $"[ID:{IDString}(P:{Parent?.IDString ?? RootID})]";

            public string BaseString(bool IncludeID = false)
                => $"{Depth.Indent(MaxIndent: 12)}{(IncludeID ? IDStrings() : null)}{TypeNameString()}, Children: {(DirectChildren?.Count)?.ToString() ?? "null"}";

            public IEnumerable<string> ChildStrings()
            {
                foreach (var child in DirectChildren)
                    yield return child.ToString();
            }

            public override string ToString()
                => ChildStrings()?.Aggregate(
                    seed: Extras?
                        .Aggregate(
                            seed: BaseString(),
                            func: (a, n) => Utils.NewLineDelimitedAggregator(a, $"{(Depth + 1).Indent(MaxIndent: 12)}: {n}"))
                        ?? BaseString(),
                    func: Utils.NewLineDelimitedAggregator)
                ?? BaseString()
                ;

            public bool SameComponentType(UnityElement Other)
                => Component?.GetType() == Other?.Component?.GetType()
                ;

            public bool SameComponentName(UnityElement Other)
                => Component?.name == Other?.Component?.name
                ;

            public bool SameComponent(UnityElement Other)
                => SameComponentType(Other)
                && SameComponentName(Other)
                ;

            public bool SameAs(UnityElement Other)
            {
                if (this == Other)
                    return true;

                if (Other == null)
                    return false;

                if (Depth != Other.Depth)
                    return false;

                if (!SameComponent(Other))
                    return false;

                if (Root?.SameAs(Other.Root) is not true)
                    return false;

                if (Parent?.SameAs(Other.Parent) is not true)
                    return false;

                return true;
            }
        }

        public static IEnumerable<string> GetTreeExtras(this Component Component)
        {
            string label = null;
            string value = null;
            string MakeEntry()
                => $"{label}: {value ?? "NO_VALUE"}";
            using var output = ScopeDisposedList<string>.GetFromPool();
            try
            {
                if (Component is RectTransform rectTransform)
                {
                    label = nameof(rectTransform.rect);
                    value = $"{rectTransform.rect}";
                    output.Add(MakeEntry());

                    label = nameof(rectTransform.position);
                    value = $"{rectTransform.position}";
                    output.Add(MakeEntry());

                    label = nameof(rectTransform.anchoredPosition);
                    value = $"{rectTransform.anchoredPosition}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is UITextSkin uITExtSkin)
                {
                    label = nameof(uITExtSkin.text);
                    value = $"{uITExtSkin.text.ToLiteral()}";
                    output.Add(MakeEntry());

                    label = nameof(uITExtSkin.color);
                    value = $"{uITExtSkin.color}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is Image unityImage)
                {
                    label = nameof(unityImage.color);
                    value = $"{unityImage.color}";
                    output.Add(MakeEntry());

                    label = nameof(unityImage.sprite);
                    value = $"{unityImage.sprite}";
                    output.Add(MakeEntry());

                    label = nameof(unityImage.material);
                    value = $"{unityImage.material}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is LayoutElement layoutElement)
                {
                    label = nameof(layoutElement.preferredHeight);
                    value = $"{layoutElement.preferredHeight}";
                    output.Add(MakeEntry());

                    label = nameof(layoutElement.minHeight);
                    value = $"{layoutElement.minHeight}";
                    output.Add(MakeEntry());

                    label = nameof(layoutElement.preferredWidth);
                    value = $"{layoutElement.preferredWidth}";
                    output.Add(MakeEntry());

                    label = nameof(layoutElement.minWidth);
                    value = $"{layoutElement.minWidth}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is HasSelectionCaret hasSelectionCaret)
                {
                    label = nameof(hasSelectionCaret.selected);
                    value = $"{hasSelectionCaret.selected}";
                    output.Add(MakeEntry());

                    label = nameof(hasSelectionCaret.selectedColor);
                    value = $"{hasSelectionCaret.selectedColor}";
                    output.Add(MakeEntry());

                    label = nameof(hasSelectionCaret.useSelectColor);
                    value = $"{hasSelectionCaret.useSelectColor}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is CanvasRenderer canvasRenderer)
                {
                    label = nameof(canvasRenderer.materialCount);
                    value = $"{canvasRenderer.materialCount}";
                    output.Add(MakeEntry());

                    label = nameof(canvasRenderer.absoluteDepth);
                    value = $"{canvasRenderer.absoluteDepth}";
                    output.Add(MakeEntry());

                    label = nameof(canvasRenderer.relativeDepth);
                    value = $"{canvasRenderer.relativeDepth}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is FrameworkScroller frameworkScroller)
                {
                    label = nameof(frameworkScroller.selectionPrefab);
                    value = $"{frameworkScroller.selectionPrefab?.GetType()?.Name ?? "NO_PREFAB"} | {frameworkScroller.selectionPrefab?.name ?? "NO_PREFAB"}";
                    output.Add(MakeEntry());

                    label = nameof(frameworkScroller.spacerPrefab);
                    value = $"{frameworkScroller.spacerPrefab?.GetType()?.Name ?? "NO_PREFAB"} | {frameworkScroller.spacerPrefab?.name ?? "NO_PREFAB"}";
                    output.Add(MakeEntry());
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Unity didn't like {Component.GetType().Name}'s {label} being looked at", x);
            }

            if (output.IsNullOrEmpty())
                yield break;

            foreach (var item in output)
                yield return item;
        }

        private static IEnumerable<UnityElement> GetComponentTreeInternal(this GameObject GameObject, UnityElement ParentElement = null)
        {
            if (GameObject == null)
                yield break;

            for (int i = 0; i < GameObject.GetComponentCount(); i++)
            {
                if (GameObject.GetComponentAtIndex(i) is Component component)
                {
                    if (new UnityElement(component) is UnityElement element)
                    {
                        element.SetParent(ParentElement);
                        foreach (var subElement in element.GetComponentTree())
                            yield return subElement;
                    }
                }
            }
        }

        public static IEnumerable<UnityElement> GetComponentTree(this GameObject GameObject, UnityElement ParentElement = null, bool RootsOnly = true)
        {
            if (GameObject == null)
                yield break;

            foreach (var element in GameObject.GetComponentTreeInternal(ParentElement))
                if (!RootsOnly
                    || element.IsRoot)
                    yield return element;
        }

        public static IEnumerable<UnityElement> GetComponentTree(this UnityElement UnityElement)
        {
            if (UnityElement == null)
            {
                // Utils.Log($"No element.");
                yield break;
            }

            if (UnityElement.Component == null)
            {
                // Utils.Log($"{UnityElement?.IDString ?? "NO_ID"} had no component.");
                yield break;
            }

            // Utils.Log($"{UnityElement?.IDString ?? "NO_ID"}");
            yield return UnityElement;

            if (UnityElement.Component is Transform transform)
            {
                foreach (var child in transform)
                    if (child is Component childComponent)
                        foreach (var childElement in childComponent.gameObject.GetComponentTree(UnityElement, RootsOnly: false))
                            yield return childElement;
            }
        }

        public static void LogComponentTree(this GameObject GameObject, string Context = null)
        {
            UnityElement.IDCounter = 0;
            using var output = ScopeDisposedList<UnityElement>.GetFromPool();
            var componentTree = GameObject.GetComponentTree();
            foreach (var element in componentTree)
                if (!output.Any(e => element.SameAs(e)))
                    output.Add(element);

            Utils.Log(Context ?? $"{nameof(LogComponentTree)}({GameObject.GetType().Name}: {GameObject.name}):");

            if (!output.IsNullOrEmpty())
                output.Loggregate(
                    Proc: e => e.ToString(),
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}{s}");
        }
    }
}
