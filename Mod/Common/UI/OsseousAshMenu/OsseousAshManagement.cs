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
    [UIView(OSSEOUS_ASH_MANAGEMENT_WINDOW_ID,
        NavCategory = "Menu",
        UICanvas = "HighScoresScreen",
        UICanvasHost = 1)]
    public class OsseousAshManagement : BaseRippedSingletonWindowBase<OsseousAshManagement, HighScoresScreen>, ControlManager.IControllerChangedEvent
    {
        public enum Modes
        {
            Configuration,
            Hosts,
        }

        public const string OSSEOUS_ASH_MANAGEMENT_WINDOW_ID = "UD_BonesFolder_OsseousAshManagement";

        public const string CMD_INSERT = "CmdInsert";

        public static bool WishContext;

        public static readonly MenuOption CONFIGURATION = new MenuOption
        {
            Id = "CONFIG",
            InputCommand = null,
            Description = "Configuration"
        };

        public static readonly MenuOption HOSTS = new MenuOption
        {
            Id = "HOSTS",
            InputCommand = null,
            Description = "Hosts"
        };

        public static List<MenuOption> leftSideMenuOptions = new List<MenuOption>
        {
            CONFIGURATION,
            HOSTS,
        };

        public static readonly MenuOption BACK_BUTTON = KeybindsScreen.BACK_BUTTON;

        public FrameworkUnityScrollChild ConfigurationRowPrefab;

        public FrameworkUnityScrollChild HostsRowPrefab;

        public override void Init()
        {
            base.Init();

            ConfigurationRowPrefab = Instantiate(OriginalInstance.LocalHighScoresRowPrefab);
            HostsRowPrefab = Instantiate(SingletonWindowBase<KeybindsScreen>.instance.keybindsScroller.selectionPrefab);

            ConfigurationRowPrefab.ClearFrameworkControlNaughty();
            HostsRowPrefab.ClearFrameworkControlNaughty();

        }

        public override string GetInstanceName()
            => "Osseous Ash Management"
            ;

        public Modes CurrentMode = Modes.Configuration;
    }
}
