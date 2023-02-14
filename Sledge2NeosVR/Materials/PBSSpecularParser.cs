using BaseX;
using CodeX;
using FrooxEngine;
using Sledge.Formats.Texture.Vtf;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * --- Albedo texture ---
 * "$basetexture" "some/path/texturename" -> Albedo texture
 * "$color" "[ 1 1 1 ]" -> Albedo color tint (RGB format)
 * "$basetexturetransform" <matrix> -> center .5 .5 scale 1 1 rotate 0 translate 0 0 -> TODO: get more details
 * --- Detail texture ---
 * "$detail" "some/path/texturename" -> Detail albedo texture
 * "$detailscale" "3" -> Detail albedo texture scale
 * "$detailtint" "[.5 .5 .5]" -> Detail texture color tint (RGB format)
 * "$detailtexturetransform" <matrix> -> center .5 .5 scale 1 1 rotate 0 translate 0 0 -> TODO: get more details
 * --- Normal map ---
 * "$normalmap" "some/path/texturename" -> Normal map texture
 * "$bumpmap" "some/path/texturename" -> Same as normal map texture
 * "$AmbientOcclTexture" "some/path/texturename" -> Ambient occlusion texture
 * "$AmbientOcclColor" "[.4 .4 .4]" -> Ambient occlusion color tint (RGB format)
 * "$AmbientOcclusion" "1" -> Controls strength of Ambient Occlusion. (1 = fully enabled, 0 = fully disabled)
 * --- Specular map ---
 * "$envmapmask" "some/path/texturename" -> Specular texture
 * "$envmaptint" "[1 1 1]" -> Specular color tint (RGB format)
 * "$basealphaenvmapmask" "1" -> Albedo contains specular map in alpha channel
 * "$normalmapalphaenvmapmask" "1" -> The normal map contains a specular map in its alpha channel
 * "$envmapmaskintintmasktexture" "1" -> Use the red channel of the $tintmasktexture as the specular mask.
 * --- Emission ---
 * "$selfillum" "1" -> Material is emissive, use albedo texture with blacked out background if "$selfillummask" is not set
 * "$selfillum_envmapmask_alpha" "1" -> replaces the original "$selfillum" command
 * "$selfillumtexture" "some/path/texturename" -> Emission texture
 * "$selfillummask" "some/path/texturename" -> Emission texture
 * "$selfillumtint" "[1 1 1]" -> Emission color tint (RGB format)
 * "$selfillummaskscale" "1" -> Scales the self-illumination effect strength. Default value is 1.0
 * --- Alpha ---
 * "$alpha" "1" -> It scales the opacity of an entire material by the given value. 1 is entirely opaque, 0 is invisible.
 * "$alphatest" "1" -> Alpha clip enable/disable, (bool, 0 / 1)
 * "$alphatestreference" ".5" -> Alpha clip cutoff value, (float, 0 - 1)
 * "$translucent" "1" -> It specifies that the material should be partially see-through. (bool)
 * --- Culling ---
 * "$nocull" "1" ->  It disables backface culling, resulting in triangles showing from both sides.
 * 
 * There are two ways of expressing a color tint value
 * "[ <float> <float> <float> ]"    -> going from 0 to 1
 * "{ <int> <int> <int> }"          -> going 0 to 255
 */

namespace Sledge2NeosVR;

/// <summary>
/// 
/// </summary>
public abstract class PBSSpecularParser
{
    protected readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>()
    {
        "$basetexture", "$detail", "$normalmap", "$bumpmap", "$envmapmask", "$normalmapalphaenvmapmask"
    };

    public virtual async Task<PBS_Specular> CreateMaterial(List<KeyValuePair<string, string>> properties)
    {
        PBS_Specular currentMaterial = new PBS_Specular();

        Dictionary<string, VtfFile> vtfDictionary = new Dictionary<string, VtfFile>();

        foreach (KeyValuePair<string, string> currentProperty in properties)
        {
            if (!propertyTextureNamesHashSet.Contains(currentProperty.Key))
            {
                UniLog.Error($"Property {currentProperty.Key} was not a propertyTextureName!");
                continue;
            }

            string currentTextureName = currentProperty.Value.Split('/').Last();
            if (!vtfDictionary.ContainsKey(currentTextureName))
            {
                UniLog.Error($"Couldn't find texture {currentTextureName} in dictionary, skipping");
                continue;
            }

            if (vtfDictionary.TryGetValue(currentTextureName, out VtfFile currentVtf))
            {
                UniLog.Error($"Texture was not found with name {currentTextureName}");
                continue;
            }

            // VTF contains the mip-maps baked in, we only care about the last original image
            VtfImage currentVtfImage = currentVtf.Images.GetLast();

            var newBitmap = new Bitmap2D(currentVtfImage.GetBgra32Data(), currentVtfImage.Width, currentVtfImage.Height, TextureFormat.BGRA32, false);
            var currentUri = await Engine.Current.LocalDB.SaveAssetAsync(newBitmap).ConfigureAwait(false);
            StaticTexture2D currentTexture2D = new StaticTexture2D();
            currentTexture2D.URL.Value = currentUri;

            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Pointsample))
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Point;
            }
            else if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Trilinear))
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Trilinear;
            }
            else
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Anisotropic;
                currentTexture2D.AnisotropicLevel.Value = 8;
            }

            // It is assumed that there will be at most one
            switch (currentProperty.Key)
            {
                case "$basetexture":
                    currentMaterial.AlbedoTexture.Target = currentTexture2D;
                    ExtractAlpha(Source.ALBEDO,  currentTexture2D, ref currentMaterial, ref properties);
                    break;
                case "$detail":
                    currentMaterial.DetailAlbedoTexture.Target = currentTexture2D;
                    break;
                case "$normalmap":
                case "$bumpmap":
                    // Source engine uses the DirectX standard for normal maps, NeosVR uses OpenGL
                    // So we need to invert the green channel
                    // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)
                    currentTexture2D.IsNormalMap.Value = true;
                    currentTexture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
                    currentMaterial.NormalMap.Target = currentTexture2D;
                    ExtractAlpha(Source.NORMAL, currentTexture2D, ref currentMaterial, ref properties);
                    break;
                case "$envmapmask":
                    ExtractAlpha(Source.SPECULAR, currentTexture2D, ref currentMaterial, ref properties);
                    break;
            }
        }
        return currentMaterial;
    }

    private void ExtractAlpha(Source fromTexture, StaticTexture2D referenceTexture, ref PBS_Specular currentMaterial, ref List<KeyValuePair<string, string>> properties)
    {
        switch (fromTexture)
        {
            case Source.ALBEDO:
                if (currentMaterial.AlbedoTexture == null || !currentMaterial.AlbedoTexture.Target.Asset.HasAlpha)
                    return;
                break;
            case Source.NORMAL:
                if (currentMaterial.NormalMap == null || !currentMaterial.NormalMap.Target.Asset.HasAlpha)
                    return;
                // Specular
                else if (currentMaterial.SpecularMap == null)
                {
                    foreach (KeyValuePair<string, string> canidate in properties)
                    {
                        if (canidate.Key == "$normalmapalphaenvmapmask")
                        {
                            StaticTexture2D normalToSpecMap = referenceTexture;
                            normalToSpecMap.ProcessPixels((color c) => new color(0f, 0f, 0f, c.a));
                            currentMaterial.SpecularMap.Target = normalToSpecMap;
                            break;
                        }
                    }
                }
                break;
        }
    }
   
    private enum Source
    {
        ALBEDO,
        NORMAL,
        SPECULAR
    }
}