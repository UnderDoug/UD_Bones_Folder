using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Cysharp.Text;

using UnityEngine;

using Qud.API;
using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

using GameObject = XRL.World.GameObject;
using ColorUtility = ConsoleLib.Console.ColorUtility;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using System.Reflection;

namespace Bones.Mod
{
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [Serializable]
    public class BonesManager : IScribedSystem
    {
        internal static string BonesPath => DataManager.SavePath("Bones");

        private static readonly string[] InfoFiles = new string[2]
        {
            $"{BonesSaver.BonesName}.json",
            $"{BonesSaver.BonesName}.sav.json"
        };

        [GameBasedStaticCache(CreateInstance = false)]
        public static BonesManager System;

        public List<string> RunningMods;

        public bool Initialized;

        [NonSerialized]
        public string BonesDirectory;

        [NonSerialized]
        public Task SaveTask;

        public BonesManager()
        {
        }

        private static BonesManager InitializeSystem() => new();

        [GameBasedCacheInit]
        public static void BonesManagerSystemInit()
        {
            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            if (System == null)
                System = The.Game?.RequireSystem(InitializeSystem);
            else
                The.Game?.AddSystem(System);

            bool success = false;
            try
            {
                if (System != null)
                    Loading.LoadTask($"Preparing Bones", System.PrepareBonesPile);

                success = true;
            }
            catch (Exception x)
            {
                MetricsManager.LogCallingModError($"{nameof(BonesManagerSystemInit)}: {x}");
                success = false;
            }
            finally
            {
                if (The.Game != null)
                    MetricsManager.LogModInfo(ModManager.GetMod(), $"{nameof(BonesManager)}.{nameof(BonesManagerSystemInit)} finished... {(success ? "success!" : "failure!")}");
            }
        }
        public void PrepareBonesPile()
        {
            RunningMods = ModManager.GetRunningMods().ToList();
        }

        public string GetBonesDirectory(string FileName = null)
        {
            if (BonesDirectory == null)
            {
                string path = Path.Combine(BonesPath, The.Game.GameID);
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception x)
                {
                    MetricsManager.LogCallingModError(x);
                    return null;
                }
                BonesDirectory = path;
            }
            if (!FileName.IsNullOrEmpty())
                return Path.Combine(BonesDirectory, FileName);

            return BonesDirectory;
        }

        public string GetSaveFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.sav.gz")
            ;

        public string GetCacheFileFullPath(string FileName)
            => GetBonesDirectory($"{FileName}.json")
            ;

        public Task CollectBones(string GameName, GameObject Player)
        {
            var task = SaveTask;

            if (task != null
                && !task.IsCompleted)
                return null;

            string message = "Hoarding Bones";

            Loading.Status status = Loading.StartTask(message);

            if (The.Game is XRLGame game
                && Player?.CurrentZone is Zone currentZone
                && SerializationWriter.Get() is SerializationWriter writer)
            {
                // Clear object ID's so they get a new one once re-serialzed (hopefully this works...)
                foreach (var zoneGO in currentZone.GetObjects())
                {
                    zoneGO._BaseID = 0;
                    zoneGO.RemoveStringProperty("id");
                }

                string bonesFilePath = GetSaveFileFullPath(GameName);
                string bonesCachePath = GetCacheFileFullPath(GameName);
                try
                {
                    if (game.WallTime != null)
                    {
                        game._walltime += game.WallTime.ElapsedTicks;
                        game.WallTime.Reset();
                        game.WallTime.Start();
                    }
                    else
                    {
                        game.WallTime = new Stopwatch();
                        game.WallTime.Start();
                    }

                    var saveBonesInfo = The.Game.SaveGameInfo()
                        // as SaveBonesJSON
                        ;
                    // saveBonesInfo.ZoneID = currentZone.ZoneID;

                    writer.Start(400);
                    writer.Write(123457);

                    writer.WriteOptimized(The.Game.GetType().Assembly.GetName().Version.ToString());

                    writer.WriteOptimized(currentZone.ZoneID);

                    Zone.Save(writer, currentZone);

                    writer.FinalizeWrite();
                    MetricsManager.LogInfo($"Done {message} in {game.WallTime.ElapsedMilliseconds}ms");
                    bool restoreBackup = false;
                    try
                    {
                        File.WriteAllText(bonesCachePath, JsonUtility.ToJson(saveBonesInfo, prettyPrint: true));
                        if (File.Exists(bonesFilePath))
                        {
                            File.Copy(bonesFilePath, bonesFilePath + ".bak", overwrite: true);
                            restoreBackup = true;
                        }
                        using (var bonesFile = File.Create(bonesFilePath))
                        {
                            using var gZipStream = new GZipStream(bonesFile, CompressionLevel.Fastest);
                            byte[] buffer = writer.Stream.GetBuffer();
                            gZipStream.Write(buffer, 0, (int)writer.Stream.Position);
                        }

                        game.CheckSave(bonesFilePath);
                    }
                    catch (Exception x)
                    {
                        game.SaveGameError(bonesFilePath, x, restoreBackup);
                    }
                    finally
                    {
                        SerializationWriter.Release(writer);
                    }
                    task = null;
                }
                catch (Exception x)
                {
                    SerializationWriter.Release(writer);
                    game.SaveGameError(bonesFilePath, x);
                    task = (SaveTask = Task.FromException(x));
                }
                finally
                {
                    MemoryHelper.GCCollectMax();
                    status.Dispose();
                }
            }
            return task;
        }

