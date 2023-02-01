using CodeX;

namespace AssetImportAPI
{
    internal interface IAssetImporter
    {
        public string FileExtension { get; set; }
        public void Initiallize(string FileExtension, AssetClass assetClass);
        public void Import(string path);
    }
}
