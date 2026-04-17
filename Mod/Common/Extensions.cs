using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Kobold;

using Platform.IO;

using UD_Bones_Folder.Mod.UI;

using XRL;
using XRL.Collections;
using XRL.Language;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

using static XRL.World.Cell;

using Range = XRL.Range;

namespace UD_Bones_Folder.Mod
{
    public static class Extensions
    {
        public static SaveBonesJSON CreateSaveBonesJSON(
            this XRLGame Game,
            IDeathEvent E,
            GameObject MoonKing,
            DirectoryInfo.DirectoryType DirectoryType
            )
        {
            var localTimeNow = DateTime.Now;
            long saveTimeValue = localTimeNow.ToUniversalTime().Ticks;

            bool visible = MoonKing.Render.Visible;
            MoonKing.Render.Visible = true;
            MoonKing.RestorePristineHealth();
            var render = new BonesRender(MoonKing.RenderForUI("SaveBonesInfo", true), HFlip: true);
            MoonKing.Render.Visible = visible;

            var colorChars = render.GetColorChars();
            var tileColor = colorChars.foreground;
            var detailColor = colorChars.detail;

            var timeSpan = TimeSpan.FromTicks(Game._walltime);

            var zone = MoonKing.CurrentZone;
            string zoneID = zone.ZoneID;
            var terrainObject = zone.GetTerrainObject();
            string zoneTerrainType = zone.Z > 10 ? "underground" : terrainObject.Blueprint;

            string location = null;

            if (zone.Z > 10)
                location += $"{zoneTerrainType}, ";

            string deathReason = E.Reason ?? "You died under mysterious circumstances!";
            if (deathReason.EndsWith(".")
                || deathReason.EndsWith("!"))
                deathReason = deathReason[..^1];

            if (deathReason.StartsWith("You"))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.Subjective= {deathReason}";
            }
            else
            if (deathReason.StartsWith("you"))
            {
                deathReason = deathReason[4..];
                if (deathReason.StartsWith("were"))
                    deathReason = $"=subject.verb:were:afterpronoun={deathReason[4..]}";
                deathReason = $"=subject.subjective= {deathReason}";
            }

            return new SaveBonesJSON
            {
                OsseousAshID = OsseousAsh.Config?.ID ?? Guid.Empty,
                OsseousAshHandle = OsseousAsh.Config?.Handle ?? OsseousAsh.DefaultOsseousAshHandle,
                SaveVersion = 400,
                GameVersion = Game.GetType().Assembly.GetName().Version.ToString(),
                ID = Game.GameID,
                Name = $"=LunarShader:{UD_Bones_LunarRegent.GetRegalTitle(MoonKing)}:*= {MoonKing.GetDisplayName(BaseOnly: true)}",
                Level = MoonKing.Statistics["Level"].Value,
                GenoSubType = $"{MoonKing.genotypeEntry.DisplayName} {MoonKing.subtypeEntry.DisplayName}",
                GameMode = Game.GetStringGameState("GameMode", "Classic"),
                CharIcon = render.GetTile(),
                FColor = tileColor,
                DColor = detailColor,

                Location = $"{location}{LoreGenerator.GenerateLandmarkDirectionsTo(zoneID)}",
                InGameTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}",
                Turn = Game.Turns,
                SaveTime = $"{localTimeNow.ToLongDateString()} at {localTimeNow.ToLongTimeString()}",
                ModsEnabled = ModManager.GetRunningMods().ToList(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                SaveTimeValue = saveTimeValue,

                ZoneID = zoneID,

                BonesSpec = new BonesSpec(MoonKing, zone),

                DirectoryType = DirectoryType,

                DeathReason = deathReason.StartReplace().AddObject(MoonKing).ToString(),
                GenotypeName = MoonKing.GetGenotype(),
                SubtypeName = MoonKing.GetSubtype(),
                Blueprint = MoonKing.Blueprint,
            };
        }

