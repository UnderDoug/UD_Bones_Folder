using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Platform.IO;

using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;

using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod
{
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptIn)]
        [Serializable]
        public class HostCollection : HashSet<Host>, IDisposable
        {
            public class HostEqualityComparer : IEqualityComparer<Host>
            {
                public static HostEqualityComparer Default => new();

                public bool Equals(Host x, Host y)
                    => x == y
                    ;

                public int GetHashCode(Host obj)
                    => obj?.GetHashCode() ?? 0
                    ;
            }

            public static HostEqualityComparer DefaultEqualityComparer => HostEqualityComparer.Default;

            public static HostCollection DefaultHosts => new(SyncedFileLocation)
            {
                Host.DefaultHost,
            };

            public FileLocationData LocationData;

            public HostCollection()
                : base(DefaultEqualityComparer)
            { }

            public HostCollection(FileLocationData LocationData)
                : this()
            {
                this.LocationData = LocationData;
            }

            public HostCollection(HostCollection Source, FileLocationData LocationData)
                : base(Source, DefaultEqualityComparer)
            {
                this.LocationData = LocationData;
            }

            public static async Task<HostCollection> ReadFromFile(FileLocationData FilePath, string FileName)
                => await FilePath.ReadFromFileAsync<HostCollection>(FileName)
                ;

            public static async Task<HostCollection> Read()
            {
                if (TryFindBestOsseousAshPath(out FileLocationData filePath, out string fileName))
                    return await ReadFromFile(filePath, fileName);

                return null;
            }

            public static async Task<HostCollection> ReadOrNew()
            {
                if (TryFindBestOsseousAshPath(out var fileLocationData, out string fileName))
                {
                    if (await fileLocationData.ReadFromFileAsync<HostCollection>(fileName) is not HostCollection hosts)
                    {
                        hosts = new(DefaultHosts, fileLocationData);
                        hosts.WriteToFile(fileLocationData, fileName);
                    }
                    return hosts;
                }
                return null;
            }

            public void WriteToFile(FileLocationData FilePath, string FileName)
            {
                File.WriteAllText(FilePath.WithFileName(FileName), JsonConvert.SerializeObject(this, Formatting.Indented));
            }

            public void Write()
            {
                if (TryFindBestOsseousAshPath(out var fileLocationData, out string fileName))
                    WriteToFile(fileLocationData, fileName);
            }

            public void WriteHosts(HashSet<Host> Hosts)
            {
                Hosts ??= new();
                if (!this.SequenceEqual(Hosts))
                {
                    Clear();
                    this.Union(Hosts);
                    Write();
                }
            }

            public void WriteAddHost(Host Host)
            {
                if (Add(Host))
                    Write();
            }

            public void WriteAddHosts(params Host[] Hosts)
            {
                bool any = false;
                foreach (var host in Hosts ?? Enumerable.Empty<Host>())
                    if (Add(host))
                        any = true;

                if (any)
                    Write();
            }

            public void WriteRemoveHost(Host Host)
            {
                if (Remove(Host))
                    Write();
            }

            public void WriteRemoveHosts(params Host[] Hosts)
            {
                bool any = false;
                foreach (var host in Hosts ?? Enumerable.Empty<Host>())
                    if (Remove(host))
                        any = true;

                if (any)
                    Write();
            }

            #region Option Handling

            private static void RemoveHostOption(
                int Index,
                ref Rack<string> Options,
                ref Rack<IRenderable> Renders,
                ref Rack<char> Hotkeys
                )
            {
                Options.RemoveAt(Index);
                Renders.RemoveAt(Index);
                Hotkeys.RemoveAt(Index);
            }

            private static void AddHostOption(
                Host Option,
                ref Rack<string> Options,
                ref Rack<IRenderable> Renders,
                ref Rack<char> Hotkeys
                )
            {
                Options.Add(Option.DisplayName());
                Renders.Add(new Renderable(Tile: "Mutations/gas_generation.bmp", ColorString: "&y", TileColor: "&y", DetailColor: 'K'));
                Hotkeys.Add(Hotkeys.GetNextHotKey());
            }

            public static async Task ManageHostsOptionButton()
            {
                using var hosts = ScopeDisposedList<Host>.GetFromPool();
                if (!Hosts.IsNullOrEmpty())
                    hosts.AddRange(Hosts);
                var options = new Rack<string>
                {
                    "new host",
                };
                int offset = options.Count;
                var renders = new Rack<IRenderable>
                {
                    new Renderable(Tile: "UI/sw_newchar.bmp", ColorString: "&W", TileColor: "&W", DetailColor: 'y'),
                };
                var hotkeys = new Rack<char>
                {
                    'n',
                };
                foreach (var host in hosts)
                    AddHostOption(host, ref options, ref renders, ref hotkeys);

                int choice;
                do
                {
                    choice = await Popup.PickOptionAsync(
                        Title: "{{yellow|Manage {{black|Osseous Ash}} Hosts}}",
                        Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                        Options: options,
                        Hotkeys: hotkeys,
                        Icons: renders,
                        IntroIcon: new Renderable(
                            Tile: "Mutations/gas_generation.bmp",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'K'),
                        DefaultSelected: 1,
                        AllowEscape: true);

                    if (choice == 0)
                    {
                        while (true)
                        {
                            string entered = await AskFullHostName(null);

                            string authToken = null;
                            if (entered != null)
                                authToken = await AskHostAuthToken(null);

                            var newHost = !entered.IsNullOrEmpty()
                                ? new Host(entered, authToken)
                                : null
                                ;

                            bool? confirmed = await ConfirmParsedHost(newHost);

                            if (!confirmed.HasValue)
                                break;

                            if (confirmed.GetValueOrDefault())
                            {
                                WriteAddHost(newHost);
                                break;
                            }
                        }
                    }
                    else
                    if (choice > 0)
                    {

                    }
                }
                while (choice >= 0);
                
/*
                if ((await Popup.NewPopupMessageAsync(
                        message: $"Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.",
                        buttons: PopupMessage.YesNoButton,
                        DefaultSelected: 1,
                        title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_DOWNLOADS}?",
                        afterRender: new Renderable(
                            Tile: "Abilities/tile_supressive_fire.png",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'R'))
                        ).command == "Yes")
                {
                    any = true;
                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshDownloads)}",
                        Value: Options.EnableOsseousAshDownloads = true);
                }

                if ((await Popup.NewPopupMessageAsync(
                    message: $"Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, " +
                        "and, if you choose one, will associate a handle with the uploaded bones.",
                    buttons: PopupMessage.YesNoButton,
                    DefaultSelected: 1,
                    title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_UPLOADS}?",
                    afterRender: new FlippableRender(
                        Source: new Renderable(
                            Tile: "Abilities/tile_supressive_fire.png",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'W'),
                        HFlip: false,
                        VFlip: true))
                    ).command == "Yes")
                {
                    any = true;

                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                        Value: Options.EnableOsseousAshUploads = true);
                    await Options.ManageOsseousAshHandle();
                }

                if (any)
                    return;

                await Popup.ShowAsync("You haven't been opted in.\n\n" +
                    "If you'd like to change your mind at any time, there are options available in the options menu.");*/
            }

            private static async Task<string> AskFullHostName(string PrependMessage)
            {
                var sB = Event.NewStringBuilder();
                sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                    .AppendLine().AppendLine();
                if (!PrependMessage.IsNullOrEmpty())
                    sB.Append(PrependMessage)
                        .AppendLine().AppendLine();

                sB.Append($"Enter the full host name of the host you'd like to add, including the protocol and, if applicable, the port number.")
                    .AppendLine().AppendLine();
                sB.Append("Examples:")
                    .AppendLine().Append("https://osseousash.cloud/")
                    .AppendLine().Append("http://localhost:8000/")
                    .AppendLine().AppendLine();
                sB.Append("You'll be asked to confirm the parsed result.");

                return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true);
            }

            private static async Task<string> AskHostAuthToken(string PrependMessage)
            {
                var sB = Event.NewStringBuilder();
                sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                    .AppendLine().AppendLine();
                if (!PrependMessage.IsNullOrEmpty())
                    sB.Append(PrependMessage)
                        .AppendLine().AppendLine();

                sB.Append($"If the new host requires authentication of some kind, enter the required key below.")
                    .AppendLine().AppendLine();
                sB.Append("You can change/update this later if necessary.");

                return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: null, ReturnNullForEscape: true);
            }

            private static async Task<bool?> ConfirmParsedHost(Host NewHost)
            {
                if (NewHost == null)
                {
                    await ShowCancelledAddHost();
                    return true;
                }

                var confirmResult = await Popup.ShowYesNoCancelAsync(
                        Message: Event.FinalizeString(
                            SB: Event.NewStringBuilder("The host you've entered was parsed in the following way:")
                                .AppendLine().AppendPair(nameof(NewHost.Name), NewHost.Name)
                                .AppendLine().AppendPair(nameof(NewHost.Port), (NewHost.Port)?.ToString() ?? "")
                                .AppendLine().AppendPair(nameof(NewHost.Encrypted), NewHost.Encrypted)
                                .AppendLine().AppendPair(nameof(NewHost.AuthToken), NewHost.AuthToken)
                                .AppendLine().AppendLine()
                                .AppendLine().Append(NewHost.GetHostNameWithProtocol())
                                .AppendLine().AppendLine()
                                .Append("Is this correct?"))
                        );

                switch (confirmResult)
                {
                    case DialogResult.Yes:
                        return true;
                    case DialogResult.No:
                        return false;
                    case DialogResult.Cancel:
                    default:
                        await ShowCancelledAddHost();
                        return null;
                }
            }

            private static async Task ShowCancelledAddHost()
            {
                await Popup.ShowAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("Addition of new host cancelled."))
                    );
            }

            #endregion

            public override string ToString()
                => this.Aggregate(LocationData.SanitiseForDisplay(), Utils.NewLineDelimitedAggregator);

            public void Dispose()
            {
                LocationData = null;
                Clear();
            }
        }
    }
}
