using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Qud.UI;

using UnityEngine;
using UnityEngine.UI;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.UI;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;

using Event = XRL.UI.Framework.Event;

namespace UD_Bones_Folder.Mod.UI
{
    [UIView(BONES_MANAGEMENT_WINDOW_ID,
        NavCategory = "Menu",
        UICanvas = "SaveManagement",
        UICanvasHost = 1)]
    public class BonesManagement : SingletonWindowBase<BonesManagement>, ControlManager.IControllerChangedEvent
    {
        public const string BONES_MANAGEMENT_WINDOW_ID = "UD_BonesFolderManagement";

        public const string CMD_INSERT = "CmdInsert";

        public static MenuOption BACK_BUTTON => EmbarkBuilderOverlayWindow.BackMenuOption;

        private static Transform MenuBarParentTransfrom;
        private static Vector2 MenuBarAnchoredPos;
        private static Vector2 MenuBarAnchorMin;
        private static Vector2 MenuBarAnchorMax;

        private static RectTransform BonesScrollerVertScroll;
        private static VerticalLayoutGroup VLayout;

        private static HasSelectionCaret _SelectionCaret;
        private static HasSelectionCaret SelectionCaret => _SelectionCaret ??= Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar)
                ?.GetComponentInChildren<HasSelectionCaret>();

        public static List<MenuOption> LegendBarOptions = new List<MenuOption>
        {
            new MenuOption
            {
                InputCommand = "NavigationXYAxis",
                Description = "navigate"
            },
            new MenuOption
            {
                KeyDescription = ControlManager.getCommandInputDescription("Accept"),
                Description = "select"
            },
            new MenuOption
            {
                KeyDescription = ControlManager.getCommandInputDescription("CmdDelete"),
                Description = "cremate"
            }
        };

        public static List<MenuOption> MenuBarOptions = new List<MenuOption>
        {
            new MenuOption
            {
                InputCommand = "CmdInsert",
                Description = "cremate all"
            },
        };

        public static bool WishContext;

        public SaveBonesInfo Preselected;

        public Image Background;

        public FrameworkScroller LegendBar;

        public FrameworkScroller AllBonesMenuBar;

        public FrameworkScroller BonesScroller;

        protected List<FrameworkDataElement> Bones;

        public EmbarkBuilderModuleBackButton BackButton;

        public TaskCompletionSource<SaveBonesInfo> CompletionSource;

        public NavigationContext MainNavContext = new();

        public ScrollContext<NavigationContext> MidHorizNav = new();

        private bool SelectFirst = true;

        public bool WasInScroller;

        public static bool Printed = false;

        protected bool MoveAllBonesMenuBar;
        protected bool MoveLegendBar;

        public Dictionary<SaveManagementRow, GameObject> SelectionChoiceSyncButtons = new();

        protected static void InitializeWithUIManager()
        {
            if (instance == null)
            {
                try
                {
                    var bonesManagementWindow = UIManager
                        .createWindow(
                            name: BONES_MANAGEMENT_WINDOW_ID,
                            scriptType: typeof(BonesManagement)
                        ) as BonesManagement;

                    bonesManagementWindow.Init();
                    bonesManagementWindow.name = "Bones Management";
                }
                catch (Exception x)
                {
                    Utils.Error(new NullReferenceException($"{nameof(UIManager)} didn't like creating a {nameof(BonesManagement)} window", x));
                }
            }
            if (instance == null)
                Utils.Error($"{nameof(BonesManagement)} window failed to initialize", new NullReferenceException($"{nameof(instance)} must not be null"));
        }

        public static bool CheckInit()
        {
            if (instance == null)
                InitializeWithUIManager();

            return instance != null;
        }