        public static void setBonesData(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            SaveRow.SetBonesIcon(BonesData);
            SaveRow.SetBonesText(BonesData);
            SaveRow.SetBonesModsDiffer(BonesData);
            SaveRow.SetBonesDeleteButton();

            BonesManagement.instance.SelectionChoiceSyncButtons ??= new();
            SaveRow.SetLocationBox(BonesData);

            SaveRow.Update();
        }

        public static void SetBonesIcon(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;
            var bonesJSON = bonesInfo?.GetBonesJSON();

            var bonesRender = bonesInfo.Render;

            var colorChars = bonesRender.GetColorChars();
            var tileColor = colorChars.foreground;
            var detailColor = colorChars.detail;

            SaveRow.imageTinyFrame ??= new();

            if (bonesJSON != null)
            {
                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(bonesRender.GetTile());
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = The.Color.Black;
                SaveRow.imageTinyFrame.unselectedDetailColor = The.Color.Black;

                SaveRow.imageTinyFrame.selectedForegroundColor = The.Color.Gray;
                if (ColorUtility.ColorMap.TryGetValue(tileColor, out var fColor))
                    SaveRow.imageTinyFrame.selectedForegroundColor = fColor;

                SaveRow.imageTinyFrame.selectedDetailColor = The.Color.DarkBlack;
                if (ColorUtility.ColorMap.TryGetValue(detailColor, out var dColor))
                    SaveRow.imageTinyFrame.selectedDetailColor = dColor;
            }
            else
            {
                SaveRow.imageTinyFrame.sprite = SpriteManager.GetUnitySprite(bonesRender.GetTile());
                SaveRow.imageTinyFrame.unselectedBorderColor = The.Color.Black;
                SaveRow.imageTinyFrame.selectedBorderColor = The.Color.Yellow;
                SaveRow.imageTinyFrame.unselectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.unselectedDetailColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedForegroundColor = UnityEngine.Color.clear;
                SaveRow.imageTinyFrame.selectedDetailColor = UnityEngine.Color.clear;
            }

            if (SaveRow.imageTinyFrame.ThreeColor)
                SaveRow.imageTinyFrame.ThreeColor.SetHFlip(Value: false);
            
            SaveRow.imageTinyFrame.Sync(force: true);
        }

        public static void SetBonesText(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;

            SaveRow.TextSkins[0].SetText($"{bonesInfo.GetName()}::{bonesInfo.Description}".Colored("W"));
            SaveRow.TextSkins[1].SetText($"{ColorUtility.CapitalizeExceptFormatting(bonesInfo.Info)}");
            SaveRow.TextSkins[2].SetText($"{bonesInfo.DeathReason} on {bonesInfo.SaveTime}");
            string bonesID = "{" + bonesInfo.ID + "} ";
            SaveRow.TextSkins[3].SetText($"{bonesInfo.Size} {bonesID}".Colored("K"));
        }

        public static void SetLocationBox(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;
            if (!BonesManagement.instance.SelectionChoiceSyncButtons.TryGetValue(SaveRow, out var locationBox))
            {
                locationBox = UnityEngine.GameObject.Instantiate(SaveRow.modsDiffer);
                BonesManagement.instance.SetParentTransform(locationBox.transform, SaveRow.modsDiffer.transform.parent);
                locationBox.SetActive(value: true);
                locationBox.transform.SetAsFirstSibling();
                BonesManagement.instance.SelectionChoiceSyncButtons[SaveRow] = locationBox;
            }
            if (locationBox.GetComponentsInChildren<UITextSkin>() is UITextSkin[] locationBoxTextSkins)
            {
                foreach (var locationBoxTextSkin in locationBoxTextSkins)
                {
                    if (locationBoxTextSkin.name == "tct"
                        || locationBoxTextSkin.gameObject.name == "tct")
                    {
                        locationBoxTextSkin.SetText(bonesInfo.DirectoryInfo.Type switch
                        {
                            DirectoryInfo.DirectoryType.Synced => "synced".Colored("G"),
                            DirectoryInfo.DirectoryType.Local => "local".Colored("W"),
                            DirectoryInfo.DirectoryType.Mod => "modded".Colored("C"),
                            DirectoryInfo.DirectoryType.Online => "online".Colored("Y"),
                            _ => "!?".Colored("R"),
                        });
                        break;
                    }
                }
            }
            BonesManagement.instance.SelectionChoiceSyncButtons[SaveRow] = locationBox;
        }

