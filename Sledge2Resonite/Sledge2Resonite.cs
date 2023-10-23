﻿using Elements.Core;
using Elements.Assets;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using Sledge.Formats.Valve;
using Sledge.Formats.Texture.Vtf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using File = System.IO.File;
using FrooxEngine.Undo;
using TextureFormat = Elements.Assets.TextureFormat;

namespace Sledge2Resonite
{
    public class Sledge2Resonite : ResoniteMod
    {
        public override string Name => "Sledge2Resonite";
        public override string Author => "Elektrospy";
        public override string Version => "0.1.0";
        public override string Link => "https://github.com/Elektrospy/Sledge2Resonite";

        internal static ModConfiguration config;

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(0, 1, 0))
                .AutoSave(true);
        }

        #region ModConfigurationKeys

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<int> importTextureRow = new("textureRows", "Import Textures number of rows", () => 5);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> tintSpecular = new("tintSpecular", "Tint Specular Textures on Import", () => false);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> generateTextureAtlas = new("Generate frame atlas", "Auto generate atlas of multiframe textures", () => true);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> SSBumpAutoConvert = new("SSBump auto convert", "Auto convert SSBump to NormalMap", () => true);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> invertNormalmapG = new("Invert normal map G ", "Invert the green color channel of normal maps", () => true);

        #endregion

        internal static Dictionary<string, VtfFile> vtfDictionary = new Dictionary<string, VtfFile>();
        internal static Dictionary<string, SerialisedObject> vmtDictionary = new Dictionary<string, SerialisedObject>();
        private static readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>()
        {
            "$basetexture", "$detail", "$normalmap", "$bumpmap", "$heightmap", "$envmapmask", "$selfillumtexture", "$selfillummask"
        };

        public override void OnEngineInit()
        {
            new Harmony("net.Elektrospy.Sledge2Resonite").PatchAll();
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => AssetPatch());
        }

        private static void AssetPatch()
        {
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Special].Add("vtf");
            aExt.Value[AssetClass.Special].Add("vmt");
            aExt.Value[AssetClass.Special].Add("raw");
        }

        [HarmonyPatch(typeof(UniversalImporter), "ImportTask", typeof(AssetClass), typeof(IEnumerable<string>), typeof(World), typeof(float3), typeof(floatQ), typeof(float3), typeof(bool))]
        public static class UniversalImporterPatch
        {
            static bool Prefix(ref IEnumerable<string> files, ref Task __result, World world)
            {
                var query = files.Where(x =>
                    x.EndsWith("vtf", StringComparison.InvariantCultureIgnoreCase) ||
                    x.EndsWith("vmt", StringComparison.InvariantCultureIgnoreCase) ||
                    x.EndsWith("raw", StringComparison.InvariantCultureIgnoreCase));

                if (query.Any())
                {
                    Msg("Importing sledge asset");
                    __result = ProcessSledgeImport(query, world);
                }

                return true;
            }
        }

        private static async Task ProcessSledgeImport(IEnumerable<string> inputFiles, World world)
        {
            await default(ToBackground);
            await ParseInputFiles(inputFiles, world, true);
            ClearDictionaries();
            await default(ToWorld);
        }

        private static async Task ParseInputFiles(IEnumerable<string> inputFiles, World world, bool createQuads = false)
        {
            SerialisedObjectFormatter ValveSerialiser = new SerialisedObjectFormatter();
            string[] filesArr = inputFiles.ToArray();
            int vtfCounter = 0;
            int vmtCounter = 0;

            for (int i = 0; i < filesArr.Count(); ++i)
            {
                if (!File.Exists(filesArr[i])) continue;

                FileInfo currentFileInfo;
                FileStream fs;
                try
                {
                    currentFileInfo = new FileInfo(filesArr[i]);
                    fs = File.Open(filesArr[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (Exception ex)
                {
                    Error($"Got an exception while opening the file: {filesArr[i]} {ex.Message}");
                    continue;
                }

                string currentFileName = currentFileInfo.Name.Split('.').First();
                string currentFileEnding = currentFileInfo.Extension;
                switch (currentFileEnding)
                {
                    case ".vtf":
                        VtfFile tempVtf = new VtfFile(fs);
                        try
                        {
                            if (vtfDictionary.ContainsKey(currentFileName))
                                tempVtf = vtfDictionary[currentFileName];
                            else
                                vtfDictionary.Add(currentFileName, tempVtf);
                        }
                        catch (Exception e)
                        {
                            Error($"Couldn't add vtf {currentFileName}, error: {e.Message}");
                        }

                        if (!createQuads) return;

                        // Add undoable slot to world, so we can append components
                        Slot currentSlot;
                        await default(ToWorld);
                        try
                        {
                            currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Texture: " + currentFileName);
                            currentSlot.CreateSpawnUndoPoint();
                        }
                        catch (Exception e)
                        {
                            Error($"Couldn't add Slot \"Texture: {currentFileName}\" to world, error: {e.Message}");
                            continue;
                        }

                        // Check if this is the first time
                        if (vtfCounter == 0)
                        {
                            currentSlot.PositionInFrontOfUser();
                        }
                        else
                        {
                            currentSlot.PositionInFrontOfUser();
                            float3 offset = UniversalImporter.GridOffset(ref vtfCounter, config.GetValue(importTextureRow));
                            currentSlot.GlobalPosition += offset;
                            vtfCounter++;
                        }
                        await CreateTextureQuadFromVtf(currentFileName, tempVtf, currentSlot);
                        break;
                    case ".vmt":
                        VMTPreprocessor vmtPrePros = new VMTPreprocessor();
                        string fileLines;
                        try
                        {
                            fileLines = File.ReadAllText(filesArr[i]);
                        }
                        catch (Exception e)
                        {
                            Error($"File read all lines error: {e.Message}");
                            continue;
                        }

                        // Clean up the vmt, before handing it over to format library for parsing
                        vmtPrePros.ParseVmt(fileLines, out fileLines);
                        List<SerialisedObject> tempSerialzeObjectList = new();
                        using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileLines)))
                        {
                            try
                            {
                                tempSerialzeObjectList = ValveSerialiser.Deserialize(memoryStream).ToList();
                            }
                            catch (Exception e)
                            {
                                Error($"valve deserialize error: {e.Message}");
                                continue;
                            }
                        }

                        var firstVmtObject = tempSerialzeObjectList.First();

                        if (!vmtDictionary.ContainsKey(firstVmtObject.Name))
                            vmtDictionary.Add(currentFileName, firstVmtObject);

                        // PropertyTextureNamesHashSet -> hashset to compare against
                        foreach (KeyValuePair<string, string> currentProperty in firstVmtObject.Properties)
                        {
                            if (propertyTextureNamesHashSet.Contains(currentProperty.Key))
                            {
                                string tempTexturePath = Utils.MergeTextureNameAndPath(currentProperty.Value, filesArr[i]);

                                if (!string.IsNullOrEmpty(tempTexturePath))
                                    await ParseInputFiles(new string[] { tempTexturePath }, world);
                                else
                                    Error("Texture path is empty or null!");
                            }
                        }

                        // Try to create material orb from dictionary
                        if (vmtDictionary.ContainsKey(currentFileName))
                            await CreateMaterialOrbFromVmt(currentFileName, firstVmtObject);
                        else
                            Error($"Couldn't find {currentFileName} in vmt dictionary");

                        vmtCounter++;
                        break;

                    case ".raw":
                        // Add undoable slot to world, so we can append components
                        Slot lutSlot;
                        await default(ToWorld);
                        try
                        {
                            lutSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("LUT: " + currentFileName);
                            lutSlot.CreateSpawnUndoPoint();
                        }
                        catch (Exception ex)
                        {
                            Error($"Couldn't add Slot \"LUT: {currentFileName}\" to world, error: {ex.Message}");
                            continue;
                        }

                        // Check if this is the first time
                        if (vtfCounter == 0)
                        {
                            lutSlot.PositionInFrontOfUser();
                        }
                        else
                        {
                            lutSlot.PositionInFrontOfUser();
                            float3 offset = UniversalImporter.GridOffset(ref vtfCounter, config.GetValue(importTextureRow));
                            lutSlot.GlobalPosition += offset;
                            vtfCounter++;
                        }
                        await NewLUTImport(filesArr[i], lutSlot);
                        break;
                    default:
                        Error($"Unknown file ending: {currentFileEnding}");
                        break;
                }

                if (fs != null) fs.Dispose();
            }

            await default(ToWorld);
            await default(ToBackground);
        }

        private static async Task CreateMaterialOrbFromVmt(string currentVmtName, SerialisedObject currentSerialisedObject)
        {
            if (string.IsNullOrEmpty(currentVmtName))
            {
                Error("Vmt name is empty!");
                return;
            }

            Msg($"Current material shader: {currentSerialisedObject.Name}");
            // TODO: add more material shader parsers

            await default(ToBackground);
            VertexLitGenericParser vertexLitGenericParser = new VertexLitGenericParser();
            await vertexLitGenericParser.ParseMaterial(currentSerialisedObject.Properties, currentVmtName);
        }

        private static async Task CreateTextureQuadFromVtf(string currentVtfName, VtfFile currentVtf, Slot currentSlot)
        {
            
            await default(ToWorld);
            currentSlot.PositionInFrontOfUser();
            VtfImage currentVtfImage = currentVtf.Images.GetLast();

            var newBitmap = new Bitmap2D(
                currentVtfImage.GetBgra32Data(),
                currentVtfImage.Width,
                currentVtfImage.Height,
                TextureFormat.BGRA32,
                false,
                ColorProfile.Linear,
                false);

            if (config.GetValue(Sledge2Resonite.generateTextureAtlas))
            {
                var imageList = currentVtf.Images;
                var mipmapNumber = currentVtf.Header.MipmapCount;
                var framesNumberRaw = imageList.Count;
                var framesNumber = framesNumberRaw / mipmapNumber;
                var bytesNumber = currentVtfImage.Width * currentVtfImage.Height * 4 * framesNumber;
                byte[] fillBytes = new byte[bytesNumber];

                Msg($"Generate image atlas with {framesNumber} frames");
                var newAtlasBitmap = new Bitmap2D(
                    fillBytes,
                    currentVtfImage.Width * framesNumber,
                    currentVtfImage.Height,
                    TextureFormat.BGRA32,
                    false,
                    ColorProfile.Linear,
                    false);
                Msg($"Generated atlas bitmap with Width: {newAtlasBitmap.Size.x} and Height: {newAtlasBitmap.Size.y}");

                Msg($"Parse frames ...");
                var frameIndexStartOffset = imageList.Count - framesNumber;
                for (int currentFrame = frameIndexStartOffset; currentFrame < framesNumberRaw; currentFrame++)
                {
                    try
                    {
                        int currentOutputFrameIndex = currentFrame - frameIndexStartOffset;
                        var currentFrameImage = imageList[currentFrame];

                        var currentFrameBitmap = new Bitmap2D(
                            currentFrameImage.GetBgra32Data(),
                            currentFrameImage.Width,
                            currentFrameImage.Height,
                            TextureFormat.BGRA32,
                            false,
                            ColorProfile.Linear,
                            false);

                        for (int currentX = 0; currentX < currentFrameBitmap.Size.x; currentX++)
                        {
                            var pixelOffsetX = currentVtfImage.Width * currentOutputFrameIndex;
                            for (int currentY = 0; currentY < currentFrameBitmap.Size.y; currentY++)
                            {
                                var rawPixelColor = currentFrameBitmap.GetPixel(currentX, currentY);
                                newAtlasBitmap.SetPixel(currentX + pixelOffsetX, currentY, in rawPixelColor);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Error("Oh no, something bad happened, skipping to next frame: " + e.ToString());
                    }
                }
                Msg($"... Frames parsing done");
                newBitmap = newAtlasBitmap;
            }


            var currentUri = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);
            StaticTexture2D currentTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
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

            // Check if normalmap flag is set and the name contains "_normal"
            // Come textures contain "flawed" flags within the header
            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Normal) 
            && (currentVtfName.ToLower().Contains("_normal") || currentVtfName.ToLower().Contains("_bump")))
            {
                currentTexture2D.IsNormalMap.Value = true;
                // Source engine uses the DirectX standard for normal maps, NeosVR uses OpenGL
                // so we need to invert the green channel
                // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)
                currentTexture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
            }

            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Ssbump) 
            && (currentVtfName.ToLower().Contains("_bump") || currentVtfName.ToLower().Contains("_ssbump")) 
            && config.GetValue(Sledge2Resonite.SSBumpAutoConvert))
            {
                currentTexture2D.IsNormalMap.Value = true;
                Utils.SSBumpToNormal(currentTexture2D);
            }

            ImageImporter.SetupTextureProxyComponents(
                currentSlot,
                currentTexture2D,
                StereoLayout.None,
                ImageProjection.Perspective,
                false);
            ImageImporter.CreateQuad(
                currentSlot,
                currentTexture2D,
                StereoLayout.None,
                true);
            currentSlot.AttachComponent<Grabbable>().Scalable.Value = true;
        }

        private static async Task NewLUTImport(string path, Slot targetSlot)
        {
            await new ToBackground();
            const int sourceLUTWidth = 32;
            const int sourceLUTHeight = 1024;

            Bitmap2D rawBitmap2D = null;
            try
            {
                var rawBytes = File.ReadAllBytes(path);
                rawBitmap2D = new Bitmap2D(rawBytes, sourceLUTWidth, sourceLUTHeight, TextureFormat.RGB24, false, ColorProfile.Linear, false);
            }
            catch (Exception e)
            {
                Error($"ReadAllBytes failed: {e.Message}");
            }

            if (rawBitmap2D.Size.x != sourceLUTWidth || rawBitmap2D.Size.y != sourceLUTHeight)
            {
                Error($"Bitmap2D with wrong resolution! width:{rawBitmap2D.Size.x}, height:{rawBitmap2D.Size.y}");
                return;
            }

            Debug("Bitmap2D has a valid resolution");

            const int pixelBoxSideLength = 32;
            var texture = new Bitmap3D(pixelBoxSideLength, pixelBoxSideLength, pixelBoxSideLength, TextureFormat.RGB24, false, ColorProfile.Linear);

            // This is dark got-dayum magic. Elektro somehow got this to work, I don't know how many hours we threw at this.
            // Converting our .raws to .pngs alone was a royal pain in the ass. Good riddance -dfg
            // Going x axis from 0 to 31 + current offset (block index)
            // Going y axis from 0 to 31 + offset (0 to 1023 in steps of 32)
            for (int currentBlockIndex = 0; currentBlockIndex < pixelBoxSideLength; currentBlockIndex++)
            {
                int rawYOffset = currentBlockIndex * pixelBoxSideLength;
                for (int rawY = 0; rawY < pixelBoxSideLength; rawY++)
                {
                    for (int rawX = 0; rawX < pixelBoxSideLength; rawX++)
                    {
                        color rawPixelColor = rawBitmap2D.GetPixel(rawX, rawY + rawYOffset);
                        texture.SetPixel(rawX, rawY, currentBlockIndex, in rawPixelColor);
                    }
                }
            }

            // TODO: Remove StaticTexture2D when done testing
            Uri uriTexture2D = await targetSlot.Engine.LocalDB.SaveAssetAsync(rawBitmap2D).ConfigureAwait(false);
            Uri uriTexture3D = await targetSlot.Engine.LocalDB.SaveAssetAsync(texture).ConfigureAwait(false);

            await new ToWorld();

            var lutTexRaw = targetSlot.AttachComponent<StaticTexture2D>();
            lutTexRaw.URL.Value = uriTexture2D;
            lutTexRaw.FilterMode.Value = TextureFilterMode.Point;

            var lutTex = targetSlot.AttachComponent<StaticTexture3D>();
            lutTex.URL.Value = uriTexture3D;
            lutTex.FilterMode.Value = TextureFilterMode.Point;

            var lutMat = targetSlot.AttachComponent<LUT_Material>();
            lutMat.LUT.Target = lutTex;

            MaterialOrb.ConstructMaterialOrb(lutMat, targetSlot);
        }

        private static void ClearDictionaries()
        {
            vmtDictionary.Clear();
            vtfDictionary.Clear();
        }

        private static async Task UpdateProgressAsync(IProgressIndicator indicator, string progressInfo, string detailInfo)
        {
            await default(ToWorld);
            indicator.UpdateProgress(0f, progressInfo, detailInfo);
            await default(ToBackground);
        }
    }
}
