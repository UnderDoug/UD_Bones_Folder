using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using HarmonyLib;

using Qud.UI;

using UnityEngine;
using UnityEngine.Events;

using XRL;
using XRL.CharacterBuilds;
using XRL.UI;
using XRL.UI.Framework;
using XRL.Wish;

namespace UD_Bones_Folder.Mod.UI
{
    [HasModSensitiveStaticCache]
    [HasWishCommand]
    [HarmonyDebug]
    [HarmonyPatch]
    public static class MainMenuBones
    {
        private static MainMenuOptionData _MainMenuBonesOptions = null;
        public static MainMenuOptionData MainMenuBonesOptions => _MainMenuBonesOptions ??= new MainMenuOptionData
        {
            Text = "Bones",
            Command = "Pick:Bones",
            Shortcut = UnityEngine.KeyCode.B,
        };

        public static bool DoingBonesManagement;

        public static List<AbstractEmbarkBuilderModule> Modules;
	    public static List<EmbarkBuilderModuleWindowDescriptor> WindowDescriptors;
	    public static List<AbstractBuilderModuleWindowBase> Windows;

        public static EmbarkBuilderModuleWindowDescriptor BonesManagementWindowDescriptor => WindowDescriptors?[0];

        [ModSensitiveCacheInit]
        public static void InsertMainMenuBones()
        {
            if (!MainMenu.LeftOptions.IsNullOrEmpty())
            {
                if (!MainMenu.LeftOptions.Contains(MainMenuBonesOptions))
                {
                    int lastIndex = MainMenu.LeftOptions.Count - 1;
                    int placementIndex = lastIndex;

                    if (MainMenu.LeftOptions.FirstOrDefault(m => m.Text == "Records") is MainMenuOptionData recordsOption)
                        placementIndex = MainMenu.LeftOptions.IndexOf(recordsOption);

                    if (placementIndex == lastIndex)
                        MainMenu.LeftOptions.Add(MainMenuBonesOptions);
                    else
                        MainMenu.LeftOptions.Insert(placementIndex + 1, MainMenuBonesOptions);
                }

                MainMenuBonesOptions.Enabled = BonesManager.HasSavedBones();
            }

        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.SelectedInfo),
            argumentTypes: new Type[] { typeof(FrameworkDataElement), },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal, })]
        [HarmonyPrefix]
        public static async void SelectedInfo_HandleBones_Prefix(FrameworkDataElement data)
        {
            // AddListner();
            DoingBonesManagement = false;
            if (((MainMenuOptionData)data)?.Command == "Pick:Bones")
            {
                if (Options.ModernUI)
                {
                    /*             
                    try
                    {
                        GameManager.Instance.PushGameView("ModernBonesManagement");
                        MainMenu.instance.DisableNavContext();
                        await BonesManagement.Instance.BonesMenu();
                    }
                    finally
                    {
                        GameManager.Instance.PopGameView();
                        MainMenu.instance.Reshow();
                    }
                    */
                    try
                    {
                        await NewGameForBones();
                        // await NavigationController.instance.SuspendContextWhile(delegate () { return Popup.ShowAsync("It Works."); });
                    }
                    finally
                    {
                        DoingBonesManagement = false;
                        UIManager.showWindow("MainMenu");
                    }
                }
            }
            DoingBonesManagement = false;
        }

        public static async Task NewGameForBones()
        {
            DoingBonesManagement = true;
            await Task.Run(() => The.Core.NewGame());
        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.EnableNavContext),
            argumentTypes: new Type[] { },
            argumentVariations: new ArgumentType[] { })]
        [HarmonyPostfix]
        public static void EnableNavContext_ActiveInactiveBones_Postfix()
        {
            MainMenuBonesOptions.Enabled = BonesManager.HasSavedBones();
        }

        public static async Task BeginBones()
        {
            /*
            if (Thread.CurrentThread == GameManager.Instance.uiQueue.threadContext)
                throw new InvalidOperationException("EmbarkBuilder::Begin can only be called from the game thread.");
            */
            
            GameManager.Instance.CurrentGameView = "EmbarkBuilder";
            await The.UiContext;
            GameManager.Instance.SetActiveLayersForNavCategory("Chargen");
            EmbarkBuilder.gameObject.GetComponent<EmbarkBuilder>()?.Destroy();
            var builder = GameManager.Instance.gameObject.AddComponent<EmbarkBuilder>().InitBonesModule();
            try
            {
                /*
                if (WindowDescriptors.FirstOrDefault(descriptor => descriptor.module is BonesManagementModule)?.getWindow() is BonesManagementWindow bonesManagement)
                    await bonesManagement.BonesMenu();
                */
                if (BonesManagementWindowDescriptor != null)
                    builder.ShowWindow(BonesManagementWindowDescriptor);
                else
                    await Popup.ShowAsync($"Failed to load {nameof(BonesManagementWindowDescriptor)}");
            }
            finally
            {
                await The.UiContext;
            }
        }

        public static EmbarkBuilder InitBonesModule(this EmbarkBuilder Builder)
        {
            InitEmbarkBuilderConfiguration();

            Modules = new();
            WindowDescriptors = new();
            Windows = new();

            Modules.AddRange(EmbarkBuilderConfiguration.activeModules);

            foreach (var module in Modules)
            {
                module.builder = Builder;
                module.enable();
            }

            foreach (var module in Modules)
            {
                module.Init();
            }

            foreach (var module in Modules)
            {
                module.assembleWindowDescriptors(WindowDescriptors);
            }
            WindowDescriptors.ForEach(descriptor => descriptor.windowInit(descriptor.getWindow()));
            return Builder;
        }

        [HarmonyPatch(
            declaringType: typeof(EmbarkBuilderConfiguration),
            methodName: nameof(EmbarkBuilderConfiguration.Init),
            argumentTypes: new Type[] { },
            argumentVariations: new ArgumentType[] { })]
        [HarmonyPrefix]
        public static bool Init_DoingBonesInstead_Prefix()
        {
            if (!DoingBonesManagement)
                return true;

            InitEmbarkBuilderConfiguration();
            return false;
        }

        public static void InitEmbarkBuilderConfiguration()
        {
            EmbarkBuilderConfiguration.activeModules = new();
            EmbarkBuilderConfiguration.modules = new();

            Dictionary<string, Action<XmlDataHelper>> handlers = null;
            handlers = new Dictionary<string, Action<XmlDataHelper>>
            {
                {
                    "embarkmodules",
                    delegate(XmlDataHelper xml)
                    {
                        xml.HandleNodes(handlers);
                    }
                },
                { "module", HandleModule }
            };

            foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("EmbarkModules"))
                item.HandleNodes(handlers);

            static void HandleModule(XmlDataHelper xml)
            {
                string className = xml.GetAttribute("Class");

                if (!EmbarkBuilderConfiguration.modules.TryGetValue(className, out AbstractEmbarkBuilderModule module))
                {
                    module = Activator.CreateInstance(ModManager.ResolveType(className)) as AbstractEmbarkBuilderModule;
                    if (className == typeof(BonesManagementModule).ToString())
                    {
                        EmbarkBuilderConfiguration.modules.Add(className, module);
                        EmbarkBuilderConfiguration.activeModules.Add(module);
                    }
                }

                if (module == null)
                {
                    MetricsManager.LogError("Unknown embark builder type: " + className);
                    xml.DoneWithElement();
                }
                else
                    module.HandleNodes(xml);
            }
        }

        [WishCommand(Command = "show UIs")]
        public static void UIWindowPicker_WishHandler()
        {
            var optionNames = new List<string>
            {
                "none"
            };
            optionNames.AddRange(UIManager.instance.windowsByName.Keys);

            var picked = Popup.PickOption(
                Title: "Pick window for info:",
                Options: optionNames);

            if (picked > 0
                && UIManager.instance.windowsByName.Values.ToList()[picked - 1] is WindowBase pickedWIndow)
            {
                List<string> output = new();
                string label = null;
                string value = null;
                string MakeEntry()
                    => $"{label}: {value}";
                try
                {
                    label = nameof(pickedWIndow.name);
                    value = pickedWIndow.name;
                    output.Add(MakeEntry());

                    label = nameof(pickedWIndow.canvasGroup);
                    value = pickedWIndow.canvasGroup.name;
                    output.Add(MakeEntry());

                    label = nameof(pickedWIndow.canvas);
                    value = pickedWIndow.canvas.name;
                    output.Add(MakeEntry());
                }
                catch (Exception x)
                {
                    Utils.Error($"Unity didn't like {label} being looked at", x);
                }
                finally
                {
                    Popup.Show(output.Aggregate("Info".WithColor("W"), Utils.NewLineDelimitedAggregator));
                }
            }
        }
    }
}