        public static void SetBonesModsDiffer(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;

            SaveRow.modsDiffer.SetActive(value: true);
            if (SaveRow.modsDiffer.GetComponentsInChildren<UITextSkin>() is UITextSkin[] modsDifferTextSkins)
            {
                foreach (var modsDifferTextSkin in modsDifferTextSkins)
                {
                    if (modsDifferTextSkin.name == "tct"
                        || modsDifferTextSkin.gameObject.name == "tct")
                    {
                        modsDifferTextSkin.SetText(bonesInfo.ModsDiffer.ToString());
                        break;
                    }
                }
            }
        }

        public static void SetBonesDeleteButton(this SaveManagementRow SaveRow)
        {
            SaveRow.deleteButton ??= new();
            SaveRow.deleteButton.RequireContext<NavigationContext>().parentContext = SaveRow.context.context;
            if (SaveRow.deleteButton.GetComponentsInChildren<UITextSkin>() is UITextSkin[] deleteButtonTextSkins)
            {
                foreach (var deleteButtonTextSkin in deleteButtonTextSkins)
                {
                    if (deleteButtonTextSkin.name == "tct"
                        || deleteButtonTextSkin.gameObject.name == "tct")
                    {
                        deleteButtonTextSkin.SetText("{{y|cremate}}");
                        break;
                    }
                }
            }
        }

        public static string Colored(this string Text, string Color)
            => Color != null
            ? Text?.WithColor(Color)
            : Text
            ;

        public static string ToLiteral(this string String, bool Quotes = false)
        {
            if (String.IsNullOrEmpty())
                return null;

            string output = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(String, false);

            if (Quotes)
                output = $"\"{output}\"";

            return output;
        }

        public static string ValueUnits(this TimeSpan Duration, int Digits = 2)
        {
            string durationUnit = "week";
            double durationValue = Duration.TotalDays / 7;
            if (Duration.TotalDays < 7)
            {
                durationUnit = "day";
                durationValue = Duration.TotalDays;
            }
            if (Duration.TotalDays < 1)
            {
                durationUnit = "hour";
                durationValue = Duration.TotalHours;
            }
            if (Duration.TotalHours < 1)
            {
                durationUnit = "minute";
                durationValue = Duration.TotalMinutes;
            }
            if (Duration.TotalMinutes < 1)
            {
                durationUnit = "second";
                durationValue = Duration.TotalSeconds;
            }
            if (Duration.TotalSeconds < 1)
            {
                durationUnit = "millisecond";
                durationValue = Duration.TotalMilliseconds;
            }
            if (Duration.TotalMilliseconds < 1)
            {
                durationUnit = "microsecond";
                durationValue = Duration.TotalMilliseconds / 1000;
            }
            return Math.Round(durationValue, Math.Max(0, Digits)).Things(durationUnit);
        }

        public static TAccumulate Aggregate<TAccumulate>(
            this int Number,
            TAccumulate seed,
            Func<TAccumulate, int, TAccumulate> func
            )
        {
            for (int i = 0; i < Number; i++)
                seed = func(seed, i);

            return seed;
        }

        public static string ThisManyTimes(this string @string, int Times = 1)
            => Times.Aggregate("", (a, n) => a + @string)
            ;
        public static string ThisManyTimes(this char @char, int Times = 1)
            => @char.ToString().ThisManyTimes(Times)
            ;

