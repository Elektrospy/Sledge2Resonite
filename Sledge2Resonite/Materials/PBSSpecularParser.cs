using Elements.Core;
using Elements.Assets;
using FrooxEngine;
using Sledge.Formats.Texture.Vtf;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TextureFormat = Elements.Assets.TextureFormat;

namespace Sledge2Resonite;

/// <summary>
/// Abstract class providing the base functionality for converting source materials into neos equivalents
/// </summary>
/// <remarks>
///  --- Albedo texture ---
/// "$basetexture" "some/path/texturename" -> Albedo texture
/// "$color" "[1 1 1]" -> Albedo color tint(RGB format)
/// "$basetexturetransform" <matrix> -> center .5 .5 scale 1 1 rotate 0 translate 0 0 -> TODO: get more details
/// --- Detail texture ---
/// "$detail" "some/path/texturename" -> Detail albedo texture
/// "$detailscale" "1" -> Detail albedo texture scale, default = 4 if not specified
/// "$detailtint" "[.5 .5 .5]" -> Detail texture color tint(RGB format)
/// "$detailblendfactor" "1" -> Lower values make the texture less visible. (0.0 - 1.0)
/// "$detailblendmode" "0" -> How to combine the detail material with the albedo.There are 12 different detail blend methods that can be used.
/// "$detailtexturetransform" <matrix> -> center .5 .5 scale 1 1 rotate 0 translate 0 0 -> TODO: get more details
/// --- Normal map ---
/// "$normalmap" "some/path/texturename" -> Normal map texture
/// "$bumpmap" "some/path/texturename" -> Same as normal map texture
/// "$AmbientOcclTexture" "some/path/texturename" -> Ambient occlusion texture
/// "$AmbientOcclColor" "[.4 .4 .4]" -> Ambient occlusion color tint (RGB format)
/// "$AmbientOcclusion" "1" -> Controls strength of Ambient Occlusion. (1 = fully enabled, 0 = fully disabled)
/// --- Height map ---
/// "$heightmap" "some/path/texturename" -> Height map texture
/// --- Specular map ---
/// "$envmapmask" "some/path/texturename" -> Specular texture
/// "$envmaptint" "[1 1 1]" -> Specular color tint(RGB format)
/// "$envmapcontrast" "1" -> Controls the contrast of the reflection. 0 is natural contrast, while 1 is the full squaring of the color (i.e.color* color)
/// "$envmapsaturation" "[1 1 1]" -> Controls the color saturation of the reflection. 0 is greyscale, while 1 is natural saturation.
/// "$basealphaenvmapmask" "1" -> Albedo contains specular map in alpha channel, Alpha channels embedded in $basetexture work in reverse.
/// "$normalmapalphaenvmapmask" "1" -> The normal map contains a specular map in its alpha channel
/// "$normalalphaenvmapmask" "1" -> common typo of the same setting
/// "$envmapmaskintintmasktexture" "1" -> Use the red channel of the $tintmasktexture as the specular mask.
/// --- Emission ---
/// "$selfillum" "1" -> Material is emissive, use albedo texture with blacked out background if "$selfillummask" is not set
/// "$selfillum_envmapmask_alpha" "1" -> replaces the original "$selfillum" command
/// "$selfillumtexture" "some/path/texturename" -> Emission texture
/// "$selfillummask" "some/path/texturename" -> Emission texture
/// "$selfillumtint" "[1 1 1]" -> Emission color tint (RGB format)
/// "$selfillummaskscale" "1" -> Scales the self-illumination effect strength.Default value is 1.0
/// --- Alpha ---
/// "$alpha" "1" -> It scales the opacity of an entire material by the given value. 1 is entirely opaque, 0 is invisible.
/// "$alphatest" "1" -> Alpha clip enable/disable, (bool, 0 / 1)
/// "$alphatestreference" ".5" -> Alpha clip cutoff value, (float, 0 - 1)
/// "$translucent" "1" -> It specifies that the material should be partially see-through. (bool)
/// --- Culling ---
/// "$nocull" "1" ->  It disables backface culling, resulting in triangles showing from both sides.
/// --- Color ---
/// There are two ways of expressing a color tint value
/// "[ <float> <float> <float> ]"    -> going from 0 to 1
/// "{ <int> <int> <int> }"          -> going 0 to 255
/// --- Regrets ---
/// using regions :(
/// </remarks>
/// <see cref="https://developer.valvesoftware.com/wiki/Category:List_of_Shader_Parameters"/>
/// 

