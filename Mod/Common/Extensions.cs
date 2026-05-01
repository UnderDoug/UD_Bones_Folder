using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Kobold;

using Platform.IO;

using UD_Bones_Folder.Mod.UI;

using UnityEngine.UI;

using XRL;
using XRL.Collections;
using XRL.Language;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.OsseousAsh;
using static UD_Bones_Folder.Mod.OsseousAsh.Report;
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
            FileLocationData.LocationType DirectoryType
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
                OsseousAshID = Config?.ID ?? Guid.Empty,
                OsseousAshHandle = Config?.Handle ?? DefaultOsseousAshHandle,
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
                Stats = new(),

                FileLocationType = DirectoryType,

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
            SaveRow.SetBonesDeleteButton(BonesData);

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
                        string typeColor = bonesInfo.FileLocationData.GetFileLocationDataTypeColor();
                        string text = bonesInfo.FileLocationData.Type.ToString().ToLower().Colored(typeColor);
                        locationBoxTextSkin.SetText(text);
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
            if (SaveRow.modsDiffer.GetComponent<FrameworkContext>() is FrameworkContext modsDiffer)
                modsDiffer.context.buttonHandlers = BonesManagementRow.ModsButtonHandler;
        }

        public static void SetBonesDeleteButton(this SaveManagementRow SaveRow, BonesInfoData BonesData)
        {
            var bonesInfo = BonesData.BonesInfo;
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
            if (!bonesInfo.IsCrematable)
            {
                if (SaveRow.deleteButton.GetComponentsInChildren<UITextSkin>() is UITextSkin[] deleteButtonTextSkinsToHide)
                {
                    foreach (var deleteButtonTextSkin in deleteButtonTextSkinsToHide)
                    {
                        if (deleteButtonTextSkin.name == "tct"
                            || deleteButtonTextSkin.gameObject.name == "tct")
                        {
                            deleteButtonTextSkin.color = UnityEngine.Color.clear;
                            break;
                        }
                    }
                }

                if (SaveRow.deleteButton.GetComponentsInChildren<Image>() is Image[] deleteButtonImagesToHide)
                    foreach (var deleteButtonImage in deleteButtonImagesToHide)
                        deleteButtonImage.color = UnityEngine.Color.clear;

                SaveRow.deleteButton.context.disabled = true;
                SaveRow.deleteButton.enabled = false;
                SaveRow.deleteButton.gameObject.SetActive(value: false);
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

        public static string Indent(this int Amount, int Factor = 4, int MaxIndent = 4, bool NBSP = false)
        {
            if (!NBSP)
                return Amount > 0
                    ? " ".ThisManyTimes(Math.Min(Amount * Math.Max(1, Factor), MaxIndent * Factor))
                    : null
                    ;
            else
                return Amount > 0
                    ? $"=ud_nbsp:{Math.Min(Amount * Math.Max(1, Factor), MaxIndent * Factor)}=".StartReplace().ToString()
                    : null
                    ;
        }

        public static StringBuilder AppendIndent(this StringBuilder SB, int Amount = 0, int Factor = 4, int MaxIndent = 4, bool AsNBSP = false)
            => SB.Append(Amount.Indent(Factor, MaxIndent, AsNBSP))
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
            => Utils.Loggregate(
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
            if (GameObjectReference == null)
            {
                Object = null;
                return false;
            }
            if (!GameObject.Validate(ref GameObjectReference.Object))
            {
                if (GameObject.FindByID(GameObjectReference.ID) is GameObject foundObject)
                    GameObjectReference.Set(foundObject);
                else
                    GameObjectReference.Clear();
            }
            return (Object = GameObjectReference.Object) != null;
        }

        public static async Task<byte[]> ReadAllBytesAsync(this System.IO.Stream Stream)
        {
            if (Stream is System.IO.MemoryStream inMemoryStream)
                return inMemoryStream.ToArray();

            using (var outStream = new System.IO.MemoryStream())
            {
                await Stream.CopyToAsync(outStream);
                return outStream.ToArray();
            }
        }

        public static byte[] ReadAllBytes(this System.IO.Stream Stream)
            => Stream.ReadAllBytesAsync().WaitResult()
            ;

        public static async Task<byte[]> ReadAllBytesAsync(this System.IO.StreamReader StreamReader)
            => await StreamReader.BaseStream.ReadAllBytesAsync()
            ;

        public static byte[] ReadAllBytes(this System.IO.StreamReader StreamReader)
            => StreamReader.ReadAllBytesAsync().WaitResult()
            ;

        public static void SplitOut(this string String, string Delimiter, out string Key, out string Value)
        {
            Key = String;
            Value = null;

            if (String.IsNullOrEmpty())
                return;

            if (Delimiter.IsNullOrEmpty())
                return;

            if (!String.Contains(Delimiter))
                return;

            var pieces = String.Split(Delimiter);

            Key = pieces[0];

            var latterPieces = pieces[1..];
            Value = latterPieces.Aggregate("", (a, n) => Utils.DelimitedAggregator(a, n, Delimiter));
        }

        public static bool TrySplitOut(this string String, string Delimiter, out string Key, out string Value)
        {
            String.SplitOut(Delimiter, out Key, out Value);
            return !Key.IsNullOrEmpty()
                && !Value.IsNullOrEmpty()
                ;
        }

        public static bool IsEmptyOrDefault(this Guid Guid)
            => Guid == default
            || Guid == Guid.Empty
            ;

        public static StringBuilder AppendPair(this StringBuilder SB, object Key, object Value)
            => SB.Append(Key?.ToString()).Append(": ").Append(Value?.ToString())
            ;

        public static char GetNextHotKey(
            this IEnumerable<char> Source,
            IEnumerable<char> Excluding = null,
            char StartAt = 'a',
            char FinishAt = 'z'
            )
        {
            char lastHotkey = Source.LastOrDefault(c => Excluding?.Contains(c) is not true);

            if (lastHotkey == default)
                return StartAt;

            if (lastHotkey != ' ')
            {
                if (lastHotkey == '\0'
                    || lastHotkey == default)
                    lastHotkey = StartAt;

                while (Source.Contains(lastHotkey)
                    && lastHotkey <= FinishAt)
                    lastHotkey++;

                if (lastHotkey > FinishAt)
                    lastHotkey = ' ';
            }
            return lastHotkey;
        }

        public static string GetColoredString(this FileLocationData.LocationType Type)
            => Type.ToString().Colored(FileLocationData.GetFileLocationDataTypeColor(Type))
            ;

        public static int FirstIndexOrDefault<T>(
            this IEnumerable<T> Source,
            Predicate<T> OnBasis = null,
            int Default = 0
            )
        {
            int index = -1;
            foreach (T element in Source ?? Enumerable.Empty<T>())
            {
                index++;
                if (OnBasis?.Invoke(element) is not false)
                    return index;
            }
            return Default;
        }

        public static async Task<TResult> AwaitResultIfNotIsCompletedSuccessfully<TResult>(this Task<TResult> ResultTask)
            => (ResultTask?.IsCompletedSuccessfully) is true
            ? ResultTask.Result
            : await ResultTask
            ;

        public static string GetCheckbox(this bool Value)
            => $"[{(Value ? "■" : " ")}]";

        public static string GetCheckboxText(this bool Value, string Label)
            => $"{Value.GetCheckbox()} {Label}";

        public static bool IsTwixt(this int Value, int LowerInclusive, int UpperExclusive)
            => Value >= LowerInclusive
            && Value < UpperExclusive
            ;

        public static ulong ToUInt64<T>(this T Value)
            where T : Enum
            => Convert.GetTypeCode(Value) switch
            {
                TypeCode.SByte or
                TypeCode.Int16 or
                TypeCode.Int32 or
                TypeCode.Int64 => (ulong)Convert.ToInt64(Value, CultureInfo.InvariantCulture),

                TypeCode.Boolean or
                TypeCode.Char or
                TypeCode.Byte or
                TypeCode.UInt16 or
                TypeCode.UInt32 or
                TypeCode.UInt64 => Convert.ToUInt64(Value, CultureInfo.InvariantCulture),

                _ => throw new InvalidOperationException("Unknown enum type."),
            };

        public static bool IsTwixtInclusive<T>(this T Value, T Lower, T Upper)
            where T : Enum
            => Value.ToUInt64() >= Lower.ToUInt64()
            && Value.ToUInt64() <= Upper.ToUInt64()
            ;

        public static bool? ToNullableBool(this UIUtils.CascadableResult Value)
        {
            if (Value <= UIUtils.CascadableResult.Continue)
                return true;

            if (Value <= UIUtils.CascadableResult.Back)
                return false;

            return null;
        }

        public static bool ToBool(this UIUtils.CascadableResult Value)
            => Value <= UIUtils.CascadableResult.Continue
            ;

        public static bool IsContinue(this UIUtils.CascadableResult Value)
            => Value.IsTwixtInclusive(UIUtils.CascadableResult.Continue, UIUtils.CascadableResult.Continue)
            ;

        public static bool IsBack(this UIUtils.CascadableResult Value)
            => Value.IsTwixtInclusive(UIUtils.CascadableResult.Back, UIUtils.CascadableResult.BackSilent)
            ;

        public static bool IsCancel(this UIUtils.CascadableResult Value)
            => Value >= UIUtils.CascadableResult.Cancel
            ;

        public static bool IsSilent(this UIUtils.CascadableResult Value)
            => ((int)Value % 2) == ((int)UIUtils.CascadableResult.BackSilent % 2)
            ;

        public static UIUtils.CascadableResult ToCascadableResult(this bool? Value, bool Silent)
        {
            if (Value.GetValueOrDefault())
                return UIUtils.CascadableResult.Continue;

            var result = Value.HasValue
                ? UIUtils.CascadableResult.Back
                : UIUtils.CascadableResult.Cancel
                ;

            if (Silent)
                result++;

            return result;
        }

        public static StringBuilder AppendQuote(this StringBuilder SB, object Value)
            => SB.Append("\"").Append(Value).Append("\"")
            ;

        public static StringBuilder AppendBonesReportedObject(this StringBuilder SB, ObjectReportDetails ReportedObject, int Indent = 0)
            => SB.AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.Blueprint), ReportedObject.Blueprint)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.SerializedBaseID), ReportedObject.SerializedBaseID)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.DisplayName), "[redacted as a precaution]")
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.IsTheLunarRegent), ReportedObject.IsLunarRegent)
                .AppendLine().AppendIndent(Indent, AsNBSP: true).AppendPair(nameof(ReportedObject.IsLunarRegent), ReportedObject.IsLunarRegent)
            ;

        public static StringBuilder AppendBonesReport(this StringBuilder SB, Report Report, string Heading = null)
        {
            if (!Heading.IsNullOrEmpty())
                SB.Append(Heading)
                    .AppendLine();

            if (Report == null)
                SB.Append("Report is null...");
            else
            {
                SB.AppendPair(nameof(Report.BonesID), Report.BonesID)
                    .AppendLine().AppendPair(nameof(Report.Type), Report.Type);

                SB.AppendLine();
                if (Report.IsSpecificObject)
                {
                    SB.Append(nameof(Report.ObjectDetails)).Append(":")
                        .AppendLine().AppendBonesReportedObject(Report.ObjectDetails, Indent: 1);
                }
                else
                    SB.AppendPair("For Specific Object", Report.IsSpecificObject);

                SB.AppendLine().Append(nameof(Report.Description)).Append(":")
                    .AppendLine();

                if (Report.Description.IsNullOrEmpty())
                    SB.AppendIndent(1, AsNBSP: true).Append("none");
                else
                    SB.AppendQuote(Report.Description);
            }
            return SB;
        }

        public static IEnumerable<string> GetNotResistedDamageTypes(this GameObject Object)
        {
            foreach ((var statName, var stat) in Object?.Statistics ?? Enumerable.Empty<KeyValuePair<string, Statistic>>())
            {
                if (statName.Contains("Resistance")
                    && stat.Value < 25)
                {
                    yield return statName[..^("Resistance".Length)];
                }
            }

            if ((Object?.FireEvent("ApplyPoison") is true)
                && (Object?.FireEvent("CanApplyPoison") is true))
                yield return "Poison";

            yield return "Bludgeoning";

            yield return "Plasma";
            yield return "Light";
            yield return "Laser";
            yield return "Bleeding";
            yield return "Asphyxiation";
            yield return "Metabolic";
            yield return "Drain";
            yield return "Psionic";
            yield return "Mental";
            yield return "Thorns";
            yield return "Collision";
            yield return "Bite";
            yield return "Illusion";
            yield return "reflected";
        }

        public static HostCollection FirstWithHostMatching(this Rack<HostCollection> Hosts, Predicate<Host> Condition)
            => Hosts?.FirstOrDefault(hc => hc.Any(Condition.Invoke))
            ;

        public static Host FirstHostMatching(this Rack<HostCollection> Hosts, Predicate<Host> Condition)
            => Hosts?.FirstWithHostMatching(Condition.Invoke)?.FirstOrDefault(Condition.Invoke)
            ;

        public static string TimeAgo(this DateTime DateTime, string PostFix = null)
            => $"{(DateTime.Now - DateTime).ValueUnits()}{(!PostFix.IsNullOrEmpty() ? $" {PostFix}" : null)}"
            ;
    }
}