        public static string CallChain(this string String, params string[] Calls)
            => Calls.Aggregate(String, (a, n) => a + "." + n)
            ;

        public static string CallChain(this Type Type, params string[] Calls)
            => Type.Name.CallChain(Calls)
            ;

        public static string Indent(this int Amount, int Factor = 4, int MaxIndent = 4)
            => Amount > 0
            ? " ".ThisManyTimes(Math.Min(Amount * Math.Max(1, Factor), MaxIndent * Factor))
            : null
            ;

        public static void PrintComponents(this UnityEngine.GameObject GameObject, string MessageBefore = null, int CurrentDepth = 0)
        {
            if (!MessageBefore.IsNullOrEmpty()
                && MessageBefore[^1] != ' ')
                MessageBefore += " ";
            if (GameObject == null)
            {
                Utils.Log($"{CurrentDepth.Indent(Factor: 4)}{MessageBefore}NULL_{nameof(UnityEngine.GameObject)}");
                return;
            }
            string name = GameObject?.name ?? "NO_NAME";
            Utils.Log($"{CurrentDepth.Indent(Factor: 4)}{MessageBefore}{name} (Components: {GameObject.GetComponentCount()})");
            for (int i = 0; i < GameObject.GetComponentCount(); i++)
            {
                try
                {
                    if (GameObject.GetComponentAtIndex(i) is UnityEngine.Component component)
                    {
                        string extras = component.GetType().Name switch
                        {
                            nameof(UITextSkin) => $"{nameof(UITextSkin.text)}: {(component as UITextSkin).text}",
                            nameof(UnityEngine.RectTransform) => $"{nameof(UnityEngine.RectTransform.rect)}: {(component as UnityEngine.RectTransform).rect}",
                            nameof(UnityEngine.UI.Image) => $"{nameof(UnityEngine.UI.Image.color)}: {(component as UnityEngine.UI.Image).color}",
                            _ => null,
                        };
                        Utils.Log($"{(CurrentDepth + 1).Indent(Factor: 4)}" +
                            $"{component.GetType()?.Name ?? "NO_COMPONENT_TYPE"}|" +
                            $"{component.name} {extras}");
                    }
                    else
                        Utils.Log($"{(CurrentDepth + 1).Indent(Factor: 4)}NO_COMPONENT");
                }
                catch (Exception x)
                {
                    Utils.Error($"{typeof(Extensions).CallChain(nameof(PrintComponents), name)} {nameof(UnityEngine.Component)} {i}", x);
                }
            }
        }

        public static IEnumerable<T> Loggregate<T>(
            this IEnumerable<T> Source,
            Func<T, string> Proc = null,
            string Empty = null,
            Func<string, string> PostProc = null
            )
            => Utils.Loggregrate(
                Source: Source,
                Proc: Proc,
                Empty: Empty,
                PostProc: PostProc)
            ;

        public static IEnumerable<KeyValuePair<GameObject, T>> GetObjectsAndPartsWithPartDescendedFrom<T>(this Zone Zone)
            where T : IPart
        {
            if (Zone == null)
                yield break;

            for (int i = 0; i < Zone.Height; i++)
            {
                for (int j = 0; j < Zone.Width; j++)
                {
                    if (Zone.Map[j][i] is not Cell cell
                        || cell.Objects is not ObjectRack objects)
                        continue;

                    for (int k = 0; k < objects.Count; k++)
                    {
                        if (objects[k] is not GameObject gameObject
                            || gameObject.GetPartDescendedFrom<T>() is not T tPart)
                            continue;

                        yield return new(gameObject, tPart);
                    }
                }
            }
        }

        public static T Log<T>(this T Message, T LogInsteadIfNull = default)
        {
            if (!(Message?.ToString()).IsNullOrEmpty()
                || !(LogInsteadIfNull?.ToString()).IsNullOrEmpty())
                Utils.Log(Message ?? LogInsteadIfNull);
            return Message;
        }

