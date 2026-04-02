using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.UI;

using XRL.Collections;
using XRL.UI;

namespace UD_Bones_Folder.Mod
{
    public static class UnityExtensions
    {
        public class UnityElement
        {
            public Component Component;
            public UnityElement Root;
            public UnityElement Parent;

            private IEnumerable<string> _Extras;
            public IEnumerable<string> Extras => _Extras ??= Component.GetTreeExtras();

            public int Depth
                => this != Root
                ? Parent.Depth + 1
                : 0
                ;

            public UnityElement()
            {
            }
            public UnityElement(Component Component, UnityElement Parent = null)
            {
                this.Component = Component;
                this.Parent = Parent;
                Root = Parent?.Root ?? Parent ?? this;
            }

            public string BaseString()
                => $"{Depth.Indent(MaxIndent: 12)}{Component.GetType()}|{Component.gameObject.name}";

            public override string ToString()
                => Extras
                    .Aggregate(
                        seed: BaseString(),
                        func: (a, n) => Utils.NewLineDelimitedAggregator(a, $"{(Depth+2).Indent(MaxIndent: 12)}: {n}"))
                ?? BaseString()
                ;

            public bool SameAs(UnityElement Other)
                => Other != null
                && Component == Other.Component
                && Parent?.Component == Other.Parent?.Component
                && Root?.Component == Other.Root?.Component
                && Depth == Other.Depth
                ;
        }

        public static IEnumerable<string> GetTreeExtras(this Component Component)
        {
            string label = null;
            string value = null;
            string MakeEntry()
                => $"{label}: {value}";
            using var output = ScopeDisposedList<string>.GetFromPool();
            try
            {
                if (Component is RectTransform rectTransform)
                {
                    label = nameof(rectTransform.rect);
                    value = $"{rectTransform.rect}";
                    output.Add(MakeEntry());
                }
                else
                if (Component is UITextSkin uITExtSkin)
                {
                    label = nameof(uITExtSkin.text);
                    value = $"{uITExtSkin.text}";
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

        public static IEnumerable<UnityElement> GetComponentTree(this GameObject GameObject, UnityElement ParentElement = null)
        {
            if (GameObject == null)
                yield break;

            for (int i = 0; i < GameObject.GetComponentCount(); i++)
                if (GameObject.GetComponentAtIndex(i) is Component component)
                    foreach (var element in new UnityElement(component, ParentElement).GetComponentTree())
                        yield return element;
        }

        public static IEnumerable<UnityElement> GetComponentTree(this UnityElement UnityElement)
        {
            if (UnityElement == null
                || UnityElement.Component == null)
                yield break;

            yield return UnityElement;

            if (UnityElement.Component is Transform transform)
            {
                foreach (var child in transform)
                    if (child is Component childComponent)
                        foreach (var childElement in childComponent.gameObject.GetComponentTree(UnityElement))
                            yield return childElement;
            }
        }

        public static void LogComponentTree(this GameObject GameObject, string Context = null)
        {
            using var output = ScopeDisposedList<UnityElement>.GetFromPool();
            foreach (var element in GameObject.GetComponentTree())
                if (!output.Any(e => element.SameAs(e)))
                    output.Add(element);

            Context ??= $"{nameof(LogComponentTree)}({GameObject}):";
            Utils.Log(Context);

            if (!output.IsNullOrEmpty())
                output.Loggregate(
                    Proc: e => e.ToString(),
                    Empty: "empty",
                    PostProc: s => $"{1.Indent()}{s}");
        }
    }
}
