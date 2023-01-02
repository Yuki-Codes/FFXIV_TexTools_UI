﻿using FFXIV_TexTools.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views.Textures
{
    public enum MaterialEditorMode
    {
        EditSingle,
        EditMulti,
        NewSingle,
        NewRace,
        NewMulti
    }
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditorView
    {
        private MaterialEditorViewModel viewModel;
        private XivMtrl _material;
        private IItemModel _item;
        private MaterialEditorMode _mode;

        public ObservableCollection<KeyValuePair<MtrlShader, string>> ShaderSource;
        public ObservableCollection<KeyValuePair<MtrlShaderPreset, string>> PresetSource;
        private static XivMtrl _copiedMaterial;
        public XivMtrl Material
        {
            get
            {
                return _material;
            }
        }

        public MaterialEditorView()
        {
            InitializeComponent();
            viewModel = new MaterialEditorViewModel(this);

            // Setup for the combo boxes.
            ShaderSource = new ObservableCollection<KeyValuePair<MtrlShader, string>>();
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Standard, "Standard".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Glass, "Glass".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Skin, "Skin".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Hair, "Hair".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Iris, "Iris".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Furniture, "Furniture".L()));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.DyeableFurniture, "Dyeable Furniture".L()));
            ShaderComboBox.ItemsSource = ShaderSource;
            ShaderComboBox.DisplayMemberPath = "Value";
            ShaderComboBox.SelectedValuePath = "Key";


            PresetSource = new ObservableCollection<KeyValuePair<MtrlShaderPreset, string>>();
            PresetSource.Add(new KeyValuePair<MtrlShaderPreset, string>(MtrlShaderPreset.Default, "Default".L()));
            PresetComboBox.ItemsSource = PresetSource;
            PresetComboBox.DisplayMemberPath = "Value";
            PresetComboBox.SelectedValuePath = "Key";

            PasteMaterialButton.IsEnabled = _copiedMaterial != null;

            Dictionary<bool, string> TransparencySource = new Dictionary<bool, string>();
            TransparencySource.Add(true, "Enabled".L());
            TransparencySource.Add(false, "Disabled".L());
            TransparencyComboBox.ItemsSource = TransparencySource;
            TransparencyComboBox.DisplayMemberPath = "Value";
            TransparencyComboBox.SelectedValuePath = "Key";

            Dictionary<bool, string> BackfacesSource = new Dictionary<bool, string>();
            BackfacesSource.Add(true, "Show Backfaces".L());
            BackfacesSource.Add(false, "Hide Backfaces".L());
            BackfacesComboBox.ItemsSource = BackfacesSource;
            BackfacesComboBox.DisplayMemberPath = "Value";
            BackfacesComboBox.SelectedValuePath = "Key";

            Dictionary<bool, string> ColorsetSource = new Dictionary<bool, string>();
            ColorsetSource.Add(true, "Enabled".L());
            ColorsetSource.Add(false, "Disabled".L());
            ColorsetComboBox.ItemsSource = ColorsetSource;
            ColorsetComboBox.DisplayMemberPath = "Value";
            ColorsetComboBox.SelectedValuePath = "Key";
            ColorsetComboBox.IsEnabled = false;

            DisableButton.IsEnabled = false;
            DisableButton.Visibility = Visibility.Hidden;

            SaveButton.Click += SaveButton_Click;
        }

        public async Task<bool> SetMaterial(XivMtrl material, IItemModel item, MaterialEditorMode mode = MaterialEditorMode.EditSingle)
        {
            _material = material;
            _item = item;
            _mode = mode;

            return await viewModel.SetMaterial(material, item, mode);
        }

        public XivMtrl GetMaterial()
        {
            return viewModel.GetMaterial();
        }

        public void Close(bool result)
        {
            DialogResult = result;
            Close();

        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _material = await viewModel.SaveChanges();
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }


        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShaderComboBox.SelectedValue == null)
            {
                return;
            }

            var shader = (MtrlShader)ShaderComboBox.SelectedValue;
            var presets = ShaderInfo.GetAvailablePresets(shader);

            PresetSource.Clear();
            foreach (var p in presets)
            {
                PresetSource.Add(new KeyValuePair<MtrlShaderPreset, string>(p, p.ToString().L()));
            }

            // Disable the box if the user has no choice anyways.
            if(PresetSource.Count > 1)
            {
                PresetComboBox.IsEnabled = true;
            } else
            {
                PresetComboBox.IsEnabled = false;
            }


            PresetComboBox.SelectedValue = MtrlShaderPreset.Default;
            // Ensure the UI is updated for the new selection.
            PresetComboBox_SelectionChanged(null, null);


            if(shader == MtrlShader.Other || shader == MtrlShader.Furniture || shader == MtrlShader.DyeableFurniture)
            {
                // Disable everything.
                NormalTextBox.IsEnabled = false;
                DiffuseTextBox.IsEnabled = false;
                SpecularTextBox.IsEnabled = false;
                ColorsetComboBox.IsEnabled = false;
                NewSharedButton.IsEnabled = false;
                NewUniqueButton.IsEnabled = false;
                PresetComboBox.IsEnabled = false;
                TransparencyComboBox.IsEnabled = false;
                ShaderComboBox.IsEnabled = false;
                SaveButton.IsEnabled = false;
                SaveButton.Visibility = Visibility.Hidden;
            }
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ShaderComboBox.SelectedValue == null || PresetComboBox.SelectedValue == null)
            {
                return;
            }

            var shader = (MtrlShader)ShaderComboBox.SelectedValue;
            var preset = (MtrlShaderPreset)PresetComboBox.SelectedValue;
            var transparency = (bool)TransparencyComboBox.SelectedValue;

            // Generate a fresh shader info so we can access some of the calculated fields.
            var info = new ShaderInfo() { Shader = shader, Preset = preset, TransparencyEnabled = transparency };

            ColorsetComboBox.SelectedValue = info.HasColorset;

            if (info.HasMulti)
            {
                SpecularLabel.Content = "Multi:".L();
                SpecularTextBox.IsEnabled = true;
                SpecularLabel.Visibility = Visibility.Visible;
                SpecularTextBox.Visibility = Visibility.Visible;
            } else if(info.HasSpec)
            {
                SpecularLabel.Content = "Specular:".L();
                SpecularTextBox.IsEnabled = true;
                SpecularLabel.Visibility = Visibility.Visible;
                SpecularTextBox.Visibility = Visibility.Visible;
            } else
            {
                // This path is never actually reached currently.
                SpecularLabel.Content = "Specular:".L();
                SpecularTextBox.IsEnabled = false;
                SpecularLabel.Visibility = Visibility.Hidden;
                SpecularTextBox.Visibility = Visibility.Hidden;
            }

            if (info.HasDiffuse)
            {
                DiffuseLabel.Content = "Diffuse:".L();
                DiffuseTextBox.IsEnabled = true;
                DiffuseLabel.Visibility = Visibility.Visible;
                DiffuseTextBox.Visibility = Visibility.Visible;
            }
            else if(info.HasReflection)
            {
                DiffuseLabel.Content = "Reflection:".L();
                DiffuseTextBox.IsEnabled = true;
                DiffuseLabel.Visibility = Visibility.Visible;
                DiffuseTextBox.Visibility = Visibility.Visible;

            } else
            {
                DiffuseLabel.Content = "Diffuse:".L();
                DiffuseTextBox.IsEnabled = false;
                DiffuseLabel.Visibility = Visibility.Hidden;
                DiffuseTextBox.Visibility = Visibility.Hidden;
            }

            if(info.ForcedTransparency != null)
            {
                TransparencyComboBox.IsEnabled = false;
                TransparencyComboBox.SelectedValue = info.ForcedTransparency;
            } else
            {
                TransparencyComboBox.IsEnabled = true;
            }



        }


        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.Textures.MaterialEditorHelpView();
            help.ShowDialog();
        }

        private void CopyMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            _copiedMaterial = _material;
            PasteMaterialButton.IsEnabled = true;
        }

        private async void PasteMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedMaterial != null)
            {
                // Paste the copied Material into the editor using our current path and item.
                _copiedMaterial.MTRLPath = _material.MTRLPath;
                await SetMaterial(_copiedMaterial, _item, _mode);
            }
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.DisableMod();
        }

        private void NewSharedButton_Click(object sender, RoutedEventArgs e)
        {
            var sharedTex = "{item_folder}/{default_name}";
            DiffuseTextBox.Text = sharedTex;
            SpecularTextBox.Text = sharedTex;
            NormalTextBox.Text = sharedTex;
        }

        private void NewUniqueButton_Click(object sender, RoutedEventArgs e)
        {
            var uniqueTex = "{item_folder}/{variant}_{default_name}";
            DiffuseTextBox.Text = uniqueTex;
            SpecularTextBox.Text = uniqueTex;
            NormalTextBox.Text = uniqueTex;

        }
    }
}