        public static HashSet<T> UnionWith<T>(this HashSet<T> Set, IEnumerable<IEnumerable<T>> Sets)
        {
            foreach (var set in Sets)
                Set.UnionWith(set);

            return Set;
        }

        public static HashSet<T> GetUnionOfSets<T>(this IEnumerable<IEnumerable<T>> Sets)
            => new HashSet<T>().UnionWith(Sets)
            ;

        public static HashSet<T> IntersectWithUnless<T>(
            this HashSet<T> Set,
            IEnumerable<T> Other,
            Predicate<IEnumerable<T>> Case
            )
        {
            if (Case?.Invoke(Other) is not true)
                Set.IntersectWith(Other);
            return Set;
        }

        public static HashSet<T> IntersectWithUnlessEmptyOrNull<T>(
            this HashSet<T> Set,
            IEnumerable<T> Other
            )
            => Set.IntersectWithUnless(
                Other: Other,
                Case: o => o.IsNullOrEmpty())
            ;

        public static bool IsTile(this string Tile)
            => Utils.TileExists(Tile)
            ;

        public static string ThisThese(this GameObject Object)
            => Object.IsPlural
            ? $"These"
            : $"This"
            ;

        public static string ThisTheseDescriptiveCategory(this GameObject Object)
        {
            string noun = Object.GetDescriptiveCategory();

            if (Object.IsPlural)
                noun = Grammar.Pluralize(noun);

            return $"{Object.ThisThese()} {noun}";
        }

        public static string IndefiniteArticleDescriptiveCategory(
            this GameObject Object,
            bool Capitalize = false,
            string Adjective = null
            )
        {
            string noun = Object.GetDescriptiveCategory();

            string indefiniteArticle;
            if (Object.IsPlural)
            {
                noun = noun.Pluralize();
                indefiniteArticle = "some";
            }
            else
            if (!Grammar.IndefiniteArticleShouldBeAn(Adjective ?? noun))
                indefiniteArticle = "a";
            else
                indefiniteArticle = "an";

            Adjective += " ";
            if (!Adjective.StartsWith(" "))
                Adjective = $" {Adjective}";

            string output = $"{indefiniteArticle}{Adjective}{noun}";
            if (Capitalize)
                output = output.Capitalize();

            return output;
        }

        public static string IndefiniteArticle(this string Word, bool Capitalize = false)
        {
            string indefiniteArticle;
            if (!Grammar.IndefiniteArticleShouldBeAn(Word))
                indefiniteArticle = "a";
            else
                indefiniteArticle = "an";

            if (Capitalize)
                indefiniteArticle = indefiniteArticle.Capitalize();

            return indefiniteArticle;
        }

