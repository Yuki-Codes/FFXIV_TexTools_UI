using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views;
using FFXIV_TexTools.Views.Models;
using Newtonsoft.Json;
using SharpDX;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Models.ModelTextures;

namespace FFXIV_TexTools
{
	internal static class AnamnesisFmvExport
	{
        private static DirectoryInfo GameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
        private static MainWindow MainWin = MainWindow.GetMainWindow();
        private static object LockObj = new object();

        private static string name;
        private static CharacterFile character;
        private static List<IItem> allItems;

        public static void Run()
		{
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Anamnesis Character|*.chara";
            DialogResult result = dlg.ShowDialog();
            if (result != DialogResult.OK)
                return;

			_ = RunInternal(dlg.FileName);
		}

		private static async Task RunInternal(string inputFilePath)
		{ 
			try
			{
                await MainWin.LockUi("Exporting Anamnesis Character", "....", LockObj);
                MainWin.LockProgress.Report("Loading chara file...");

                name = Path.GetFileNameWithoutExtension(inputFilePath);

                // read chara file
                string json = File.ReadAllText(inputFilePath);
				character = JsonConvert.DeserializeObject<CharacterFile>(json);
                XivRace race = character.GetXivRace();

                // load all items we can
                MainWin.LockProgress.Report("Loading items...");
                allItems = await XivCache.GetFullItemList();

                // Body
                await Export("Face", GetFaceModel(race, character.Head, character.Eyebrows, character.Eyes, character.Nose, character.Jaw, character.Mouth), race);
                await Export("EarsTail", GetEarsTailModel(race, character.TailEarsType), race);
                await Export("Hair", GetHairModel(race, character.Hair), race);
                await Export("Head", GetItemModel(character.HeadGear, "Head"), race);
                await Export("Body", GetItemModel(character.Body, "Body"), race);
                await Export("Hands", GetItemModel(character.Hands, "Hands"), race);
                await Export("Legs", GetItemModel(character.Legs, "Legs"), race);
                await Export("Feet", GetItemModel(character.Feet, "Feet"), race);
                await Export("Earring", GetItemModel(character.Ears, "Earring"), race);
                await Export("Neck", GetItemModel(character.Neck, "Neck"), race);
                await Export("Wrists", GetItemModel(character.Wrists, "Wrists"), race);
                await Export("L Ring", GetItemModel(character.LeftRing, "Rings"), race);
                await Export("R ring", GetItemModel(character.RightRing, "Rings"), race);
                await Export("Weapon Main", GetWeaponModel(character.MainHand, true), race);
                await Export("Weapon Off", GetWeaponModel(character.OffHand, false), race);
            }
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error running export");
			}
            finally
			{
                await MainWin.UnlockUi(LockObj);
            }
		}

        private static IItemModel GetHairModel(XivRace race, byte hair)
		{
            int raceCode = int.Parse(race.GetRaceCode());

            foreach (IItem item in allItems)
            {
                if (item is IItemModel itemModel)
                {
                    if (item.PrimaryCategory != "Character")
                        continue;

                    if (item.SecondaryCategory != "Hair")
                        continue;

                    if (itemModel.ModelInfo.PrimaryID != raceCode)
                        continue;

                    XivCharacter faceItem = (XivCharacter)itemModel.Clone();

                    faceItem.ModelInfo = new XivModelInfo();
                    faceItem.ModelInfo.SecondaryID = hair;
                    return faceItem;
                }
            }

            throw new Exception($"Failed to find hair model: {race}, {hair}");
        }

        private static IItemModel GetFaceModel(XivRace race, byte head, byte eyebrows, byte eyes, byte nose, byte jaw, byte mouth)
		{
            int raceCode = int.Parse(race.GetRaceCode());

            foreach (IItem item in allItems)
			{
                if (item is IItemModel itemModel)
                {
                    if (item.PrimaryCategory != "Character")
                        continue;

                    if (item.SecondaryCategory != "Face")
                        continue;

                    if (itemModel.ModelInfo.PrimaryID != raceCode)
                        continue;

                    XivCharacter faceItem = (XivCharacter)itemModel.Clone();

                    faceItem.ModelInfo = new XivModelInfo();
                    faceItem.ModelInfo.SecondaryID = head;
                    return faceItem;
                }
			}

            throw new Exception($"Failed to find face model: {race}, {head}");
		}

        private static IItemModel GetEarsTailModel(XivRace race, byte id)
        {
            // only au ra, miqote, and viera have ears or tail models.
            if (race != XivRace.AuRa_Female
                && race != XivRace.AuRa_Male
                && race != XivRace.Miqote_Female
                && race != XivRace.Miqote_Male
                && race != XivRace.Viera_Female
                && race != XivRace.Viera_Male)
                return null;

            int raceCode = int.Parse(race.GetRaceCode());

            if (id == 0)
                id = 1;

            foreach (IItem item in allItems)
            {
                if (item is IItemModel itemModel)
                {
                    if (item.PrimaryCategory != "Character")
                        continue;

                    if (item.SecondaryCategory != "Ear" && item.SecondaryCategory != "Tail")
                        continue;

                    if (itemModel.ModelInfo.PrimaryID != raceCode)
                        continue;

                    XivCharacter earTailItem = (XivCharacter)itemModel.Clone();

                    earTailItem.ModelInfo = new XivModelInfo();
                    earTailItem.ModelInfo.SecondaryID = id;
                    return earTailItem;
                }
            }

            throw new Exception($"Failed to find Ears/Tail model: {race}, {id}");
        }

