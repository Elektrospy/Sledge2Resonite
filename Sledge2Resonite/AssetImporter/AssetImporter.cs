using FrooxEngine;
using Sledge.Formats.Texture.Vtf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sledge2Resonite
{
    internal abstract class AssetImporter<T, U> where U : IAssetProvider
    {
        Dictionary<string, Asset<T, U>> assets { get; set; } = new();
        public Asset<T, U> ImportAsset(string filePath)
        {
            //check if file exists
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }
            string fileName = Path.GetFileName(filePath);
            FileStream stream = new FileStream(filePath, FileMode.Open);
            return ImportAsset(stream, fileName);
        }

        public Asset<T, U> ImportAsset(FileStream fileStream, string name)
        {
            //check if asset already exists
            if (assets.ContainsKey(name))
            {
                return assets[name];
            }

            //check if file is valid
            if (fileStream == null)
            {
                throw new NullReferenceException("fileStream is null");
            }

            Asset<T, U> asset = PreProcessAsset(fileStream);
            assets.Add(name, asset);
            return asset;
        }

        abstract protected Asset<T, U> PreProcessAsset(FileStream file);
    }
}