        public IEnumerable<SaveGameInfo> GetSavedBonesInfo()
        {
            using var saveGameInfo = ScopeDisposedList<SaveGameInfo>.GetFromPool();
            try
            {
                if (!Directory.Exists(BonesPath))
                    yield break;

                foreach (string bonesFolder in Directory.EnumerateDirectories(BonesPath))
                    if (GetDirectoryInfo(bonesFolder) is SaveGameInfo bonesInfo)
                        saveGameInfo.Add(bonesInfo);
            }
            catch (Exception x)
            {
                SeriousBonesError(x, BonesPath);
                saveGameInfo.Clear();
            }

            if (!saveGameInfo.IsNullOrEmpty())
                foreach (var bonesInfo in saveGameInfo)
                    yield return bonesInfo;
        }

        private static SaveGameInfo GetDirectoryInfo(string Directory)
        {
            try
            {
                if (Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("mods")
                    || Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("textures"))
                    return null;

                foreach (string infoFile in InfoFiles)
                {
                    if (Path.Combine(Directory, infoFile) is string path
                        && File.Exists(path))
                        return SavesAPI.ReadSaveJson(Directory, path).Result;
                }
                if (!global::System.IO.Directory.EnumerateFileSystemEntries(Directory).Any(f => !f.EndsWith("Cache.db")))
                {
                    try
                    {
                        global::System.IO.Directory.Delete(Directory, recursive: true);
                    }
                    catch (Exception message)
                    {
                        MetricsManager.LogWarning(message);
                    }
                }
                else
                    MetricsManager.LogWarning($"Weird bones directory with no .json file present: {DataManager.SanitizePathForDisplay(Directory)}");
                
            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                MetricsManager.LogWarning(x);
            }
            return null;
        }