// TODO: fails specular creation on securitystation_bits.vmt -> inside normalmap

public abstract class PBSSpecularParser
{
    protected readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>()
    {
        "$basetexture", "$detail", "$normalmap", "$bumpmap", "$heightmap", "$envmapmask, $ambientoccltexture"
    };

    protected readonly HashSet<char> multiValueEncloseCharHashset = new HashSet<char>()
    {
        '{', '}', '[', ']'
    };

    public virtual async Task<PBS_Specular> ParseMaterial(List<KeyValuePair<string, string>> properties, string name)
    {
        await default(ToWorld);
        var currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Material: " + name);
        currentSlot.PositionInFrontOfUser();
        var currentMaterial = currentSlot.CreateMaterialOrb<PBS_Specular>();
        await default(ToBackground);

        Bitmap2D normalmapBitmap = null;

        foreach (KeyValuePair<string, string> currentProperty in properties)
        {
            if (!propertyTextureNamesHashSet.Contains(currentProperty.Key))
            {
                continue;
            }

            // Get texture name and try to grab it from dictionary
            string currentTextureName = currentProperty.Value.Split('/').Last();
            if (!Sledge2Resonite.vtfDictionary.TryGetValue(currentTextureName, out VtfFile currentVtf))
            {
                UniLog.Error($"Texture was not found in dictionary with name {currentTextureName}");
                foreach (var types in Sledge2Resonite.vtfDictionary)
                {
                    UniLog.Log(types.Key + ", " + types.Value);
                }

                continue;
            }

            // VTF contains mip-maps, but we only care about the last original image
            VtfImage currentVtfImage = currentVtf.Images.GetLast();
            var newBitmap = new Bitmap2D(currentVtfImage.GetBgra32Data(), currentVtfImage.Width, currentVtfImage.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false);
            await default(ToWorld);
            StaticTexture2D currentTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
            currentTexture2D.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);

            // Assign texture filtering based on flags in header
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
            await default(ToWorld);
            switch (currentProperty.Key)
            {
                case "$basetexture":
                    currentMaterial.AlbedoTexture.Target = currentTexture2D;
                    break;
                case "$detail":
                    currentMaterial.DetailAlbedoTexture.Target = currentTexture2D;
                    break;
                case "$normalmap":
                case "$bumpmap":
                    // Source engine uses the DirectX standard for normal maps, Resonite uses OpenGL
                    // So we need to invert the green channel
                    // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)
                    normalmapBitmap = newBitmap;
                    // TODO: add green channel invert again
                    if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Ssbump)
                    && (currentTextureName.ToLower().Contains("_bump") || currentTextureName.ToLower().Contains("_ssbump"))
                    && Sledge2Resonite.config.GetValue(Sledge2Resonite.SSBumpAutoConvert))
                    {
                        currentTexture2D.IsNormalMap.Value = true;
                        Utils.SSBumpToNormal(currentTexture2D);
                    }

                    if (Sledge2Resonite.config.GetValue(Sledge2Resonite.invertNormalmapG))
                    {
                        await currentTexture2D.InvertG();
                    }

