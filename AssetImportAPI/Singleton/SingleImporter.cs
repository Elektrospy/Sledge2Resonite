using CodeX;

namespace AssetImportAPI.Single
{
    public class SingleImporter : AssetImporter
    {
        public override void Initiallize(string FileExtension, AssetClass assetClass)
        {
            base.Initiallize(FileExtension, assetClass);
        }

        public virtual void Import(string file)
        {
            base.Import(file);
        }
    }
}
