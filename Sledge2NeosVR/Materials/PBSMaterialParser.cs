using BaseX;
using CodeX;
using FrooxEngine;
using Sledge.Formats.Texture.Vtf;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sledge2NeosVR;

public abstract class PBSMaterialParser<T> : PBS_Material, IPBS_Material
{
    private readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>() {
            "$basetexture", "$detail", "$normalmap", "$bumpmap", "$envmapmask"
    };

    public async Task<PBS_Material> CreateMaterial(List<KeyValuePair<string, string>> properties)
    {
        PBS_Material mat;

        if (typeof(T) is IPBS_Metallic)
        {
            mat = new PBS_Metallic();
        }
        else // (typeof(T) is IPBS_Specular)
        {
            mat = new PBS_Specular();
        }

        Dictionary<string, VtfFile> vtfDictionary = new Dictionary<string, VtfFile>();

        foreach (KeyValuePair<string, string> currentProperty in properties)
        {
            // handle specific textures
            if (propertyTextureNamesHashSet.Contains(currentProperty.Key))
            {
                string currentTextureName = currentProperty.Value.Split('/').Last();
                if (!vtfDictionary.ContainsKey(currentTextureName))
                {
                    // Error(string.Format("couldn't find texture {0} in dictionary, skipping", currentTextureName));
                    continue;
                }

                VtfFile tempVtf;
                vtfDictionary.TryGetValue(currentTextureName, out tempVtf);
                // vtf can contain multiple frames / images, we currently don't handle that
                VtfImage tempVtfImage = tempVtf.Images.GetFirst();

                // encode 
                var newBitmap = new Bitmap2D(tempVtfImage.GetBgra32Data(), tempVtfImage.Width, tempVtfImage.Height, TextureFormat.BGRA32, false);
                var tempUri = await Engine.Current.LocalDB.SaveAssetAsync(newBitmap).ConfigureAwait(false);
                StaticTexture2D tempTexture2D = new StaticTexture2D();
                tempTexture2D.URL.Value = tempUri;

                switch (currentProperty.Key)
                {
                    case "$basetexture":
                        mat.AlbedoTexture.Target = tempTexture2D;
                        break;
                    case "$detail":
                        mat.DetailAlbedoTexture.Target = tempTexture2D;
                        break;
                    case "$normalmap":
                    case "$bumpmap":
                        tempTexture2D.IsNormalMap.Value = true;
                        mat.NormalMap.Target = tempTexture2D;
                        break;
                    case "$envmapmask":
                        break;
                }
            }
        }

        return mat;
    }
}