                    currentTexture2D.IsNormalMap.Value = true;
                    currentMaterial.NormalMap.Target = currentTexture2D;
                    break;
                case "$heightmap":
                    currentMaterial.HeightMap.Target = currentTexture2D;
                    break;
            }
            await default(ToBackground);
        }

        // Convert key value list to dictionary for easier access
        var propertiesDictionary = properties.Distinct().ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

        currentMaterial = await SetAlphaClip(currentMaterial, propertiesDictionary);
        currentMaterial = await SetAlphaBlend(currentMaterial, propertiesDictionary);
        currentMaterial = await CreateSpecularFromSpecularMap(currentMaterial, propertiesDictionary, currentSlot);
        currentMaterial = await CreateSpecularMapFromAlbedoMap(currentMaterial, propertiesDictionary, currentSlot);
        currentMaterial = await CreateSpecularFromNormalMap(currentMaterial, propertiesDictionary, currentSlot, normalmapBitmap);
        currentMaterial = await CreateEmissionMap(currentMaterial, propertiesDictionary, currentSlot);
        currentMaterial = await SetAlbedoColor(currentMaterial, propertiesDictionary);
        currentMaterial = await SetSpecularColor(currentMaterial, propertiesDictionary);
        currentMaterial = await SetSpecularMapTint(currentMaterial, propertiesDictionary, currentSlot);
        currentMaterial = await SetTextureTransforms(currentMaterial, propertiesDictionary);

        return currentMaterial;
    }

    private async Task<PBS_Specular> CreateSpecularMapFromAlbedoMap(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary, Slot currentSlot)
    {
        // Handle specular map in albedo alpha channel
        if (propertiesDictionary.TryGetValue("$basealphaenvmapmask", out string hasAblbedoSpecular) &&
            propertiesDictionary.TryGetValue("$basetexture", out string specularAlbedoInsideAlpha))
        {
            if (hasAblbedoSpecular == "1")
            {
                string currentAlbedoName = specularAlbedoInsideAlpha.Split('/').Last();
                if (Sledge2Resonite.vtfDictionary.TryGetValue(currentAlbedoName, out VtfFile tempAlbedoBitmap2D))
                {
                    // Copy texture and invert alpha channel for specular
                    await default(ToBackground);
                    var albMap = tempAlbedoBitmap2D.Images.GetLast();
                    var albMapRaw = albMap.GetBgra32Data();
                    var newSpecularBitmap = new Bitmap2D(
                        albMapRaw,
                        albMap.Width,
                        albMap.Height,
                        TextureFormat.BGRA32,
                        false, 
                        ColorProfile.Linear, 
                        false);

                    // Wait for the world to catch up
                    await default(ToWorld);
                    StaticTexture2D finalTexture = currentSlot.AttachComponent<StaticTexture2D>();
                    finalTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newSpecularBitmap);
                    currentMaterial.SpecularMap.Target = finalTexture;
                }
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> CreateSpecularFromSpecularMap(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary, Slot currentSlot)
    {
        if (propertiesDictionary.TryGetValue("$envmapmask", out string currentEnvmapmask) &&
                    propertiesDictionary.TryGetValue("$basetexture", out string specularAlbedo))
        {
            string currentEnvmapmaskName = currentEnvmapmask.Split('/').Last();
            string currentAlbedoName = specularAlbedo.Split('/').Last();

            if (Sledge2Resonite.vtfDictionary.TryGetValue(currentEnvmapmaskName, out var tempEnvMapBitmap2D) &&
                Sledge2Resonite.vtfDictionary.TryGetValue(currentAlbedoName, out var tempAlbedoBitmap2D))
            {
                await default(ToBackground);
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
                    envMapModified[x + 1] = albMapModified[x + 1]; // G
                    envMapModified[x + 2] = albMapModified[x + 2]; // R
                }

                // Create new specular bitmap from merging into albedo
                var finalMap = CreateSpecularByAlphaTransfer(
                    new Bitmap2D(albMapModified, albMap.Width, albMap.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false),
                    new Bitmap2D(envMapModified, envMap.Width, envMap.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false));

                // Create new specular texture asset and assign
                await default(ToWorld);
                StaticTexture2D finalTexture = currentSlot.AttachComponent<StaticTexture2D>();
                finalTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(finalMap);
                currentMaterial.SpecularMap.Target = finalTexture;
                await default(ToBackground);
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> CreateSpecularFromNormalMap(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary, Slot currentSlot, Bitmap2D normalmapBitmap)
    {
        // Handle specular texture in normal map
        if (propertiesDictionary.TryGetValue("$normalmapalphaenvmapmask", out string hasNormalmapSpecular) &&
            propertiesDictionary.TryGetValue("$basetexture", out string specularAlbedoForNormalmap))
        {
            if (hasNormalmapSpecular == "1" && normalmapBitmap != null)
            {
                UniLog.Log("got specular in normalmap alpha channel");
                string currentAlbedoName = specularAlbedoForNormalmap.Split('/').Last();
                if (Sledge2Resonite.vtfDictionary.TryGetValue(currentAlbedoName, out var tempAlbedoBitmap2D))
                {
                    await default(ToBackground);

                    // Copy texture and invert alpha channel for specular
                    var albMap = tempAlbedoBitmap2D.Images.GetLast();
                    var albMapRaw = albMap.GetBgra32Data();

                    // Create new specular bitmap from copying the normalmap alpha into the albedo alpha
                    var finalMap = CreateSpecularByAlphaTransfer(new Bitmap2D(albMapRaw, albMap.Width, albMap.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false), normalmapBitmap);

                    // Wait for the world to catch up
                    await default(ToWorld);
                    StaticTexture2D finalTexture = currentSlot.AttachComponent<StaticTexture2D>();
                    finalTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(finalMap);
                    currentMaterial.SpecularMap.Target = finalTexture;
                    await default(ToBackground);
                }
                else
                {
                    UniLog.Error($"Couldn't find albedo texture: {currentAlbedoName}");
                }
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetAlphaBlend(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        if (propertiesDictionary.TryGetValue("$translucent", out string hasAlphaBlend))
        {
            UniLog.Log("alphablend: " + hasAlphaBlend);
            if (hasAlphaBlend == "1")
            {
                await default(ToWorld);
                currentMaterial.BlendMode.Value = BlendMode.Alpha;
                await default(ToBackground);
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetAlphaClip(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        if (propertiesDictionary.TryGetValue("$alphatest", out string hasAlphaClip))
        {
            if (hasAlphaClip == "1")
            {
                await default(ToWorld);
                currentMaterial.BlendMode.Value = BlendMode.Cutout;
                await default(ToBackground);

                if (propertiesDictionary.TryGetValue("$alphatestreference", out string alphaCutOff) &&
                    float.TryParse(alphaCutOff, NumberStyles.Number, CultureInfo.InvariantCulture, out float parsed))
                {
                    await default(ToWorld);
                    currentMaterial.AlphaCutoff.Value = parsed;
                    await default(ToBackground);
                }
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> CreateEmissionMap(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary, Slot currentSlot)
    {
        // Create emission texture 
        if (propertiesDictionary.TryGetValue("$selfillum", out string currentSelfIllum) &&
            propertiesDictionary.TryGetValue("$basetexture", out string emissionAlbedo))
        {
            string emissonAlbedoName = emissionAlbedo.Split('/').Last();
            if (currentSelfIllum == "1" &&
                Sledge2Resonite.vtfDictionary.TryGetValue(emissonAlbedoName, out var tempAlbedoBitmap2D))
            {
                await default(ToBackground);

                // Add new emission texture to world and tint background black
                var albMap = tempAlbedoBitmap2D.Images.GetLast();
                var albMapRaw = albMap.GetBgra32Data();
                var newEmission = new Bitmap2D(albMapRaw, albMap.Width, albMap.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false);

                // Assign to material
                await default(ToWorld);
                StaticTexture2D emissionTexture = currentSlot.AttachComponent<StaticTexture2D>();
                emissionTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newEmission);
                await emissionTexture.ProcessPixels(c => color.AlphaBlend(c, color.Black));
                currentMaterial.EmissiveMap.Target = emissionTexture;
                await default(ToBackground);

                // set emission color
                await SetEmissionColor(currentMaterial, propertiesDictionary);
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetAlbedoColor(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        if (propertiesDictionary.TryGetValue("$color", out string currentAlbedoTint))
        {
            if (Float3Extensions.GetFloat3FromString(currentAlbedoTint, out float3 val))
            {
                await default(ToWorld);
                currentMaterial.AlbedoColor.Value = new colorX(new float4(val.x, val.y, val.z, 1));
                await default(ToBackground);
            }
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetEmissionColor(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        if (propertiesDictionary.TryGetValue("$selfillumtint", out string currentSelfIllumTint))
        {
            UniLog.Log("emission color tint: " + currentSelfIllumTint);
            if (Float3Extensions.GetFloat3FromString(currentSelfIllumTint, out float3 val))
            {
                await default(ToWorld);
                currentMaterial.EmissiveColor.Value = new colorX(new float4(val.x, val.y, val.z, 1));
                await default(ToBackground);
            }
            else
            {
                UniLog.Error("Failed to parse float3 with " + val);
            }
        }
        else
        {
            await default(ToWorld);
            currentMaterial.EmissiveColor.Value = new colorX(new float4(1, 1, 1, 1));
            await default(ToBackground);
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetSpecularColor(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        if (propertiesDictionary.TryGetValue("$envmaptint", out string currentSpecularTint))
        {
            UniLog.Log("Specular color tint: " + currentSpecularTint);
            if (Float3Extensions.GetFloat3FromString(currentSpecularTint, out float3 tint))
            {
                await default(ToWorld);
                currentMaterial.SpecularColor.Value = new colorX(new float4(tint.x, tint.y, tint.z, 1));
                await default(ToBackground);
            }
            else
            {
                UniLog.Error("Failed to parse float3 with " + tint);
            }
        }

        return currentMaterial;
    }

    // TODO Test
    private async Task<PBS_Specular> SetSpecularMapTint(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary, Slot currentSlot)
    {
        if (!Sledge2Resonite.config.GetValue(Sledge2Resonite.tintSpecular))
        {
            return currentMaterial;
        }

        colorX tint = colorX.White;
        if (propertiesDictionary.TryGetValue("$envmaptint", out string currentSpecularTint))
        {
            UniLog.Log("Specular color tint: " + currentSpecularTint);
            if (Float3Extensions.GetFloat3FromString(currentSpecularTint, out float3 hue))
            {
                tint = new colorX(new float4(hue.x, hue.y, hue.z, 1));
            }
            else if (currentMaterial.SpecularColor.Value != colorX.White)
            {
                tint = currentMaterial.SpecularColor.Value;
            }
            else
            {
                UniLog.Error("Failed to parse float3 with " + tint);
                return currentMaterial;
            }
        }
        else if (Sledge2Resonite.vtfDictionary.TryGetValue(currentSpecularTint, out var currentSpecularMapTexture))
        {
            // Copy texture and invert alpha channel for specular
            var specMap = currentSpecularMapTexture.Images.GetLast();
            var specMapRaw = specMap.GetBgra32Data();

            // Create new specular bitmap from copying the normalmap alpha into the albedo alpha
            var finalMap = new Bitmap2D(specMapRaw, specMap.Width, specMap.Height, TextureFormat.BGRA32, false, ColorProfile.Linear, false);

            // Wait for the world to catch up
            await default(ToWorld);
            StaticTexture2D finalTexture = currentSlot.AttachComponent<StaticTexture2D>();
            await default(ToBackground);

            await finalTexture.ProcessPixels((color color) => (color)color * (color)tint);

            await default(ToWorld);
            finalTexture.URL.Value = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(finalMap);
            currentMaterial.SpecularMap.Target = finalTexture;
            await default(ToBackground);
        }
        else
        {
            UniLog.Error("Failed to parse float3 with " + tint);
        }

        return currentMaterial;
    }

    private async Task<PBS_Specular> SetTextureTransforms(PBS_Specular currentMaterial, Dictionary<string, string> propertiesDictionary)
    {
        // Apply texture transforms
        if (propertiesDictionary.TryGetValue("$detailscale", out string currentDetailScale))
        {
            await default(ToWorld);

            if (multiValueEncloseCharHashset.Any(currentDetailScale.Contains))
            {
                if (Float2Extensions.GetFloat2FromString(currentDetailScale, out float2 canidate))
                {
                    currentMaterial.DetailTextureScale.Value = canidate;
                }
            }
            else
            {
                if (float.TryParse(currentDetailScale, NumberStyles.Number, CultureInfo.InvariantCulture, out float parsed))
                {
                    currentMaterial.DetailTextureScale.Value = new float2(parsed, parsed);
                }
                else
                {
                    UniLog.Error("Failed to parse value with " + currentDetailScale);
                }
            }

            await default(ToBackground);
        }

        return currentMaterial;
    }

    private Bitmap2D CreateSpecularByAlphaTransfer(Bitmap2D albedo, Bitmap2D donor)
    {
        // Check if they donor bitmaps even contain transparent pixels
        if (!donor.HasTransparentPixels())
        {
            // UniLog.Error("no transparent pixels");
            return albedo;
        }

        // Rescale donor bitmap to match albedo bitmap
        if (albedo.Size != donor.Size)
        {
            // UniLog.Log("Texture size mismatch, rescaling");
            donor = donor.GetRescaled(albedo.Size, false, false, Filtering.Lanczos3);
        }

        // Assign new pixel values to albedo alpha channel
        for (int x = 0; x < albedo.Size.x; x++)
        {
            for (int y = 0; y < albedo.Size.y; y++)
            {
                var originalPixel = albedo.GetPixel(x, y);
                var donorPixel = donor.GetPixel(x, y);
                albedo.SetPixel(x, y, new color(originalPixel.r, originalPixel.g, originalPixel.b, donorPixel.a));
            }
        }

        return albedo;
    }

}