        public static void ApplyRegistrar(this GameObject Object, bool Active = false, bool Recursive = true, int Depth = 0)
        {
            //Utils.Log($"{Depth.Indent()}{nameof(ApplyRegistrar)}: {Object?.DebugName ?? "NO_OBJECT??"}");

            if (Recursive)
            {
                if (Object.GetInventoryAndEquipmentAndDefaultEquipment() is List<GameObject> inventoryObjects)
                    for (int i = 0; i < inventoryObjects.Count; i++)
                        inventoryObjects[i].ApplyRegistrar(Active, Recursive, Depth + 1);

                if (Object.GetContents() is List<GameObject> contentsObject)
                    for (int i = 0; i < contentsObject.Count; i++)
                        contentsObject[i].ApplyRegistrar(Active, Recursive, Depth + 1);
            }

            if (Object?.PartsList is PartRack partsList)
            {
                for (int i = 0; i < partsList.Count; i++)
                    if (partsList[i] is IPart part)
                        part.ApplyRegistrar(Object, Active);
            }

            if (Object?._Effects is EffectRack _effects)
            {
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] is Effect effect)
                        effect.ApplyRegistrar(Object, Active);
            }

            if (Recursive)
            {
            }
        }

        public static bool IsMoonKing(this GameObject Object, string FromBonesID = null)
            => Object.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
                && (FromBonesID.IsNullOrEmpty()
                    || lunarRegent.BonesID.EqualsNoCase(FromBonesID))
            ;

        public static void FeverWarp(this GameObject BonesObject, string BonesID = null, bool Recursive = true, int Depth = 0)
        {
            //Utils.Log($"{Depth.Indent()}{nameof(FeverWarp)}: {BonesObject?.DebugName ?? "NO_OBJECT??"}");

            if (Recursive)
            {
                if (BonesObject.GetInventoryAndEquipmentAndDefaultEquipment() is List<GameObject> inventoryObjects)
                    for (int i = 0; i < inventoryObjects.Count; i++)
                        inventoryObjects[i].FeverWarp(BonesID, Depth: Depth + 1);

                if (BonesObject.GetContents() is List<GameObject> contentsObject)
                    for (int i = 0; i < contentsObject.Count; i++)
                        contentsObject[i].FeverWarp(BonesID, Depth: Depth + 1);
            }

            if (BonesID != null
                && !BonesObject.IsMoonKing(BonesID))
            {
                if (BonesObject.NeedsFeverWarped(out bool tileOnly))
                {
                    if (!BonesObject.HasPart<UD_Bones_FeverWarped>())
                        BonesObject.AddPart(
                            P: new UD_Bones_FeverWarped(TileOnly: tileOnly)
                            {
                                Persists = true,
                            }.OverrideBonesID<UD_Bones_FeverWarped>(BonesID));
                }
                else
                {
                    try
                    {
                        _ = GameObjectFactory.Factory.Blueprints[BonesObject.Blueprint];
                    }
                    catch
                    {
                        if (!BonesObject.HasPart<UD_Bones_FeverWarped>())
                            BonesObject.AddPart(
                                P: new UD_Bones_FeverWarped(TileOnly: false)
                                {
                                    Persists = true,
                                }.OverrideBonesID<UD_Bones_FeverWarped>(BonesID));
                    }
                }
            }
        }

        public static bool NeedsFeverWarped(this GameObject BonesObject, out bool TileOnly)
        {
            TileOnly = false;
            if (BonesObject.TryGetPart(out UD_Bones_FeverWarped feverWarped))
            {
                TileOnly = feverWarped.IsTileOnly();
                return false;
            }

            if (BonesObject.GetStringProperty(nameof(UD_Bones_FeverWarped), $"{false}").EqualsNoCase($"{true}"))
            {
                TileOnly = BonesObject.GetStringProperty($"{nameof(UD_Bones_FeverWarped)}::TileOnly", $"{false}").EqualsNoCase($"{true}");
                return true;
            }

            bool blueprintExists = GameObjectFactory.Factory.HasBlueprint(BonesObject.Blueprint);
            if (BonesObject.GetTile() is string bonesTile
                && !bonesTile.IsTile())
            {
                TileOnly = blueprintExists;
                return true;
            }

            if (!blueprintExists)
                return true;

            return false;
        }

        public static void SetEquipmentFrameColors(this GameObject GameObject, string TopLeft_Left_Right_BottomRight = null)
            => GameObject.SetStringProperty(Const.EQ_FRAME_COLORS, TopLeft_Left_Right_BottomRight, RemoveIfNull: true)
            ;

        public static string GetEquipmentFrameColors(this GameObject GameObject, string Default = null)
            => GameObject.GetStringProperty(Const.EQ_FRAME_COLORS, Default)
            ;

        public static bool IsEquipment(this GameObject GameObject)
        {
            if (GameObject.GetBlueprint()?.InheritsFrom("Item") is not true)
                return false;

            if (GameObject.HasPart<Armor>())
                return true;

            if (GameObject.HasPart<CyberneticsBaseItem>())
                return true;

            if (GameObject.HasPart<MissileWeapon>())
                return true;

            if (GameObject.TryGetPart(out MeleeWeapon mw)
                && !mw.IsImprovisedWeapon())
                return true;

            if (GameObject.HasPart<ThrownWeapon>()
                && !GameObject.HasTagOrProperty("HideThrownWeaponPerformance"))
                return true;

            if (GameObject.HasTagOrProperty("UsesSlots"))
                return true;

            return false;
        }

        public static int NegSafeModulo(this int Number, int Value)
            => (Value + (Number % Value)) % Value;

        public static IEnumerable<Range> GetRanges(this IEnumerable<string> Source, int Offset = 0, int? MaxLengthOverride = null)
        {
            if (Source.IsNullOrEmpty())
                yield break;

            int sourceLength = Source.Aggregate(0, (a, n) => a + n.Length);
            int maxLength = Math.Min(MaxLengthOverride ?? sourceLength, sourceLength);
            int currentPos = Offset;
            int finalPos = maxLength - 1;
            foreach (string element in Source)
            {
                if (currentPos >= finalPos)
                    break;

                int elementEndPos = Math.Min(currentPos + element.Length, finalPos);
                yield return new(currentPos, elementEndPos);

                currentPos = elementEndPos;
            }
        }

        public static bool IsMad(this GameObject GameObject)
            => GameObject
                ?.GetPropertyOrTag(Const.IS_MAD_PROP, $"{false}")
                ?.EqualsNoCase($"{true}") is true;

        public static void SetMad(this GameObject GameObject, bool? Value)
            => GameObject?.SetStringProperty(Const.IS_MAD_PROP, Value?.ToString());

        public static bool HasLunarRegentBonesID(this GameObject Object, string BonesID = null)
        {
            if (Object.GetPart<UD_Bones_LunarRegent>()?.BonesID is not string bonesID)
                return false;

            return BonesID == null
                || bonesID == BonesID;
        }

        public static T WaitResult<T>(this Task<T> Task)
        {
            if (Task == null)
                return default;

            Task.Wait();

            return Task.Result;
        }

        public static bool DirectoryExistsSafe(this string Path)
            => !Path.IsNullOrEmpty()
            && Directory.Exists(Path)
            ;

        public static bool ContainsAny(this string String, params string[] Strings)
            => Strings.IsNullOrEmpty()
            ? String.IsNullOrEmpty()
            : Strings.Any(s => String.Contains(s))
            ;

        public static bool ContainsAny(this string String, IEnumerable<string> Strings)
            => Strings.IsNullOrEmpty()
            ? String.IsNullOrEmpty()
            : Strings.Any(s => String.Contains(s))
            ;

        public static bool InheritsFrom(
            [NotNullWhen(true)] this Type Type,
            [NotNullWhen(true)] Type OtherType,
            bool IncludeSelf = true)
            => Type != null
            && OtherType != null
            && ((IncludeSelf
                    && Type == OtherType)
                || OtherType.IsSubclassOf(Type)
                || Type.IsAssignableFrom(OtherType)
                || (Type.YieldInheritedTypes().ToList() is List<Type> inheritedTypes
                    && inheritedTypes.Contains(OtherType)));

        public static bool TryFindLunarRegent(this Zone Z, string BonesID, out GameObject LunarRegent)
            => (LunarRegent = Z.GetObjects(go => go.GetPart<UD_Bones_LunarRegent>()?.BonesID == BonesID).FirstOrDefault()) != null
            ;

        public static bool TryEnsureObject(this GameObjectReference GameObjectReference, out GameObject Object)
        {
            if (!GameObject.Validate(ref GameObjectReference.Object))
            {
                if (GameObject.FindByID(GameObjectReference.ID) is GameObject foundObject)
                    GameObjectReference.Set(foundObject);
                else
                    GameObjectReference.Clear();
            }
            return (Object = GameObjectReference.Object) != null;
        }
    }
}