        private static IItemModel GetItemModel(CharacterFile.ItemSave itemSave, string category)
		{
            if (itemSave.ModelBase == 0 && itemSave.ModelVariant == 0)
                return null;

            foreach(IItem item in allItems)
			{
                if (item.PrimaryCategory != "Gear")
                    continue;

                if (item is IItemModel itemModel)
				{
                    if (itemModel.ModelInfo.PrimaryID == itemSave.ModelBase &&
                        itemModel.ModelInfo.ImcSubsetID == itemSave.ModelVariant &&
                        itemModel.SecondaryCategory == category)
					{
                        return itemModel;
					}
                }
			}

            throw new Exception($"Could not find model for item save: {itemSave}");
		}

        private static IItemModel GetWeaponModel(CharacterFile.WeaponSave weaponSave, bool mainHand)
        {
            if (weaponSave.ModelSet == 0 && weaponSave.ModelBase == 0 && weaponSave.ModelVariant == 0)
                return null;

            foreach (IItem item in allItems)
            {
                if (item is XivGear itemModel)
                {
                    if (itemModel.ModelInfo.PrimaryID == weaponSave.ModelSet &&
                        itemModel.ModelInfo.SecondaryID == weaponSave.ModelBase &&
                        itemModel.ModelInfo.ImcSubsetID == weaponSave.ModelVariant)
                    {
                        return mainHand ? itemModel : itemModel.PairedItem;
                    }
                }
            }

            throw new Exception($"Could not find model for weapon save: {weaponSave}");
        }

        private static async Task Export(string part, IItemModel item, XivRace desiredRace)
		{
            if (item == null)
                return;

            MainWin.LockProgress.Report($"Exporting {part}: {item.Name}");

            Mdl _mdl = new Mdl(GameDirectory, item.DataFile);

            TTModel model = await _mdl.GetModel(item, desiredRace);
            XivRace modelRace = desiredRace;

            if (model == null)
            {
                List<XivRace> priority = desiredRace.GetModelPriorityList();
                foreach (XivRace newRace in priority)
                {
                    model = await _mdl.GetModel(item, newRace);
                    modelRace = newRace;

                    if (model != null)
                    {
                        break;
                    }
                }
            }

            if (model == null)
                throw new Exception($"Failed to get model for item: {item}");

            if (!Directory.Exists($"/{name}/{part}/"))
                Directory.CreateDirectory($"/{name}/{part}/");

            string path = $"/{name}/{part}/{item.Name}.fbx";

            if (desiredRace != modelRace)
                ApplyDeformers(model, modelRace, desiredRace);

            await _mdl.ExportModel(model, path);

            MainWin.LockProgress.Report($"Converting Normals {part}: {item.Name}");

            string[] normalMaps = Directory.GetFiles(Path.GetDirectoryName(path), "*_n.png");
            foreach (string normalMap in normalMaps)
            {
                string disMap = normalMap.Replace("_n.png", "_dis.png");

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "NormalToHeight.exe";
                processStartInfo.Arguments = $"{normalMap} {disMap} -normalise";
                processStartInfo.CreateNoWindow = true;
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Process proc = Process.Start(processStartInfo);

                while (!proc.HasExited)
                {
                    await Task.Delay(100);
                }
            }
        }

        /// <summary>
        /// Applies the deformer to a model
        /// </summary>
        /// <param name="model">The model being deformed</param>
        /// <param name="currentRace">The current model race</param>
        /// <param name="targetRace">The target race to convert the model to</param>
        private static void ApplyDeformers(TTModel model, XivRace currentRace, XivRace targetRace)
        {
        
            // Current race is already parent node
            // Direct conversion
            // [ Current > (apply deform) > Target ]
            if (currentRace.IsDirectParentOf(targetRace))
            {
                ModelModifiers.ApplyRacialDeform(model, targetRace);
            }
            // Target race is parent node of Current race
            // Convert to parent (invert deform)
            // [ Current > (apply inverse deform) > Target ]
            else if (targetRace.IsDirectParentOf(currentRace))
            {
                ModelModifiers.ApplyRacialDeform(model, currentRace, true);
            }
            // Current race is not parent of Target Race and Current race has parent
            // Make a recursive call with the current races parent race
            // [ Current > (apply inverse deform) > Current.Parent > Recursive Call ]
            else if (currentRace.GetNode().Parent != null)
            {
                ModelModifiers.ApplyRacialDeform(model, currentRace, true);
                ApplyDeformers(model, currentRace.GetNode().Parent.Race, targetRace);
            }
            // Current race has no parent
            // Make a recursive call with the target races parent race
            // [ Target > (apply deform on Target.Parent) > Target.Parent > Recursive Call ]
            else
            {
                ModelModifiers.ApplyRacialDeform(model, targetRace.GetNode().Parent.Race);
                ApplyDeformers(model, targetRace.GetNode().Parent.Race, targetRace);
            }
        }

