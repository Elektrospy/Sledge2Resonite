using BaseX;
using CodeX;
using FrooxEngine;
using Sledge.Formats.Texture.Vtf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/*
 * --- Albedo texture ---
 * "$basetexture" "some/path/texturename" -> Albedo texture
 * "$color" "[1 1 1]" -> Albedo color tint (RGB format)
 * "$basetexturetransform" <matrix> -> center .5 .5 scale 1 1 rotate 0 translate 0 0 -> TODO: get more details
 * --- Detail texture ---
 * "$detail" "some/path/texturename" -> Detail albedo texture
 * "$detailscale" "1" -> Detail albedo texture scale, default = 4 if not specified
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

public abstract class PBSSpecularParser
{
    protected readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>()
    {
        "$basetexture", "$detail", "$normalmap", "$bumpmap", "$envmapmask, $ambientoccltexture"
    };

    public virtual async Task<PBS_Specular> ParseMaterial(List<KeyValuePair<string, string>> properties, string name)
    {
        await default(ToWorld);
        var currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Material: " + name);
        currentSlot.PositionInFrontOfUser();
        var currentMaterial = currentSlot.CreateMaterialOrb<PBS_Specular>();
        await default(ToBackground);

        Uri albedoURLCopy = null;

        foreach (KeyValuePair<string, string> currentProperty in properties)
        {
            if (!propertyTextureNamesHashSet.Contains(currentProperty.Key))
            {
                continue;
            }

            // get texture name and try to grab it from dictionary
            string currentTextureName = currentProperty.Value.Split('/').Last();
            if (!Sledge2NeosVR.vtfDictionary.TryGetValue(currentTextureName, out VtfFile currentVtf))
            {
                UniLog.Error($"Texture was not found in dictionary with name {currentTextureName}");

                foreach (var types in Sledge2NeosVR.vtfDictionary)
                {
                    UniLog.Log(types.Key + ", " + types.Value);
                }

                continue;
            }

            // VTF contains mip-maps, but we only care about the last original image
            VtfImage currentVtfImage = currentVtf.Images.GetLast();
            var newBitmap = new Bitmap2D(currentVtfImage.GetBgra32Data(), currentVtfImage.Width, currentVtfImage.Height, TextureFormat.BGRA32, false);
            await default(ToWorld);
            StaticTexture2D currentTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
            currentTexture2D.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);

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

            // It is assumed that there will be at least one
            switch (currentProperty.Key)
            {
                case "$basetexture":
                    await default(ToWorld);
                    albedoURLCopy = currentTexture2D.URL.Value;
                    currentMaterial.AlbedoTexture.Target = currentTexture2D;

                    if (!currentMaterial.SpecularMap.IsAssetAvailable)
                    {
                        foreach (KeyValuePair<string, string> canidate in properties)
                        {
                            if (canidate.Key == "$basealphaenvmapmask" && canidate.Value == "1")
                            {
                                currentMaterial.SpecularMap.Target = currentTexture2D;
                                break;
                            }
                        }
                    }

                    await default(ToBackground);
                    break;
                case "$detail":
                    await default(ToWorld);
                    currentMaterial.DetailAlbedoTexture.Target = currentTexture2D;
                    await default(ToBackground);
                    break;
                case "$normalmap":
                case "$bumpmap":
                    // Source engine uses the DirectX standard for normal maps, NeosVR uses OpenGL
                    // So we need to invert the green channel
                    // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)

                    // Determine if envmaptint exists
                    bool flag = true;
                    float3 tint = float3.Zero;
                    tint = new float3(0.4f, 0.4f, 0.4f);

                    await default(ToWorld);
                    currentTexture2D.IsNormalMap.Value = true;
                    currentMaterial.NormalMap.Target = currentTexture2D;
                    await default(ToBackground);

                    if (albedoURLCopy is not null && currentMaterial.AlbedoTexture is not null)
                    {
                        await default(ToWorld);
                        var modifiedAlbedoTexture = currentSlot.AttachComponent<StaticTexture2D>();
                        modifiedAlbedoTexture.URL.Value = albedoURLCopy;
                        await default(ToBackground);

                        if (flag)
                        {
                            UniLog.Log("got the tint lmao");
                            var newColor = new color(
                                tint.x,
                                tint.y,
                                tint.z);
                            modifiedAlbedoTexture.ProcessPixels((color c) => c * newColor);
                        }
                        else
                        {
                            UniLog.Log("no tint kek");
                        }

                        modifiedAlbedoTexture.ProcessBitmap((Bitmap2D albedo) =>
                        {
                            UniLog.Log("preprocessing pixels1");
                            return CreateSpecularByAlphaTransfer(albedo, newBitmap);
                        });

                        await default(ToWorld);
                        currentMaterial.SpecularMap.Target = modifiedAlbedoTexture;
                        await default(ToBackground);
                    }

                    break;
                case "$envmaptint":
                    tint = new float3(0.4f, 0.4f, 0.4f);
                    //if (Float3Extensions.GetFloat3FromString(currentProperty.Value, out float3 val))
                    //{
                    //    UniLog.Log("Parsed float3 with " + val);
                    //    tint = val;
                    //}
                    //else
                    //{
                    //    UniLog.Log("Failed to parse float3 with " + val);
                    //    tint = float3.Zero;
                    //}
                    break;
            }
        }

        // Convert key value list to dictionary for easier access
        var propertiesDictionary = properties.ToDictionary(x => x.Key, x => x.Value);

        // Try to find, parse and assign properties
        if (propertiesDictionary.TryGetValue("$detailscale", out string currentDetailScale))
        {
            await default(ToWorld);
            if (Float2Extensions.GetFloat2FromString(currentDetailScale, out float2 canidate))
            {
                currentMaterial.DetailTextureScale.Value = canidate;
            }
            else
            {
                var newScale = float.Parse(currentDetailScale, CultureInfo.InvariantCulture.NumberFormat);
                currentMaterial.DetailTextureScale.Value = new float2(newScale, newScale);
            }
            await default(ToBackground);
        }

        if (propertiesDictionary.TryGetValue("$envmapmask", out string currentEnvmapmask))
        {
            await default(ToBackground);
            string currentEnvmapmaskName = currentEnvmapmask.Split('/').Last();

            if(Sledge2NeosVR.vtfDictionary.TryGetValue(currentEnvmapmaskName, out var tempEnvMapBitmap2D) 
                && Sledge2NeosVR.vtfDictionary.TryGetValue(currentEnvmapmaskName, out var tempAlbedoBitmap2D) )
            {
                var albMap = tempAlbedoBitmap2D.Images.GetLast();
                var envMap = tempEnvMapBitmap2D.Images.GetLast();
                var albMapModified = albMap.GetBgra32Data();
                var envMapModified = envMap.GetBgra32Data();
                for (int x = 0; x < envMapModified.Length; x += 4)
                {
                    envMapModified[x + 3] = (byte) // A
                        ((envMapModified[x] + // B
                        envMapModified[x + 1] + // G
                        envMapModified[x + 2]) / 3); // R

                    envMapModified[x] = albMapModified[x]; // B
                    envMapModified[x + 1] = albMapModified[x + 1];  // G
                    envMapModified[x + 2] = albMapModified[x + 2]; // R
                }

                UniLog.Log("preprocessing pixels 2");
                var finalMap = CreateSpecularByAlphaTransfer(
                    new Bitmap2D(albMapModified, albMap.Width, albMap.Height, TextureFormat.BGRA32, false),
                    new Bitmap2D(envMapModified, envMap.Width, envMap.Height, TextureFormat.BGRA32, false));

                await default(ToWorld);
                StaticTexture2D finalTexture = currentSlot.AttachComponent<StaticTexture2D>();
                finalTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(finalMap);
                await default(ToBackground);
                
                await default(ToWorld);
                currentMaterial.SpecularMap.Target = finalTexture;
                await default(ToBackground);
            }
        }

        return currentMaterial;
    }

    private Bitmap2D CreateSpecularByAlphaTransfer(Bitmap2D albedo, Bitmap2D donorBitmap)
    {
        // Check if they bitmaps even contain transparent
        if (!donorBitmap.HasTransparentPixels())
        {
            UniLog.Log("no transparent pixels");
            return albedo;
        }

        // Rescale donor bitmap to match albedo bitmap
        if (albedo.Size != donorBitmap.Size) 
        {
            UniLog.Log("downscaling");
            donorBitmap.GetRescaled(albedo.Size, false, false, Filtering.Lanczos3);
        }

        var albedoRaw = albedo.RawData;
        var donorRaw = donorBitmap.RawData;
        for (int i = 3; i < albedoRaw.Length; i += 4)
        {
            // Every 4th byte (alpha), set albedo alpha byte to donorbitmap byte value
            albedoRaw[i] = donorRaw[i];
        }

        UniLog.Log("done debugging");

        return albedo;
    }
}