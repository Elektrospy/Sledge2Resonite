using CodeX;
using FrooxEngine;
using System;
using System.IO;

namespace AssetImportAPI
{
    public abstract class AssetImporter : IAssetImporter
    {
        public string FileExtension { get; set; }

        public virtual void Initiallize(string fileExtension, AssetClass assetClass)
        {
            if (string.IsNullOrEmpty(fileExtension))
            {
                throw new ArgumentNullException("FileExtension was null or enpty");
            }

            FileExtension = fileExtension;
            Engine.Current.RunPostInit(() => Utils.AssetPatch(fileExtension));
        }

        public virtual void Import(string file)
        {
            if (!ShouldImportFile(file)) return;
        }
        
        /// <summary>
        /// Utility method to discriminate file types on import. Useful when dealing with arbitrarily large archives.
        /// Will handle recursive cases.
        /// </summary>
        /// <para>
        /// This uses the user's active mod config to change import settings.
        /// </para>
        /// <param name="file">The canidate file to test against</param>
        /// <returns>A boolean indicating if the file should be imported.</returns>
        public bool ShouldImportFile(string file)
        {
            var assetClass = AssetHelper.ClassifyExtension(Path.GetExtension(file));
            return (AssetImporterMod.config.GetValue(AssetImporterMod.importText) && assetClass == AssetClass.Text) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importTexture) && assetClass == AssetClass.Texture) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importDocument) && assetClass == AssetClass.Document) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importMesh) && assetClass == AssetClass.Model
                && Path.GetExtension(file).ToLower() != ".xml") 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importPointCloud) && assetClass == AssetClass.PointCloud) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importAudio) && assetClass == AssetClass.Audio) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importFont) && assetClass == AssetClass.Font) 
            || (AssetImporterMod.config.GetValue(AssetImporterMod.importVideo) && assetClass == AssetClass.Video) 
            || Path.GetExtension(file).ToLower().EndsWith(FileExtension);
        }
    }
}