        [Serializable]
		public class CharacterFile
		{
			public enum Genders : byte
			{
				Masculine = 0,
				Feminine = 1,
			}

			public enum Races : byte
			{
				Hyur = 1,
				Elezen = 2,
				Lalafel = 3,
				Miqote = 4,
				Roegadyn = 5,
				AuRa = 6,
				Hrothgar = 7,
				Viera = 8,
			}

			public enum Tribes : byte
			{
				Midlander = 1,
				Highlander = 2,
				Wildwood = 3,
				Duskwight = 4,
				Plainsfolk = 5,
				Dunesfolk = 6,
				SeekerOfTheSun = 7,
				KeeperOfTheMoon = 8,
				SeaWolf = 9,
				Hellsguard = 10,
				Raen = 11,
				Xaela = 12,
				Helions = 13,
				TheLost = 14,
				Rava = 15,
				Veena = 16,
			}

			public Races Race { get; set; }
			public Genders Gender { get; set; }
			public byte Height { get; set; }
			public Tribes Tribe { get; set; }
			public byte Head { get; set; }
			public byte Hair { get; set; }
			public byte Eyebrows { get; set; }
			public byte Eyes { get; set; }
			public byte Nose { get; set; }
			public byte Jaw { get; set; }
			public byte Mouth { get; set; }
			public byte TailEarsType { get; set; }
			public byte Bust { get; set; }

			// weapons
			public WeaponSave MainHand { get; set; }
			public WeaponSave OffHand { get; set; }

			// equipment
			public ItemSave HeadGear { get; set; }
			public ItemSave Body { get; set; }
			public ItemSave Hands { get; set; }
			public ItemSave Legs { get; set; }
			public ItemSave Feet { get; set; }
			public ItemSave Ears { get; set; }
			public ItemSave Neck { get; set; }
			public ItemSave Wrists { get; set; }
			public ItemSave LeftRing { get; set; }
			public ItemSave RightRing { get; set; }

            public XivRace GetXivRace()
			{
                if (this.Race == Races.Hyur && this.Tribe == Tribes.Midlander && this.Gender == Genders.Masculine) return XivRace.Hyur_Midlander_Male;
                if (this.Race == Races.Hyur && this.Tribe == Tribes.Midlander && this.Gender == Genders.Feminine) return XivRace.Hyur_Midlander_Female;
                if (this.Race == Races.Hyur && this.Tribe == Tribes.Highlander && this.Gender == Genders.Masculine) return XivRace.Hyur_Highlander_Male;
                if (this.Race == Races.Hyur && this.Tribe == Tribes.Highlander && this.Gender == Genders.Feminine) return XivRace.Hyur_Highlander_Female;
                if (this.Race == Races.Elezen && this.Gender == Genders.Masculine) return XivRace.Elezen_Male;
                if (this.Race == Races.Elezen && this.Gender == Genders.Feminine) return XivRace.Elezen_Female;
                if (this.Race == Races.Miqote && this.Gender == Genders.Masculine) return XivRace.Miqote_Male;
                if (this.Race == Races.Miqote && this.Gender == Genders.Feminine) return XivRace.Miqote_Female;
                if (this.Race == Races.Roegadyn && this.Gender == Genders.Masculine) return XivRace.Roegadyn_Male;
                if (this.Race == Races.Roegadyn && this.Gender == Genders.Feminine) return XivRace.Roegadyn_Female;
                if (this.Race == Races.Lalafel && this.Gender == Genders.Masculine) return XivRace.Lalafell_Male;
                if (this.Race == Races.Lalafel && this.Gender == Genders.Feminine) return XivRace.Lalafell_Female;
                if (this.Race == Races.AuRa && this.Gender == Genders.Masculine) return XivRace.AuRa_Male;
                if (this.Race == Races.AuRa && this.Gender == Genders.Feminine) return XivRace.AuRa_Female;
                if (this.Race == Races.Hrothgar && this.Gender == Genders.Masculine) return XivRace.Hrothgar_Male;
                if (this.Race == Races.Hrothgar && this.Gender == Genders.Feminine) return XivRace.Hrothgar_Female;
                if (this.Race == Races.Viera && this.Gender == Genders.Masculine) return XivRace.Viera_Male;
                if (this.Race == Races.Viera && this.Gender == Genders.Feminine) return XivRace.Viera_Female;

                throw new Exception($"Unknown Xiv Race: {this.Race}, {this.Tribe}, {this.Gender}");
            }

			[Serializable]
			public class WeaponSave
			{
				public ushort ModelSet { get; set; }
				public ushort ModelBase { get; set; }
				public ushort ModelVariant { get; set; }
			}

			[Serializable]
			public class ItemSave
			{
				public ushort ModelBase { get; set; }
				public byte ModelVariant { get; set; }
			}
		}
	}
}