        private static void SeriousBonesError(Exception Ex, string Path)
        {
            MetricsManager.LogError("Error checking for save files", Ex);
            using var sB = ZString.CreateStringBuilder();
            if (Ex is UnauthorizedAccessException
                || Ex is IOException)
            {
                sB.AppendLine("There was a permission error while trying to access your bones directory.");
                sB.AppendLine();
                sB.AppendLine(ColorUtility.EscapeFormatting(Ex.Message));
            }
            else
            {
                sB.AppendLine("There was an error while trying to access your bones directory.");
                sB.AppendLine();
                sB.Append("Directory: ");
                sB.AppendLine(ColorUtility.EscapeFormatting(Path));
            }
            sB.AppendLine();
            sB.AppendLine("Caves of Qud will run, but Bones won't be available. Please check your directory’s permissions.");

            Popup.WaitNewPopupMessage(
                message: sB.ToString(),
                buttons: new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = "Bummer!",
                        hotkey = "Accept,Cancel",
                        command = "Cancel"
                    }
                },
                title: "Error reading bones location.");
        }

        public BonesData ExhumeMoonKing(SaveGameInfo SavedBonesInfo)
        {
            string fullBonesPath = Path.Combine(SavedBonesInfo.Directory, $"{BonesSaver.BonesName}.sav.gz");
            int versionNumber = -1;
            string versionString = "{unknown}";
            BonesData bonesData = null;
            try
            {
                if (!File.Exists(fullBonesPath))
                {
                    fullBonesPath = Path.Combine(SavedBonesInfo.Directory, $"{BonesSaver.BonesName}.sav");
                    if (!File.Exists(fullBonesPath))
                    {
                        // Popup.Show("No saved game exists. (" + DataManager.SanitizePathForDisplay(Path) + ")");
                        return null;
                    }
                }

                var reader = SerializationReader.Get();

                var status = Loading.StartTask("Exhuming Moon King");

                try
                {
                    using (FileStream fileStream = File.OpenRead(fullBonesPath))
                    {
                        var stream = reader.Stream;
                        bool isGZip = false;
                        if (fileStream.Length >= 2)
                        {
                            Span<byte> buffer = stackalloc byte[2];
                            fileStream.Read(buffer);

                            if (buffer[0] == 31
                                && buffer[1] == 139)
                                isGZip = true;

                            fileStream.Position = 0L;
                        }
                        if (isGZip)
                        {
                            using var gZipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                            gZipStream.CopyTo(stream);
                        }
                        else
                            fileStream.CopyTo(stream);

                        stream.Position = 0L;
                    }
                    reader.Start();
                    if (reader.ReadInt32() != 123457)
                    {
                        versionString = "2.0.167.0 or prior";
                        throw new Exception("Bones file is the incorrect version.");
                    }
                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != 400)
                        {
                            string backupPath = fullBonesPath + $"_upgradebackup_{versionNumber}.gz";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(fullBonesPath, backupPath);
                                string cacheDBPath = Path.Combine(Path.GetDirectoryName(fullBonesPath), "Cache.db");
                                string cacheDBBackupPath = cacheDBPath + $"_upgradebackup_{versionNumber}.gz";
                                if (File.Exists(cacheDBPath)
                                    && !File.Exists(cacheDBBackupPath))
                                    File.Copy(cacheDBPath, cacheDBBackupPath);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogCallingModError($"bones upgrade backup: {x}");
                    }
                    if (reader.FileVersion < 395
                        || reader.FileVersion > 400)
                    {
                        throw new Exception("Bones file is the incorrect version (" + versionString + ").");
                    }

                    bonesData = new(SavedBonesInfo.ID, reader);

                    if (reader.Errors > 0)
                    {
                        Popup.DisplayLoadError(reader, "bones", reader.Errors);
                    }
                }
                catch (Exception x)
                {
                    ModManager.GetMod()?.Error(x);
                    bonesData = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                }

            }
            catch (Exception x)
            {
                string message = "That save file appears to be corrupt, you can try to restore the backup in your save directory (" + DataManager.SanitizePathForDisplay(Path.Combine(SavedBonesInfo.Directory, $"{BonesSaver.BonesName}.sav.gz.bak")) + ") by removing the 'bak' file extension.";
                if (ModManager.TryGetStackMod(x, out var Mod, out var Frame))
                {
                    MethodBase method = Frame.GetMethod();
                    string culpritMethod = method.DeclaringType?.FullName + "." + method.Name;
                    Mod.Error(culpritMethod + "::" + x);
                    message = "That save file is likely not loading because of a mod error from " + Mod.DisplayTitleStripped + " (" + culpritMethod + "), make sure the correct mods are enabled or contact the mod author.";
                }
                else
                {
                    if (versionNumber < 400)
                    {
                        message = "That save file looks like it's from an older save format revision (" + versionString + "). Sorry!\n\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.";
                    }
                    else if (versionNumber > 400)
                    {
                        message = "That save file looks like it's from a newer save format revision (" + versionString + ").\n\nYou can probably change to a newer branch in your game client and get it to load if you want to finish it off.";
                    }
                    MetricsManager.LogException("XRLGame.LoadGame::", x, "serialization_error");
                }

                // Popup.Show(message);

                throw;
            }

            return bonesData;
        }

        public SaveGameInfo GetSavedBonesByID(string BonesID)
        {
            foreach (var savedBones in GetSavedBonesInfo())
            {
                if (savedBones.ID == BonesID)
                    return savedBones;
            }
            return null;
        }

        public bool TryGetSavedBonesByID(string BonesID, out SaveGameInfo SavedBonesInfo)
            => (SavedBonesInfo = GetSavedBonesByID(BonesID)) != null
            ;

        public void CremateMoonKing(string BonesID)
        {
            if (TryGetSavedBonesByID(BonesID, out var savedBones))
                savedBones.Delete();
        }
    }
}
