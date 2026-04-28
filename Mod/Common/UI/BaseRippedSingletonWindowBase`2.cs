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
    public class BaseRippedSingletonWindowBase<TWindow, TRippedWindow>
        : SingletonWindowBase<BaseRippedSingletonWindowBase<TWindow, TRippedWindow>>
        , ControlManager.IControllerChangedEvent
        where TWindow : class, new()
        where TRippedWindow : SingletonWindowBase<TRippedWindow>, new()
    {
        protected static string WindowTypeName => typeof(TWindow).Name;

        protected static string RippedWindowTypeName => typeof(TRippedWindow).Name;

        protected static TRippedWindow OriginalInstance => SingletonWindowBase<TRippedWindow>.instance;

        public static TWindow Instance
            => CheckInit()
            ? instance as TWindow
            : null
            ;

        public Image Background;

        public FrameworkScroller LegendBar;

        public FrameworkScroller PrimaryScroller;

        public EmbarkBuilderModuleBackButton BackButton;

        public NavigationContext MainNavContext = new();

        public ScrollContext<NavigationContext> MidHorizNav = new();

        public List<FrameworkDataElement> Elements = new();

        public bool WasInScroller;

        protected virtual bool SelectFirst { get; set; } = true;

        protected static void InitializeWithUIManager()
        {
            if (instance == null)
            {
                try
                {
                    var newRippedWindow = UIManager
                        .createWindow(
                            name: WindowTypeName,
                            scriptType: typeof(BaseRippedSingletonWindowBase<TWindow, TRippedWindow>)
                        ) as BaseRippedSingletonWindowBase<TWindow, TRippedWindow>;

                    newRippedWindow.Init();
                }
                catch (Exception x)
                {
                    Utils.Error(new NullReferenceException($"{nameof(UIManager)} didn't like creating a {WindowTypeName} window", x));
                }
            }
            if (instance == null)
                Utils.Error($"{WindowTypeName} window failed to initialize", new NullReferenceException($"{nameof(instance)} must not be null"));
        }

        public override void Init()
        {
            base.Init();

            name = GetInstanceName();

            if (OriginalInstance is null)
                throw new NullReferenceException($"{RippedWindowTypeName}.{nameof(SingletonWindowBase<TRippedWindow>.instance)} must not be null");

            if (Instantiate(OriginalInstance.gameObject) is not GameObject rippedSingletonWindowObject)
                throw new Exception($"Failed to get {WindowTypeName} game object for cloning.");

            PrimaryScroller = Instantiate(OriginalInstance.GetComponentsInChildren<FrameworkScroller>().FirstOrDefault(fws => fws.name.ContainsAny("menu", "legend", "hotkey", "side")));
            if (PrimaryScroller != null)
            {
                SetParentTransform(PrimaryScroller);

                Background = PrimaryScroller.gameObject.AddComponent<Image>();
                Background.color = SaveManagement.instance?.background?.color ?? The.Color.Black;
                Background.material = SaveManagement.instance?.background?.material;
            }
            else
                Utils.Error($"{nameof(BaseRippedSingletonWindowBase<TWindow, TRippedWindow>)}.{nameof(Init)}", new NullReferenceException($"{nameof(PrimaryScroller)} must not be null"));

            BackButton = Instantiate(SaveManagement.instance.backButton);
            if (BackButton != null)
                SetParentTransform(BackButton);
            else
                Utils.Error($"{nameof(BaseRippedSingletonWindowBase<TWindow, TRippedWindow>)}.{nameof(Init)}", new NullReferenceException($"{nameof(BackButton)} must not be null"));
        }

        public static bool CheckInit()
        {
            if (instance == null)
                InitializeWithUIManager();

            return instance != null;
        }

        public virtual string GetInstanceName()
            => WindowTypeName
            ;

        public virtual void SetParentTransform(Component Component, Transform Transform = null)
        {
            Transform ??= transform;
            Component.gameObject.SetActive(value: false);
            Component.transform.SetParent(Transform, worldPositionStays: false);
        }

        public virtual void SetupContext()
        {
            MainNavContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            MainNavContext.buttonHandlers.Set(InputButtonTypes.CancelButton, Event.Helpers.Handle(Exit));

            MidHorizNav.SetAxis(InputAxisTypes.NavigationXAxis);
            MidHorizNav.contexts.Clear();
            MidHorizNav.contexts.Add(BackButton.navigationContext);
            MidHorizNav.contexts.Add(PrimaryScroller.GetNavigationContext());
            MidHorizNav.Setup();
            MidHorizNav.parentContext = MainNavContext;

            LegendBar.GetNavigationContext().parentContext = MainNavContext;
        }

        public virtual void EnableNavContext()
        {
            MainNavContext.disabled = false;
            PrimaryScroller.GetNavigationContext().ActivateAndEnable();
            PrimaryScroller.BeforeShow(Elements);
        }

        public virtual bool IsInsideActiveContext(NavigationContext NavigationContext)
        {
            if (NavigationController.instance is not NavigationController navController)
                return false;

            if (navController.activeContext is not NavigationContext activeContext)
                return false;

            return activeContext.IsInside(NavigationContext);
        }

        public virtual void DisableNavContext(bool deactivate)
        {
            if (deactivate
                && IsInsideActiveContext(MainNavContext))
                NavigationController.instance.activeContext = null;

            MainNavContext.disabled = true;
        }

        public void DisableNavContext()
            => DisableNavContext(true);

        public override void Hide()
        {
            base.Hide();
            DisableNavContext();
            gameObject.SetActive(value: false);
        }

        public virtual void BeforeExit()
        {
        }

        public void Exit()
        {
            BeforeExit();
            MetricsManager.LogEditorInfo($"Exiting {WindowTypeName}");
            ControlManager.ResetInput();
        }

        public virtual IEnumerable<MenuOption> GetLegendBarOptions()
        {
            yield return new MenuOption
            {
                InputCommand = "NavigationXYAxis",
                Description = "navigate"
            };
            yield return new MenuOption
            {
                KeyDescription = ControlManager.getCommandInputDescription("Accept"),
                Description = "select"
            };
        }

        public virtual void UpdateLegendBar()
        {
            if (!LegendBar.gameObject.activeSelf)
                LegendBar.gameObject.SetActive(value: true);

            LegendBar.GetNavigationContext().disabled = true;
            LegendBar.BeforeShow(GetLegendBarOptions());
        }

        public virtual bool CheckBeforeShow()
            => true
            ;

        public sealed override void Show()
        {
            if (!CheckBeforeShow())
            {
                Exit();
                return;
            }
            base.Show();

            SetUpBackButton();
            SetUpPrimaryScroller();

            SetupContext();
            EnableNavContext();
            UpdateLegendBar();
        }

        public virtual void SetUpBackButton()
        {
            BackButton.gameObject.SetActive(value: true);
            if (BackButton.navigationContext == null)
                BackButton.Awake();
            BackButton.navigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            BackButton.navigationContext.buttonHandlers.Set(InputButtonTypes.AcceptButton, Event.Helpers.Handle(Exit));
        }

        public virtual void SetUpPrimaryScroller()
        {
            PrimaryScroller.gameObject.SetActive(value: true);
            PrimaryScroller.scrollContext.wraps = true;
        }

        public virtual void SetPrimarySelectedListners()
        {
            PrimaryScroller.onSelected.RemoveAllListeners();
            PrimaryScroller.onSelected.AddListener(SelectedElement);
        }

        public virtual void SetPrimaryHighlightedListners()
        {
            PrimaryScroller.onHighlight.RemoveAllListeners();
            PrimaryScroller.onHighlight.AddListener(HighlightedElement);
        }

        public virtual void SelectedElement(FrameworkDataElement data)
        {
        }

        public void HighlightedElement(FrameworkDataElement data)
        {
        }

        public void Update()
        {
            if (MainNavContext.IsActive()
                && IsInsideActiveContext(PrimaryScroller.GetNavigationContext()) != WasInScroller)
                OnUpdateActive();

            OnUpdate();
        }

        public virtual void OnUpdateActive()
        {
            UpdateLegendBar();
        }

        public virtual void OnUpdate()
        {
        }

        public void ControllerChanged()
        {
            OnControllerChanged();
        }

        public virtual void OnControllerChanged()
        {
            UpdateLegendBar();
        }
    }
}
