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

namespace UD_Bones_Folder.Mod
{
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class Configuration
        {
            public Guid ID;
            public string Handle;

            [JsonIgnore]
            private bool _AskAtStartup;
            
            public bool AskAtStartup
            {
                get => _AskAtStartup;
                set
                {
                    WriteAskAtStartup(value);
                }
            }

            public int CustomPermyriadChance = Options.DefaultPermyriadChance;

            [JsonIgnore]
            private HashSet<string> LockedMembers = new();

            public static async Task<Configuration> ReadFromFile(string FilePath)
            {
                Configuration configJSON;
                try
                {
                    configJSON = await File.ReadAllJsonAsync<Configuration>(FilePath);
                }
                catch (Exception x)
                {
                    Utils.Error($"Loading OsseousAsh {FilePath}", x);
                    configJSON = null;
                }
                return configJSON;
            }

            public static async Task<Configuration> Read()
            {
                if (TryFindBestOsseousAshPath(out FileLocationData fileLocationData, out string fileName))
                    return await fileLocationData.ReadFromFileAsync<Configuration>(fileName);

                return null;
            }

            public static async Task<Configuration> ReadOrNew()
            {
                if (TryFindBestOsseousAshPath(out FileLocationData fileLocationData, out string fileName))
                {
                    if (await fileLocationData.ReadFromFileAsync<Configuration>(fileName) is not Configuration configJSON)
                    {
                        configJSON = new Configuration
                        {
                            AskAtStartup = true,
                            Handle = DefaultOsseousAshHandle,
                            ID = Guid.NewGuid(),
                        };
                        Options.EnableOsseousAshStartupPopup = true;
                        configJSON.WriteToFile(fileLocationData.WithFileName(fileName));
                        return configJSON;
                    }
                    Options.EnableOsseousAshStartupPopup = configJSON.AskAtStartup;
                    return configJSON;
                }
                return null;
            }

            public bool WriteUpdateField<T>(
                ref T Field,
                T Value,
                Func<T, T, bool> Predicate,
                Action<T, T> PostProc = null,
                Action<T, T> Catch = null,
                Action<T, T> AndFinally = null,
                string LockMember = null
                )
            {
                var fieldName = (new { Field })
                    .GetType()
                    .GetProperties()[0].Name;

                LockedMembers ??= new();

                if (!LockMember.IsNullOrEmpty()
                    && LockedMembers.Contains(LockMember))
                    return false;

                if (!LockMember.IsNullOrEmpty())
                    LockedMembers.Add(LockMember);

                T originalFieldValue = Field;
                try
                {
                    if (Predicate(Field, Value))
                    {
                        Field = Value;
                        Write();

                        PostProc?.Invoke(originalFieldValue, Value);
                        return true;
                    }
                    return false;
                }
                catch (Exception x)
                {
                    try
                    {
                        Catch?.Invoke(originalFieldValue, Value);
                    }
                    catch (Exception xX)
                    {
                        Utils.Error($"Failed to revert {fieldName} to {nameof(originalFieldValue)} and perform passed {nameof(Catch)} {nameof(Action)}", xX);
                    }
                    Utils.Error($"Failed to {nameof(WriteUpdateField)} {fieldName}", x);
                    return false;
                }
                finally
                {
                    if (!LockMember.IsNullOrEmpty())
                        LockedMembers.Remove(LockMember);

                    AndFinally?.Invoke(originalFieldValue, Value);
                }
            }

            public void WriteToFile(string FilePath)
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }

            public void Write()
            {
                if (TryFindBestOsseousAshPath(out FileLocationData fileLocationData, out string fileName))
                    WriteToFile(fileLocationData.WithFileName(fileName));
            }

            public void WriteAskAtStartup(bool Value, bool Propagate = true)
            {
                WriteUpdateField(
                    Field: ref _AskAtStartup,
                    Value: Value,
                    Predicate: (f, v) => f != v,
                    PostProc: (f, v)
                        => XRL.UI.Options.SetOption(
                            ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshStartupPopup)}",
                            Value: Options.EnableOsseousAshStartupPopup = v));
                
                /*LockedMembers ??= new();
                if (!LockedMembers.Contains(nameof(AskAtStartup))
                    && _AskAtStartup != Value)
                {
                    LockedMembers.Add(nameof(AskAtStartup));
                    try
                    {
                        _AskAtStartup = Value;

                        Write();

                        XRL.UI.Options.SetOption(
                            ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshStartupPopup)}",
                            Value: Options.EnableOsseousAshStartupPopup = Value);
                    }
                    finally
                    {
                        LockedMembers.Remove(nameof(AskAtStartup));
                    }
                }*/
            }

            public void WriteHandle(string Handle)
            {
                if (this.Handle != Handle)
                {
                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.OsseousAshHandle)}",
                        Value: this.Handle = Handle);
                    Write();
                }
            }

            public void WriteID(Guid ID)
            {
                if (this.ID != ID)
                {
                    this.ID = ID;
                    Write();
                }
            }

            public void WriteCustomPermyriadChance(int CustomPermyriadChance)
            {
                if (this.CustomPermyriadChance != CustomPermyriadChance)
                {
                    this.CustomPermyriadChance = CustomPermyriadChance;
                    Write();
                }
            }
        }
    }
}
