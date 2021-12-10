using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
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
using xivModdingFramework.Models.ModelTextures;

namespace FFXIV_TexTools
{
	internal static class AnamnesisFmvExport
	{
        private static DirectoryInfo GameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
        private static MainWindow MainWin = MainWindow.GetMainWindow();
        private static object LockObj = new object();

        private static FullModelView Fmv;

        public static void Run()
		{
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Anamnesis Character|*.chara";
            DialogResult result = dlg.ShowDialog();
            if (result != DialogResult.OK)
                return;

			_ = RunInternal(dlg.FileName);
		}

		private static async Task RunInternal(string path)
		{ 
			try
			{
                await MainWin.LockUi("Exporting Anamnesis Character", "....", LockObj);
                MainWin.LockProgress.Report("Loading chara file...");

                // red chara file
                string json = File.ReadAllText(path);
				CharacterFile character = JsonConvert.DeserializeObject<CharacterFile>(json);
                XivRace race = character.GetXivRace();

                // load all items we can
                MainWin.LockProgress.Report("Loading items...");
                List<IItem> allItems = await XivCache.GetFullItemList();

                // load the Fmv
                MainWin.LockProgress.Report("Starting FMV...");
                Fmv = FullModelView.Instance;
                Fmv.Owner = MainWin;
                Fmv.Show();

                await Task.Delay(500);

                // Body
                MainWin.LockProgress.Report("Loading Face");
                await AddToFmv(GetFaceModel(allItems, race, character.Head, character.Eyebrows, character.Eyes, character.Nose, character.Jaw, character.Mouth), race);
                MainWin.LockProgress.Report("Loading Hair");
                await AddToFmv(GetHairModel(allItems, race, character.Hair), race);

                // Gear
                MainWin.LockProgress.Report("Loading Item: Head");
                await AddToFmv(GetItemModel(allItems, character.HeadGear, "Head"), race);
                MainWin.LockProgress.Report("Loading Item: Body");
                await AddToFmv(GetItemModel(allItems, character.Body, "Body"), race);
                MainWin.LockProgress.Report("Loading Item: Hands");
                await AddToFmv(GetItemModel(allItems, character.Hands, "Hands"), race);
                MainWin.LockProgress.Report("Loading Item: Legs");
                await AddToFmv(GetItemModel(allItems, character.Legs, "Legs"), race);
                MainWin.LockProgress.Report("Loading Item: Feet");
                await AddToFmv(GetItemModel(allItems, character.Feet, "Feet"), race);
                MainWin.LockProgress.Report("Loading Item: Earring");
                await AddToFmv(GetItemModel(allItems, character.Ears, "Earring"), race);
                MainWin.LockProgress.Report("Loading Item: Neck");
                await AddToFmv(GetItemModel(allItems, character.Neck, "Neck"), race);
                MainWin.LockProgress.Report("Loading Item: Wrists");
                await AddToFmv(GetItemModel(allItems, character.Wrists, "Wrists"), race);
                MainWin.LockProgress.Report("Loading Item: Left Ring");
                await AddToFmv(GetItemModel(allItems, character.LeftRing, "Rings"), race);
                MainWin.LockProgress.Report("Loading Item: Right Ring");
                await AddToFmv(GetItemModel(allItems, character.RightRing, "Rings"), race);

                // Weapons
                // Weapons crash the FMV, even normally a user cnat add a weapon to the FMV for some reason.
                /*MainWin.LockProgress.Report("Loading Weapon: Main Hand");
                await AddToFmv(GetWeaponModel(allItems, character.MainHand, true), race);
                MainWin.LockProgress.Report("Loading Weapon: Off Hand");
                await AddToFmv(GetWeaponModel(allItems, character.OffHand, false), race);*/
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

        private static IItemModel GetHairModel(List<IItem> allItems, XivRace race, byte hair)
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

        private static IItemModel GetFaceModel(List<IItem> allItems, XivRace race, byte head, byte eyebrows, byte eyes, byte nose, byte jaw, byte mouth)
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

        private static IItemModel GetItemModel(List<IItem> allItems, CharacterFile.ItemSave itemSave, string category)
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

        private static IItemModel GetWeaponModel(List<IItem> allItems, CharacterFile.WeaponSave weaponSave, bool mainHand)
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

        private static async Task AddToFmv(IItemModel item, XivRace race)
		{
            if (item == null)
                return;

            MainWin.LockProgress.Report($"Loading FMV: {item.Name}");

            Mdl _mdl = new Mdl(GameDirectory, item.DataFile);

            TTModel model = await _mdl.GetModel(item, race);

            if (model == null)
            {
                List<XivRace> priority = race.GetModelPriorityList();
                XivRace actualRace = race;
                foreach (XivRace newRace in priority)
                {
                    model = await _mdl.GetModel(item, newRace);
                    actualRace = newRace;

                    if (model != null)
                    {
                        break;
                    }
                }
            }

            if (model == null)
                throw new Exception($"Failed to get model for item: {item}");

            Dictionary<int, ModelTextureData> textureData = await GetMaterials(item, model, race);

			await Fmv.AddModel(model, textureData, item, actualRace);
            await Task.Delay(4000);
		}

        // Taken from ModelViewModel:1771
        private static async Task<Dictionary<int, ModelTextureData>> GetMaterials(IItemModel item, TTModel model, XivRace race)
        {
			Dictionary<int, ModelTextureData> textureDataDictionary = new Dictionary<int, ModelTextureData>();
            if (model == null)
                return textureDataDictionary;

			Dictionary<int, XivMtrl> mtrlDictionary = new Dictionary<int, XivMtrl>();
			Mtrl mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
			List<string> mtrlFilePaths = model.Materials;

			int materialNum = 0;
            foreach (string mtrlFilePath in mtrlFilePaths)
            {
				IItemModel mtrlItem = (IItemModel)item.Clone();

                int modelID = mtrlItem.ModelInfo.PrimaryID;
				int bodyID = mtrlItem.ModelInfo.SecondaryID;
				string filePath = mtrlFilePath;

                if (!filePath.Contains("hou") && mtrlFilePath.Count(x => x == '/') > 1)
                {
                    filePath = mtrlFilePath.Substring(mtrlFilePath.LastIndexOf("/"));
                }

				string typeChar = $"{mtrlFilePath[4]}{mtrlFilePath[9]}";

				string raceString = "";
                switch (typeChar)
                {
                    // Character Body
                    case "cb":
						string body = mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4);
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);

                        // XIV automatically forces skin materials to instead reference the appropiate one for the character wearing it.
                        race = XivRaceTree.GetSkinRace(race);


						int gender = 0;
                        if (int.Parse(XivRaces.GetRaceCode(race).Substring(0, 2)) % 2 == 0)
                        {
                            gender = 1;
                        }

						// Get the actual skin the user's preferred race uses.
						XivRace settingsRace = XivRaceTree.GetSkinRace(GetSettingsRace(gender).Race);
						string settingsBody = settingsRace == GetSettingsRace(gender).Race ? GetSettingsRace(gender).BodyID : "0001";

						// If the user's race is a child of the item's race, we can show the user skin instead.
						bool useSettings = XivRaceTree.IsChildOf(settingsRace, race);
                        if (useSettings)
                        {
                            filePath = mtrlFilePath.Replace(raceString, settingsRace.GetRaceCode()).Replace(body, settingsBody);
                            race = settingsRace;
                            body = settingsBody;
                        }
                        else
                        {
                            // Just use item race.
                            filePath = mtrlFilePath.Replace(raceString, race.GetRaceCode()).Replace(body, "0001");
                        }

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Body,
                            Name = XivStrings.Body,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = int.Parse(body)
                            }
                        };

                        break;
                    // Face
                    case "cf":
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("f") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Face,
                            Name = XivStrings.Face,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
                            }
                        };

