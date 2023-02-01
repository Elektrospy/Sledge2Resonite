using CodeX;
using FrooxEngine;
using System.IO;

namespace AssetImportAPI.Archive
{
    public class ArchiveImporter : AssetImporter
    {
        public string ArchiveFolderName { get; set; }
        public static string cachePath;

        public override void Initiallize(string FileExtension, AssetClass assetClass)
        {
            base.Initiallize(FileExtension, assetClass);
            cachePath = Path.Combine(Engine.Current.CachePath, "Cache", ArchiveFolderName);
            Directory.CreateDirectory(cachePath);
        }
        public override void Import(string file)
        {
            base.Import(file);
        }
    }
}