        public override void Init()
        {
            //Utils.Log($"{nameof(BonesManagement)}.{nameof(Init)}");
            base.Init();
            if (Instantiate(SaveManagement.instance.gameObject) is not GameObject saveManagementObject)
                throw new Exception($"Failed to get {nameof(SaveManagement)} game object for cloning.");

            BonesScroller = Instantiate(SaveManagement.instance.savesScroller);
            if (BonesScroller != null)
            {
                SetParentTransform(BonesScroller);

                Background = BonesScroller.gameObject.AddComponent<Image>();
                Background.color = SaveManagement.instance?.background?.color ?? The.Color.Black;
                Background.material = SaveManagement.instance?.background?.material;
            }
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BonesScroller)} must not be null"));

            BackButton = Instantiate(SaveManagement.instance.backButton);
            if (BackButton != null)
                SetParentTransform(BackButton);
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BackButton)} must not be null"));

            /*if (BonesScroller.GetComponentsInChildren<FrameworkScroller>() is FrameworkScroller[] frameworkScrollers
                && frameworkScrollers.Length > 1)
            {
                foreach (var childRectTransfrom in frameworkScrollers[0].GetComponentsInChildren<RectTransform>())
                {
                    if (childRectTransfrom.gameObject.name == "VLayout")
                    {
                        BonesScrollerVertScroll = childRectTransfrom;
                        break;
                    }
                }
                MenuBarParentTransfrom = frameworkScrollers[1].transform.parent;
                if (frameworkScrollers[1].transform is RectTransform menuBarRectTransfrom)
                {
                    Utils.Log($"{nameof(MenuBarParentTransfrom)}.{nameof(menuBarRectTransfrom.rect)}.{nameof(menuBarRectTransfrom.rect.position)}: {menuBarRectTransfrom.rect.position}");
                    MenuBarAnchoredPos = menuBarRectTransfrom.anchoredPosition;
                    MenuBarAnchorMin = menuBarRectTransfrom.anchorMin;
                    MenuBarAnchorMax = menuBarRectTransfrom.anchorMax;
                }
                frameworkScrollers[1].gameObject.SetActive(value: false);
                frameworkScrollers[1].GetNavigationContext().disabled = true;
                frameworkScrollers[1].choices.Clear();
                frameworkScrollers[1].selectionClones.Clear();
                frameworkScrollers[1].DestroyImmediate();
            }*/

            MenuBarParentTransfrom = BonesScroller.transform;

            
            if (BonesScroller.GetComponentsInChildren<FrameworkScroller>() is FrameworkScroller[] frameworkScrollers
                && frameworkScrollers.Length > 1)
                LegendBar = frameworkScrollers[1];

            /*
            if (LegendBar.GetComponent<RectTransform>() is RectTransform hotkeyBarRectTransform)
                hotkeyBarRectTransform.Translate(0, hotkeyBarRectTransform.rect.y * 3.5f, 0);
            */

            if (BonesScroller.GetComponentsInChildren<UITextSkin>() is UITextSkin[] uITextSkins)
                foreach (var uITextSkin in uITextSkins)
                    if (uITextSkin.name == "header")
                        uITextSkin.SetText("{{W|MANAGE BONES}}");
            
            /**/
            AllBonesMenuBar = Instantiate(LegendBar);

            //AllBonesMenuBar = Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar);

            //SetParentTransform(AllBonesMenuBar, BonesScroller.transform);
            /*SetParentTransform(AllBonesMenuBar, LegendBar.transform.parent);
            AllBonesMenuBar.transform.SetAsLastSibling();

            LegendBar.transform.SetAsLastSibling();*/

            LegendBar.gameObject.name = nameof(LegendBar);
            AllBonesMenuBar.gameObject.name = nameof(AllBonesMenuBar);

            if (AllBonesMenuBar.GetComponent<RectTransform>() is RectTransform allBonesRectTransform
                && BonesScroller.transform.parent is RectTransform bonesScrollerParentRectTransform
                && LegendBar.GetComponent<RectTransform>() is RectTransform legendRectTransform)
            {
                //allBonesRectTransform.Translate(0, (bonesScrollerParentRectTransform.rect.y * GetConfigBonesMulti()) + (allBonesRectTransform.rect.y * GetConfigMenuMulti()), 0);
                //allBonesRectTransform.Translate(0, allBonesRectTransform.rect.y * GetConfigMenuYMulti(), 0);

                allBonesRectTransform.anchoredPosition = legendRectTransform.anchoredPosition;
                allBonesRectTransform.position = legendRectTransform.position;
                allBonesRectTransform.localPosition = legendRectTransform.localPosition;
                allBonesRectTransform.offsetMin = legendRectTransform.offsetMin;
                allBonesRectTransform.offsetMax = legendRectTransform.offsetMax;
                //allBonesRectTransform.SetLocalPositionAndRotation(new(0, 0, 0), allBonesRectTransform.rotation);
                //allBonesRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0f, 260f);

                float legendBarPreferredWidth = -2f;
                float legendBarMinHeight = -2f;
                foreach (var layoutElement in legendRectTransform.GetComponents<LayoutElement>())
                {
                    if (layoutElement.gameObject.name == nameof(LegendBar))
                    {
                        legendBarMinHeight = layoutElement.minHeight;
                        legendBarPreferredWidth = layoutElement.preferredWidth;
                        break;
                    }

                }

                if (legendBarPreferredWidth != -2f)
                {
                    foreach (var layoutElement in allBonesRectTransform.GetComponents<LayoutElement>())
                    {
                        if (layoutElement.gameObject.name == nameof(AllBonesMenuBar))
                        {
                            layoutElement.minHeight = legendBarMinHeight;
                            layoutElement.preferredWidth = legendBarPreferredWidth;
                            break;
                        }
                    }
                }
/*
                if (TryGetConfigParamTyped("MenuColor", s => s?.EqualsNoCase("Yes") is true, out bool menuColor))
                {
                    foreach (var childImage in allBonesRectTransform.GetComponentsInChildren<Image>())
                    {
                        if (childImage.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            if (menuColor)
                                childImage.color = The.Color.Green.WithAlpha(0.75f);
                            else
                                childImage.color = The.Color.Green.WithAlpha(0);
                        }
                    }
                }
                allBonesRectTransform.Translate(0, allBonesRectTransform.rect.y * GetConfigMenuYMulti(), 0);*/
                allBonesRectTransform.Translate(0, allBonesRectTransform.rect.y * 0.5f, 0);
            }

            if (LegendBar.GetComponent<RectTransform>() is RectTransform legendBarRectTransform)
            {
                /*if (TryGetConfigParamTyped("LegendColor", s => s?.EqualsNoCase("Yes") is true, out bool legendColor))
                {
                    foreach (var childImage in legendBarRectTransform.GetComponentsInChildren<Image>())
                    {
                        if (childImage.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            if (legendColor)
                                childImage.color = The.Color.Blue.WithAlpha(0.75f);
                            else
                                childImage.color = The.Color.Blue.WithAlpha(0);
                        }
                    }
                }
                legendBarRectTransform.Translate(0, legendBarRectTransform.rect.y * GetConfigLegendYMulti(), 0);*/
                legendBarRectTransform.Translate(0, legendBarRectTransform.rect.y * 1.5f, 0);
            }

            SetParentTransform(AllBonesMenuBar, LegendBar.transform.parent);
            AllBonesMenuBar.transform.SetAsLastSibling();

            LegendBar.transform.SetAsLastSibling();

            LegendBar.gameObject.name = nameof(LegendBar);
            AllBonesMenuBar.gameObject.name = nameof(AllBonesMenuBar);
        }

        public void SetParentTransform(Component Component, Transform Transform = null)
        {
            Transform ??= transform;
            Component.gameObject.SetActive(value: false);
            Component.transform.SetParent(Transform, worldPositionStays: false);
        }

        public void SetupContext()
        {
            //Utils.Log($"{nameof(BonesManagement)}.{nameof(SetupContext)}");

            MainNavContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            MainNavContext.buttonHandlers.Set(InputButtonTypes.CancelButton, Event.Helpers.Handle(Exit));

            MidHorizNav.SetAxis(InputAxisTypes.NavigationXAxis);
            MidHorizNav.contexts.Clear();
            MidHorizNav.contexts.Add(AllBonesMenuBar.GetNavigationContext());
            MidHorizNav.contexts.Add(BackButton.navigationContext);
            MidHorizNav.contexts.Add(BonesScroller.GetNavigationContext());
            MidHorizNav.Setup();
            MidHorizNav.parentContext = MainNavContext;

            AllBonesMenuBar.GetNavigationContext().parentContext = MidHorizNav;
            LegendBar.GetNavigationContext().parentContext = MidHorizNav;
        }

        public override void Show()
        {
            // ##############################

            /*AllBonesMenuBar?.gameObject.SetActive(value: false);
            AllBonesMenuBar?.DestroyImmediate();
            AllBonesMenuBar = null;*/

            /*
            LegendBar?.gameObject.SetActive(value: false);
            LegendBar?.DestroyImmediate();
            LegendBar = null;
            */

            /*AllBonesMenuBar = Instantiate(LegendBar);
            //AllBonesMenuBar = Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar);
            //SetParentTransform(AllBonesMenuBar, BonesScroller.transform);
            //SetParentTransform(AllBonesMenuBar, MenuBarParentTransfrom);
            SetParentTransform(AllBonesMenuBar, LegendBar.transform.parent);
            AllBonesMenuBar.transform.SetAsLastSibling();*/

            /*
            LegendBar = Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar);
            //SetParentTransform(LegendBar, BonesScroller.transform);
            SetParentTransform(LegendBar, MenuBarParentTransfrom);
            LegendBar.transform.SetAsLastSibling();*/

            /*if (BonesScroller.transform.parent is RectTransform bonesScrollerParentRectTransform)
            {
                float bonesScrollerYWithMulti = bonesScrollerParentRectTransform.rect.y * GetConfigBonesYMulti();
                float bonesScrollerXWithMulti = bonesScrollerParentRectTransform.rect.x * GetConfigBonesXMulti();

                if (AllBonesMenuBar.GetComponent<RectTransform>() is RectTransform allBonesRectTransform)
                {
                    float childWidth = 0;
                    foreach (var childRect in allBonesRectTransform.GetComponentsInChildren<RectTransform>())
                    {
                        if (childRect.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            Utils.Log($"Adding {childRect.rect.width} to {nameof(AllBonesMenuBar)} {nameof(childWidth)}");
                            childWidth += childRect.rect.width;
                        }
                    }
                    allBonesRectTransform.Translate(
                        x: bonesScrollerXWithMulti + (childWidth * GetConfigMenuXMulti()),
                        y: bonesScrollerYWithMulti + (allBonesRectTransform.rect.y * GetConfigMenuYMulti()),
                        z: 0);
                }

                if (LegendBar.GetComponent<RectTransform>() is RectTransform legendRectTransform)
                {
                    float childWidth = 0;
                    foreach (var childRect in legendRectTransform.GetComponentsInChildren<RectTransform>())
                    {
                        if (childRect.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            Utils.Log($"Adding {childRect.rect.width} to {nameof(LegendBar)} {nameof(childWidth)}");
                            childWidth += childRect.rect.width;
                        }
                    }
                    legendRectTransform.Translate(
                        x: bonesScrollerXWithMulti + (childWidth * GetConfigLegendXMulti()),
                        y: bonesScrollerYWithMulti + (legendRectTransform.rect.y * GetConfigLegendYMulti()),
                        z: 0);
                }
            }*/

            // ##############################

            if (!Printed
                && Printed)
            {
                /*Printed = true;
                Utils.Log("#".ThisManyTimes(45));
                if (BonesScroller.GetComponentsInChildren<Component>() is Component[] components)
                    foreach (var component in components)
                        component.gameObject.PrintComponents($"{component.GetType()}|{component.gameObject.name}: ");
                // instance.gameObject.LogComponentTree();
                Utils.Log("#".ThisManyTimes(45));*/
            }

            //Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}");
            if (BonesManager.GetSaveBonesInfoAsync() is not Task<IEnumerable<SaveBonesInfo>> savedBonesInfoTask)
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get bonesInfo task");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            Task.WaitAll(savedBonesInfoTask);

            if (savedBonesInfoTask.Result is not IEnumerable<SaveBonesInfo> bonesInfos
                || bonesInfos.OrderBy(bones => bones, SaveBonesInfo.SaveBonesInfoComparerDescending).AsEnumerable() is not IEnumerable<SaveBonesInfo> orderedBonesInfos
                || SaveBonesInfosToUIElements(orderedBonesInfos) is not List<BonesInfoData> bareBones
                || bareBones.IsNullOrEmpty()
                || (Bones = new(bareBones)).IsNullOrEmpty())
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get choices");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            base.Show();

            MainNavContext.commandHandlers ??= new();
            MainNavContext.commandHandlers.Set(CMD_INSERT, Event.Helpers.Handle(HandleDeleteAll));

            BackButton.gameObject.SetActive(value: true);
            if (BackButton.navigationContext == null)
                BackButton.Awake();
            BackButton.navigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            BackButton.navigationContext.buttonHandlers.Set(InputButtonTypes.AcceptButton, Event.Helpers.Handle(Exit));

            BonesScroller.gameObject.SetActive(value: true);
            BonesScroller.scrollContext.wraps = true;
            BonesScroller.BeforeShow(Bones);

            for (int i = 0; i < (BonesScroller.selectionClones?.Count ?? 0); i++)
            {
                if (BonesScroller.selectionClones[i] is FrameworkUnityScrollChild selectionCloneI
                    && Bones[i] is BonesInfoData bonesDataI
                    && selectionCloneI.gameObject.GetComponent<SaveManagementRow>() is SaveManagementRow saveRow)
                {
                    saveRow.setBonesData(bonesDataI);
                    saveRow.deleteButton.context.buttonHandlers = BonesManagementRow.ButtonHandlers;
                    saveRow.context.context.commandHandlers = BonesManagementRow.CommandHandlers;
                }
                
            }

            if (Preselected != null
                || MainMenuBones.ReturnToBones != null)
            {
                Preselected ??= MainMenuBones.ReturnToBones;
                bool isBones(FrameworkDataElement element)
                    => element is BonesInfoData bonesData
                    && bonesData.BonesInfo.ID == Preselected.ID
                    ;
                BonesScroller.scrollContext.selectedPosition = Math.Clamp(Bones.FindIndex(isBones), 0, Bones.Count - 1);
            }
            else
            if (SelectFirst)
            {
                SelectFirst = false;
                BonesScroller.scrollContext.selectedPosition = 0;
            }
            else
            if (BonesScroller.scrollContext.selectedPosition >= Bones.Count)
                BonesScroller.scrollContext.selectedPosition = Math.Max(Bones.Count - 1, 0);

            BonesScroller.onSelected.RemoveAllListeners();
            BonesScroller.onSelected.AddListener(SelectedBones);

            BonesScroller.onHighlight.RemoveAllListeners();
            BonesScroller.onHighlight.AddListener(HighlightedBones);

            AllBonesMenuBar.onSelected ??= new();
            AllBonesMenuBar.onSelected.RemoveAllListeners();
            AllBonesMenuBar.onSelected.AddListener(SelectedAllBones);

            AllBonesMenuBar.onHighlight ??= new();
            AllBonesMenuBar.onHighlight.RemoveAllListeners();
            AllBonesMenuBar.onHighlight.AddListener(HighlightedAllBones);

            //MoveAllBonesMenuBar = true;
            //MoveLegendBar = true;

            SetupContext();
            EnableNavContext();
            UpdateLegendBar();
            UpdateMenuBars();

            /*
            Utils.Log("=".ThisManyTimes(45));
            instance.gameObject.LogComponentTree($"{Utils.CallChain(nameof(BonesManagement), nameof(instance), nameof(instance.gameObject))} {instance.gameObject.name}");
            Utils.Log("=".ThisManyTimes(45));
            */
        }

        public override void Hide()
        {
            base.Hide();
            DisableNavContext();
            gameObject.SetActive(value: false);
            Printed = false;
        }

        public void EnableNavContext()
        {
            MainNavContext.disabled = false;
            BonesScroller.GetNavigationContext().ActivateAndEnable();
        }

        public bool IsInsideActiveContext(NavigationContext NavigationContext)
        {
            if (NavigationController.instance is not NavigationController navController)
                return false;

            if (navController.activeContext is not NavigationContext activeContext)
                return false;

            return activeContext.IsInside(NavigationContext);
        }

        public void DisableNavContext(bool deactivate)
        {
            if (deactivate
                && IsInsideActiveContext(MainNavContext))
                NavigationController.instance.activeContext = null;
            MainNavContext.disabled = true;
        }

        public void DisableNavContext()
            => DisableNavContext(true);

        public void SelectedBones(FrameworkDataElement data)
        {
            if (data is BonesInfoData bonesData)
                CompletionSource?.TrySetResult(bonesData.BonesInfo);
        }

        public void HighlightedBones(FrameworkDataElement data)
        {
            if (data is BonesInfoData bonesData)
            {
                for (int i = 0; i < (BonesScroller.selectionClones?.Count ?? 0); i++)
                {
                    if (Bones[i] != bonesData)
                        continue;

                    if (BonesScroller.selectionClones[i] is FrameworkUnityScrollChild selectionCloneI
                        && selectionCloneI.gameObject.GetComponent<SaveManagementRow>() is SaveManagementRow saveRow)
                    {
                        // this *was* working, but seemingly doesn't, now...
                        saveRow.setBonesData(bonesData);
                        saveRow.Update();
                        BonesScroller.PostSetup.Invoke(selectionCloneI, selectionCloneI.scrollContext, bonesData, i);
                        break;
                    }
                }
            }
        }

        public void SelectedAllBones(FrameworkDataElement data)
        {
            if (data is MenuOption menuData)
            {
                if (menuData.InputCommand == CMD_INSERT)
                {
                    HandleDeleteAll();
                    //CompletionSource?.TrySetResult(null);
                }
            }
        }

        public void HighlightedAllBones(FrameworkDataElement data)
        {
            /*if (data is MenuOption menuData)
            {
                if (menuData.InputCommand == CMD_INSERT
                    && !menuData.Description.StartsWith("{{red|"))
                    menuData.Description = menuData.Description.Colored("red");
                else
                if (menuData.InputCommand != CMD_INSERT
                    && menuData.Description.StartsWith("{{red|"))
                    menuData.Description = menuData.Description.Strip();
            }*/
        }

        public async Task<SaveBonesInfo> BonesMenu()
        {
            gameObject.SetActive(value: true);

            SelectFirst = Preselected == null
                && MainMenuBones.ReturnToBones == null;

            while (true)
            {
                CompletionSource?.TrySetCanceled();
                CompletionSource = new TaskCompletionSource<SaveBonesInfo>();

                await The.UiContext;
                ControlManager.ResetInput();

                Show();

                var bonesInfo = await CompletionSource.Task;
                DisableNavContext();

                await The.UiContext;

                if (bonesInfo == null)
                    break;

                try
                {
                    if (bonesInfo.GetBonesJSON() is not SaveBonesJSON json)
                    {
                        await Popup.ShowAsync("That bones file appears to be missing an info file, or the info file has been lost during load." +
                            $"\n\nPlease contact {Utils.AuthorOnPlatforms} to see if the file can be recovered.");
                    }
                    else
                    if (json.SaveVersion < 400)
                    {
                        await Popup.ShowAsync("That bones file has an impossibly low save version and has either been modified haphardly or did something goofy while saving." +
                            $"\n\nPlease contact {Utils.AuthorOnPlatforms} to see if the file can be recovered.");
                    }
                    else
                    if (await bonesInfo.TryRestoreModsAsync())
                    {
                        Hide();
                        return bonesInfo;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error("Bones Menu", x);
                }
            }
            Hide();
            return null;
        }

        public void Exit()
        {
            Printed = false;
            MetricsManager.LogEditorInfo("Exiting bones screen");
            CompletionSource?.TrySetResult(null);
            ControlManager.ResetInput();
        }

        public async void HandleDeleteAll()
        {
            if (await BonesManager.CremateAllMoonKings(DisableNavContext, EnableNavContext) is IEnumerable<SaveBonesInfo> currentBonesInfos)
            {
                if (currentBonesInfos.IsNullOrEmpty()
                    || SaveBonesInfosToUIElements(currentBonesInfos) is not IEnumerable<BonesInfoData> bareBones
                    || !bareBones.IsNullOrEmpty())
                {
                    Exit();
                }
                else
                {
                    /*Bones ??= new();
                    Bones.Clear();
                    Bones.AddRange(bareBones);
                    Show();*/
                }
            }
        }

        public static Task HandleDeleteAllTask()
            => Task.Run(() =>
            {
                if (CheckInit())
                    instance?.HandleDeleteAll();
            });

        public async void HandleDelete()
        {
            if (!IsInsideActiveContext(BonesScroller.GetNavigationContext()))
                return;

            if (Bones[BonesScroller.selectedPosition] is not BonesInfoData bonesData
                || bonesData.BonesInfo is not SaveBonesInfo bonesInfo)
                return;

            List<QudMenuItem> buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                buttons = PopupMessage.AcceptCancelButton;

            string title = $"Cremate {bonesInfo.GetName()}".Colored("R");
            if ((await Popup.NewPopupMessageAsync(
                    message: $"Are you sure you want to cremate {bonesInfo.GetName()}'s bones?",
                    buttons: buttons,
                    title: title,
                    DefaultSelected: 1)
                ).command != "Cancel")
            {
                DisableNavContext();

                bonesInfo.Cremate();

                EnableNavContext();

                await Popup.NewPopupMessageAsync("Bones Cremated!", PopupMessage.AcceptButton);

                if (await BonesManager.GetSaveBonesInfoAsync() is not IEnumerable<SaveBonesInfo> bonesInfos
                    || bonesInfos.OrderBy(bones => bones, SaveBonesInfo.SaveBonesInfoComparerDescending).AsEnumerable() is not IEnumerable<SaveBonesInfo> orderedBonesInfos
                    || SaveBonesInfosToUIElements(orderedBonesInfos) is not IEnumerable<BonesInfoData> bareBones
                    || bareBones.IsNullOrEmpty()
                    || (Bones = new(bareBones)).IsNullOrEmpty())
                    Exit();
                else
                    Show();
            }
        }

        public void UpdateLegendBar()
        {
            if (!LegendBar.gameObject.activeSelf)
                LegendBar.gameObject.SetActive(value: true);

            LegendBar.GetNavigationContext().disabled = true;
            LegendBar.BeforeShow(LegendBarOptions);

            if (MoveLegendBar
                && BonesScroller.transform.parent is RectTransform bonesScrollerParentRectTransform)
            {
                MoveLegendBar = false;
                /*float bonesScrollerYWithMulti = bonesScrollerParentRectTransform.rect.y * GetConfigBonesYMulti();
                float bonesScrollerXWithMulti = bonesScrollerParentRectTransform.rect.x * GetConfigBonesXMulti();*/

                if (LegendBar.GetComponent<RectTransform>() is RectTransform legendRectTransform)
                {
                    /*if (TryGetConfigParamTyped("LegendColor", s => s?.EqualsNoCase("Yes") is true, out bool legendColor))
                    {
                        foreach (var childImage in legendRectTransform.GetComponentsInChildren<Image>())
                        {
                            if (childImage.gameObject.name.StartsWith("KeyMenuOption"))
                            {
                                if (legendColor)
                                    childImage.color = The.Color.Blue.WithAlpha(0.75f);
                                else
                                    childImage.color = The.Color.Blue.WithAlpha(0);
                            }
                        }
                    }*/

                    float childWidth = 0;
                    foreach (var childRect in legendRectTransform.GetComponentsInChildren<RectTransform>())
                    {
                        if (childRect.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            Utils.Log($"Adding {childRect.rect.width} to {nameof(LegendBar)} {nameof(childWidth)}");
                            childWidth += childRect.rect.width;
                        }
                    }
                    /*legendRectTransform.Translate(
                        x: bonesScrollerXWithMulti + (childWidth * GetConfigLegendXMulti()),
                        y: bonesScrollerYWithMulti + (legendRectTransform.rect.y * GetConfigLegendYMulti()),
                        z: 0);*/
                    /*legendRectTransform.Translate(
                        x: legendRectTransform.rect.x * GetConfigLegendXMulti(),
                        y: bonesScrollerYWithMulti + (legendRectTransform.rect.y * GetConfigLegendYMulti()),
                        z: 0);*/
                    //legendRectTransform.anchoredPosition = new(legendRectTransform.anchoredPosition.x, BonesScrollerVertScroll.anchoredPosition.y);
                    //legendRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, BonesScrollerVertScroll.rect.width);
                    /*legendRectTransform.Translate(
                        x: BonesScrollerVertScroll.rect.width * GetConfigBonesXMulti(),
                        y: bonesScrollerYWithMulti + (legendRectTransform.rect.y * GetConfigLegendYMulti()),
                        z: 0);*/
                }
            }
        }

        public void UpdateMenuBars()
        {
            if (!AllBonesMenuBar.gameObject.activeSelf)
                AllBonesMenuBar.gameObject.SetActive(value: true);

            AllBonesMenuBar.GetNavigationContext().disabled = false;
            AllBonesMenuBar.BeforeShow(MenuBarOptions);

            /*
            for (int i = 0; i < AllBonesMenuBar.choices.Count; i++)
            {
                if (AllBonesMenuBar.choices[i] is FrameworkDataElement choice
                    && AllBonesMenuBar.selectionClones[i] is FrameworkUnityScrollChild selection)
                {
                    if (selection.FrameworkControl is KeyMenuOption menuOption)
                    {
                        if (menuOption.GetComponent<HasSelectionCaret>() is not HasSelectionCaret selectionCaret)
                        {
                            selectionCaret = menuOption.gameObject.AddComponent<HasSelectionCaret>();

                            selectionCaret.enabled = true;
                            selectionCaret.selectable = menuOption.GetComponentInParent<FrameworkContext>();

                            selectionCaret.image = SelectionCaret.image;
                            selectionCaret.useSelectColor = SelectionCaret.useSelectColor;
                            selectionCaret.unselectedColor = SelectionCaret.unselectedColor;
                            selectionCaret.selectedColor = SelectionCaret.selectedColor;
                        }
                    }
                }
            }*/

            if (MoveAllBonesMenuBar
                && BonesScroller.transform.parent is RectTransform bonesScrollerParentRectTransform)
            {
                MoveAllBonesMenuBar = false;
                /*float bonesScrollerYWithMulti = bonesScrollerParentRectTransform.rect.y * GetConfigBonesYMulti();
                float bonesScrollerXWithMulti = bonesScrollerParentRectTransform.rect.x * GetConfigBonesXMulti();*/

                if (AllBonesMenuBar.GetComponent<RectTransform>() is RectTransform allBonesRectTransform)
                {
                    /*if (TryGetConfigParamTyped("MenuColor", s => s?.EqualsNoCase("Yes") is true, out bool menuColor))
                    {
                        foreach (var childImage in allBonesRectTransform.GetComponentsInChildren<Image>())
                        {
                            if (childImage.gameObject.name.StartsWith("KeyMenuOption"))
                            {
                                if (menuColor)
                                    childImage.color = The.Color.Green.WithAlpha(0.75f);
                                else
                                    childImage.color = The.Color.Green.WithAlpha(0);
                            }
                        }
                    }*/

                    float childWidth = 0;
                    foreach (var childRect in allBonesRectTransform.GetComponentsInChildren<RectTransform>())
                    {
                        if (childRect.gameObject.name.StartsWith("KeyMenuOption"))
                        {
                            Utils.Log($"Adding {childRect.rect.width} to {nameof(AllBonesMenuBar)} {nameof(childWidth)}");
                            childWidth += childRect.rect.width;
                        }
                    }
                    /*allBonesRectTransform.Translate(
                        x: bonesScrollerXWithMulti + (childWidth * GetConfigMenuXMulti()),
                        y: bonesScrollerYWithMulti + (allBonesRectTransform.rect.y * GetConfigMenuYMulti()),
                        z: 0);*/
                    /*allBonesRectTransform.Translate(
                        x: allBonesRectTransform.rect.x * GetConfigMenuXMulti(),
                        y: bonesScrollerYWithMulti + (allBonesRectTransform.rect.y * GetConfigMenuYMulti()),
                        z: 0);*/
                    //allBonesRectTransform.anchoredPosition = new(allBonesRectTransform.anchoredPosition.x, BonesScrollerVertScroll.anchoredPosition.y);
                    //allBonesRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, BonesScrollerVertScroll.rect.width);
                    
                    
                    /*allBonesRectTransform.Translate(
                        x: BonesScrollerVertScroll.rect.width * GetConfigBonesXMulti(),
                        y: bonesScrollerYWithMulti + (allBonesRectTransform.rect.y * GetConfigMenuYMulti()),
                        z: 0);*/
                }
            }
        }

        public void Update()
        {
            if (MainNavContext.IsActive()
                && IsInsideActiveContext(BonesScroller.GetNavigationContext()) != WasInScroller)
                UpdateLegendBar();
        }

        public void ControllerChanged()
        {
            UpdateLegendBar();
            UpdateMenuBars();
        }

        public static IEnumerable<BonesInfoData> SaveBonesInfosToUIElements(IEnumerable<SaveBonesInfo> SaveGameInfoList)
            => !SaveGameInfoList.IsNullOrEmpty()
            ? SaveGameInfoList.Aggregate(
                seed: new List<BonesInfoData>(),
                func: delegate (List<BonesInfoData> accumulator, SaveBonesInfo next)
                {
                    accumulator.Add(
                        item: new BonesInfoData
                        { 
                            BonesInfo = next,
                        });
                    return accumulator;
                })
            : new()
            ;

/*
        public static bool TryGetConfigParamTyped<T>(string Key, Func<string, T> Parse, out T Result)
        {
            Result = default;
            if (BonesManager.GetSaveBonesMenuBarConfigAsync() is Task<Dictionary<string, string>> paramPairsTask)
            {
                Utils.Log($"{Utils.CallChain(nameof(TryGetConfigParamTyped), nameof(paramPairsTask))}({nameof(Key)}: {Key})");
                paramPairsTask.Wait();
                if (paramPairsTask.Result is Dictionary<string, string> paramPairs
                    && paramPairs.TryGetValue(Key, out string valueRaw))
                {
                    try
                    {
                        Result = Parse(valueRaw);
                        return true;
                    }
                    catch (Exception x)
                    {
                        string parseNull = Parse != null
                            ? " not"
                            : null
                            ;
                        Utils.Error($"Failed to parse {nameof(valueRaw)} of type {typeof(T).Name} using passed {nameof(Parse)} (which was{parseNull} null)", x);
                        Result = default;
                        return false;
                    }
                }
            }
            return false;
        }

        public float GetConfigBonesYMulti()
        {
            if (TryGetConfigParamTyped("BonesYMulti", float.Parse, out float result))
                return result;

            return 2f;
        }

        public float GetConfigBonesXMulti()
        {
            if (TryGetConfigParamTyped("BonesXMulti", float.Parse, out float result))
                return result;

            return 0f;
        }

        public float GetConfigMenuYMulti()
        {
            if (TryGetConfigParamTyped("MenuYMulti", float.Parse, out float result))
                return result;

            return -3.5f;
        }

        public float GetConfigMenuXMulti()
        {
            if (TryGetConfigParamTyped("MenuXMulti", float.Parse, out float result))
                return result;

            return 0f;
        }

        public float GetConfigLegendYMulti()
        {
            if (TryGetConfigParamTyped("LegendYMulti", float.Parse, out float result))
                return result;

            return -2.5f;
        }

        public float GetConfigLegendXMulti()
        {
            if (TryGetConfigParamTyped("LegendXMulti", float.Parse, out float result))
                return result;

            return 0f;
        }*/
    }
}