                        break;
                    // Hair
                    case "ch":
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("h") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Hair,
                            Name = XivStrings.Hair,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
                            }
                        };

                        break;
                    // Tail
                    case "ct":
						string tempPath = mtrlFilePath.Substring(4);
                        bodyID = int.Parse(tempPath.Substring(tempPath.IndexOf("t") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Tail,
                            Name = XivStrings.Tail,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
                            }
                        };

                        break;
                    // Ears
                    case "cz":
						string tPath = mtrlFilePath.Substring(4);
                        bodyID = int.Parse(tPath.Substring(tPath.IndexOf("z") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Ear,
                            Name = XivStrings.Ear,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
                            }
                        };

                        break;
                    // Equipment
                    case "ce":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("e") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        break;
                    // Accessory
                    case "ca":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("a") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        break;
                    // Weapon
                    case "wb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("w") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    // Monster
                    case "mb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("_m") + 2, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    // DemiHuman
                    case "de":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("d") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("e") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    default:
                        break;
                }

				int dxVersion = int.Parse(Settings.Default.DX_Version);
				XivMtrl mtrlData = await mtrl.GetMtrlData(mtrlItem, filePath, dxVersion);

                if (mtrlData == null)
                    continue;

                mtrlDictionary.Add(materialNum, mtrlData);
                materialNum++;
            }

            foreach (KeyValuePair<int, XivMtrl> xivMtrl in mtrlDictionary)
            {
				CustomModelColors colors = ModelTexture.GetCustomColors();
                colors.InvertNormalGreen = false;

				ModelTextureData modelMaps = await ModelTexture.GetModelMaps(GameDirectory, xivMtrl.Value, colors);
                textureDataDictionary.Add(xivMtrl.Key, modelMaps);
            }

            return textureDataDictionary;
        }

        /// <summary>
        /// Gets the race from the settings
        /// </summary>
        /// <param name="gender">The gender of the currently selected race</param>
        /// <returns>A tuple containing the race and body</returns>
        private static (XivRace Race, string BodyID) GetSettingsRace(int gender)
        {
			string settingsRace = Settings.Default.Default_Race;
			string defaultBody = "0001";

            if (settingsRace.Equals(XivStringRaces.Hyur_M))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0101"), defaultBody);
                }
            }

            if (settingsRace.Equals(XivStringRaces.Hyur_H))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0301"), defaultBody);
                }

                return (XivRaces.GetXivRace("0401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_R))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), defaultBody);
                }

                return (XivRaces.GetXivRace("1401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_X))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), "0101");
                }

                return (XivRaces.GetXivRace("1401"), "0101");
            }

            return (XivRaces.GetXivRace("0201"), defaultBody);
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
