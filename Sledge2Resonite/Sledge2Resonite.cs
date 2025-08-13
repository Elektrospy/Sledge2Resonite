using Elements.Core;
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
        public override string Version => "0.2.1";
        public override string Link => "https://github.com/Elektrospy/Sledge2Resonite";

        internal static ModConfiguration config;

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(0, 2, 1))
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

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> lutAsLinear = new("Import lUT as linear ", "Import *.raw LUTs with a linear color profile", () => true);

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

        static void AssetPatch()
        {
            /*
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Special].Add("vtf");
            aExt.Value[AssetClass.Special].Add("vmt");
            aExt.Value[AssetClass.Special].Add("raw");
            */
            // Revised implementation using reflection to handle API changes, *borrowed* from xLinka :)

            try
            {
                Debug("Attempting to add vtf, vmt and raw support to import system");

                // Get ImportExtension type via reflection since it's now a struct inside AssetHelper
                var assHelperType = typeof(AssetHelper);
                var importExtType = assHelperType.GetNestedType("ImportExtension", System.Reflection.BindingFlags.NonPublic);

                if (importExtType == null)
                {
                    Error("ImportExtension type not found. This mod is toast.");
                    return;
                }

                // Create ImportExtension instances with reflection
                // Constructor args: (string ext, bool autoImport)
                var importExtVTF = System.Activator.CreateInstance(importExtType, new object[] { "vtf", true });
                var importExtVMT = System.Activator.CreateInstance(importExtType, new object[] { "vmt", true });
                var importExtRAW = System.Activator.CreateInstance(importExtType, new object[] { "raw", true });

                // Get the associatedExtensions field via reflection
                var extensionsField = assHelperType.GetField("associatedExtensions",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (extensionsField == null)
                {
                    Error("Could not find associatedExtensions field");
                    return;
                }

                // Get the dictionary and add our extension to the Special asset class
                var extensions = extensionsField.GetValue(null);
                var dictType = extensions.GetType();
                var specialValue = dictType.GetMethod("get_Item").Invoke(extensions, new object[] { AssetClass.Special });

                if (specialValue == null)
                {
                    Error("Couldn't get Special asset class list");
                    return;
                }

                // Add our ImportExtension to the list
                specialValue.GetType().GetMethod("Add").Invoke(specialValue, new[] { importExtVTF });
                specialValue.GetType().GetMethod("Add").Invoke(specialValue, new[] { importExtVMT });
                specialValue.GetType().GetMethod("Add").Invoke(specialValue, new[] { importExtRAW });

                Debug("vtf, vmt and raw import extensions added successfully");
            }
            catch (System.Exception ex)
            {
                Error($"Failed to add vtf, vmt and raw to special import formats: {ex}");
            }
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

        static async Task ProcessSledgeImport(IEnumerable<string> inputFiles, World world)
        {
            await default(ToBackground);
            await ParseInputFiles(inputFiles, world, true);
            ClearDictionaries();
            await default(ToWorld);
        }

        static async Task ParseInputFiles(IEnumerable<string> inputFiles, World world, bool createQuads = true)
        {
            Msg($"ParseInputFiles");
            SerialisedObjectFormatter ValveSerialiser = new SerialisedObjectFormatter();
            string[] filesArr = inputFiles.ToArray();
            int vtfCounter = 0;
            int vmtCounter = 0;

            for (int i = 0; i < filesArr.Count(); ++i)
            {
                Debug($"Does {filesArr[i]} exist?");
                if (!File.Exists(filesArr[i])) continue;
                Debug($"Yes it does!");

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

                string currentFileName = currentFileInfo.Name.Substring(0, currentFileInfo.Name.Length - 4);
                string currentFileEnding = currentFileInfo.Extension;
                switch (currentFileEnding)
                {
                    case ".vtf":
                        VtfFile tempVtf = new(fs);
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
                            Error($"Couldn't add Slot \"Texture: {currentFileName}\" to world, error: {e}");
                            continue;
                        }

                        // Check if this is the first time
                        currentSlot.PositionInFrontOfUser();
                        if (vtfCounter != 0)
                        {
                            float3 offset = UniversalImporter.GridOffset(ref vtfCounter, config.GetValue(importTextureRow));
                            currentSlot.GlobalPosition += offset;
                        }
                        vtfCounter++;
                        Msg($"Call CreateTextureQuadFromVtf");
                        try
                        {
                            await CreateTextureQuadFromVtf(currentFileName, tempVtf, currentSlot);
                        }
                        catch (Exception e)
                        {
                            Error($"Whoooops, you need to put the CD into your computer: {e}");
                            continue;
                        }
                        Msg($"CreateTextureQuadFromVtf was called!");
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
                                    await ParseInputFiles(new string[] { tempTexturePath }, world, false);
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
                        lutSlot.PositionInFrontOfUser();
                        if (vtfCounter != 0)
                        {
                            float3 offset = UniversalImporter.GridOffset(ref vtfCounter, config.GetValue(importTextureRow));
                            lutSlot.GlobalPosition += offset;
                        }
                        vtfCounter++;
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

        static async Task CreateMaterialOrbFromVmt(string currentVmtName, SerialisedObject currentSerialisedObject)
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

        static async Task CreateTextureQuadFromVtf(string currentVtfName, VtfFile currentVtf, Slot currentSlot)
        {
            Debug($"CreateTextureQuadFromVtf");
            await default(ToWorld);

            // Cache configuration values
            bool shouldGenerateAtlas = config.GetValue(Sledge2Resonite.generateTextureAtlas);
            bool shouldInvertNormalG = config.GetValue(Sledge2Resonite.invertNormalmapG);
            bool shouldAutoConvertSSBump = config.GetValue(Sledge2Resonite.SSBumpAutoConvert);
            string lowerVtfName = currentVtfName.ToLower();
            var mipmapNumber = currentVtf.Images.Select(x => x.Mipmap).Distinct().Count();
            var framesNumberRaw = currentVtf.Images.Count();
            var framesNumber = framesNumberRaw / mipmapNumber;

            Debug($"Processing VTF with {currentVtf.Images.Count} total images (including mipmaps)");


            Bitmap2D newBitmap;
            if (shouldGenerateAtlas && framesNumber > 1)
            {
                newBitmap = await CreateAtlasBitmapWithMipmaps(currentVtf, currentVtfName);
            }
            else
            {
                newBitmap = CreateSingleTextureWithMipmaps(currentVtf);
            }

            // Save and setup texture
            Msg($"Saving asset {currentVtfName} to local database");
            var currentUri = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);

            Msg($"Attach component 'StaticTexture2D' to {currentSlot.Name}");
            StaticTexture2D currentTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
            currentTexture2D.URL.Value = currentUri;

            // Configure texture filtering
            ConfigureTextureFiltering(currentTexture2D, currentVtf);

            // Handle normal maps
            await ProcessNormalMaps(currentTexture2D, currentVtf, lowerVtfName, shouldInvertNormalG);

            // Handle SSBump conversion
            ProcessSSBump(currentTexture2D, currentVtf, lowerVtfName, shouldAutoConvertSSBump);

            // Setup final components
            SetupFinalComponents(currentSlot, currentTexture2D);
        }

        static Bitmap2D CreateSingleTextureWithMipmaps(VtfFile vtfFile)
        {
            Debug($"Creating single texture with mipmaps");

            // Get the base mip level (largest image) for the first frame
            VtfImage baseImage = vtfFile.Images.Last();
            byte[] imageData = baseImage.GetBgra32Data();

            Debug($"Using base mip level: {baseImage.Width}x{baseImage.Height}");

            // Create bitmap with mipmap generation enabled
            return new Bitmap2D(
                imageData,
                baseImage.Width,
                baseImage.Height,
                TextureFormat.BGRA32,
                false, // Generate mipmaps automatically
                ColorProfile.sRGB,
                false, // Don't flip Y
                "VTF" // Original format identifier
            );
        }

        static async Task<Bitmap2D> CreateAtlasBitmapWithMipmaps(VtfFile vtfFile, string vtfName)
        {
            Debug($"Creating atlas bitmap with mipmaps");

            // Calculate mipmap count by analyzing the image collection
            // Since MipmapCount is no longer public, we need to determine it ourselves
            int mipmapLevels = vtfFile.Images.Select(x => x.Mipmap).Distinct().Count();
            int totalImages = vtfFile.Images.Count;
            int frameCount = totalImages / Math.Max(1, mipmapLevels);

            Debug($"Atlas info: {frameCount} frames, {mipmapLevels} mipmap levels each (calculated)");

            if (frameCount <= 1 || mipmapLevels <= 1)
            {
                return CreateSingleTextureWithMipmaps(vtfFile);
            }

            // Get base image dimensions from the largest image
            VtfImage baseImage = vtfFile.Images.Last();
            int baseWidth = baseImage.Width;
            int baseHeight = baseImage.Height;

            Debug($"Base image dimensions: {baseWidth}x{baseHeight}");

            // Create atlas using the highest quality images
            const int colorChannels = 4;
            int atlasWidth = baseWidth * frameCount;
            int atlasSize = atlasWidth * baseHeight * colorChannels;
            byte[] atlasData = new byte[atlasSize];

            // Copy each frame's highest quality image to the atlas
            await CopyFramesToAtlas(vtfFile, frameCount, mipmapLevels, baseWidth, baseHeight, atlasData);

            // Create the final atlas bitmap using the highest quality images
            return new Bitmap2D(
                atlasData,
                atlasWidth,
                baseHeight,
                TextureFormat.BGRA32,
                false, // Let Resonite generate mipmaps from our high-quality atlas
                ColorProfile.sRGB,
                false, // Don't flip Y
                "VTF_Atlas" // Original format identifier
            );
        }

        static async Task CopyFramesToAtlas(VtfFile vtfFile, int frameCount, int mipmapLevels,
            int frameWidth, int frameHeight, byte[] atlasData)
        {
            unsafe
            {
                fixed (byte* atlasPtr = atlasData)
                {
                    // Find all images that match our base resolution (these are the highest quality frames)
                    var baseImages = vtfFile.Images
                        .Where(img => img.Width == frameWidth && img.Height == frameHeight)
                        .Take(frameCount)
                        .ToList();

                    Debug($"Found {baseImages.Count} base resolution images for atlas");

                    for (int frame = 0; frame < Math.Min(frameCount, baseImages.Count); frame++)
                    {
                        try
                        {
                            VtfImage frameImage = baseImages[frame];
                            byte[] frameData = frameImage.GetBgra32Data();

                            fixed (byte* framePtr = frameData)
                            {
                                int frameOffsetBytes = frame * frameWidth * 4;
                                int atlasStride = frameWidth * frameCount * 4;
                                int frameStride = frameWidth * 4;

                                // Copy row by row
                                for (int y = 0; y < frameHeight; y++)
                                {
                                    byte* srcRow = framePtr + (y * frameStride);
                                    byte* dstRow = atlasPtr + (y * atlasStride) + frameOffsetBytes;

                                    Buffer.MemoryCopy(srcRow, dstRow, frameStride, frameStride);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Error($"Error copying frame {frame} to atlas: {e}");
                        }
                    }
                }
            }
        }

        static void ConfigureTextureFiltering(StaticTexture2D texture2D, VtfFile vtfFile)
        {
            if (vtfFile.Header.Flags.HasFlag(VtfImageFlag.PointSample))
            {
                texture2D.FilterMode.Value = TextureFilterMode.Point;
            }
            else if (vtfFile.Header.Flags.HasFlag(VtfImageFlag.Trilinear))
            {
                texture2D.FilterMode.Value = TextureFilterMode.Trilinear;
            }
            else
            {
                texture2D.FilterMode.Value = TextureFilterMode.Anisotropic;
                texture2D.AnisotropicLevel.Value = 8;
            }
        }

        static async Task ProcessNormalMaps(StaticTexture2D texture2D, VtfFile vtfFile,
            string lowerVtfName, bool shouldInvertNormalG)
        {
            if (vtfFile.Header.Flags.HasFlag(VtfImageFlag.Normal)
                && (lowerVtfName.Contains("_normal") || lowerVtfName.Contains("_bump")))
            {
                texture2D.IsNormalMap.Value = true;

                if (shouldInvertNormalG)
                {
                    await texture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
                }
            }
        }

        static void ProcessSSBump(StaticTexture2D texture2D, VtfFile vtfFile,
            string lowerVtfName, bool shouldAutoConvertSSBump)
        {
            if (shouldAutoConvertSSBump
                && vtfFile.Header.Flags.HasFlag(VtfImageFlag.SsBump)
                && (lowerVtfName.Contains("_ssbump") || lowerVtfName.Contains("_bump")))
            {
                texture2D.IsNormalMap.Value = true;
                Utils.SSBumpToNormal(texture2D);
            }
        }

        static void SetupFinalComponents(Slot slot, StaticTexture2D texture2D)
        {
            ImageImporter.SetupTextureProxyComponents(
                slot,
                texture2D,
                StereoLayout.None,
                ImageProjection.Perspective,
                false
            );

            ImageImporter.CreateQuad(
                slot,
                texture2D,
                StereoLayout.None,
                true
            );

            slot.AttachComponent<Grabbable>().Scalable.Value = true;
        }

        static async Task NewLUTImport(string path, Slot targetSlot)
        {
            await new ToBackground();
            const int sourceLUTWidth = 32;
            const int sourceLUTHeight = 1024;

            Bitmap2D rawBitmap2D = null;
            try
            {
                byte[] rawBytes = File.ReadAllBytes(path);
                Debug($"read *.raw byte size: {rawBytes.Length} of {sourceLUTHeight * sourceLUTWidth}");
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
            Bitmap3D texture = new Bitmap3D(pixelBoxSideLength, pixelBoxSideLength, pixelBoxSideLength, TextureFormat.RGB24, false, ColorProfile.Linear);

            // This is dark got-dayum magic. Elektro somehow got this to work, I don't know how many hours we threw at this.
            // Converting our .raws to .pngs alone was a royal pain in the ass. Good riddance -dfg
            // Going x axis from 0 to 31 + current offset (block index)
            // Going y axis from 0 to 31 + offset (0 to 1023 in steps of 32)
            for (int currentBlockIndex = 0; currentBlockIndex < pixelBoxSideLength; currentBlockIndex++)
            {
                for (int rawY = 0; rawY < pixelBoxSideLength; rawY++)
                {
                    for (int rawX = 0; rawX < pixelBoxSideLength; rawX++)
                    {
                        color rawPixelColor = rawBitmap2D.GetPixel(rawX, rawY + (currentBlockIndex * pixelBoxSideLength));
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
            lutTex.FilterMode.Value = TextureFilterMode.Anisotropic;
            lutTex.AnisotropicLevel.Value = 16;
            lutTex.DirectLoad.Value = true;
            lutTex.PreferredFormat.Value = TextureCompression.RawRGBA;
            lutTex.PreferredProfile.Value = Sledge2Resonite.config.GetValue(Sledge2Resonite.invertNormalmapG) ? ColorProfile.Linear : ColorProfile.sRGB;

            var lutMat = targetSlot.AttachComponent<LUT_Material>();
            lutMat.LUT.Target = lutTex;
            lutMat.UseSRGB.Value = !Sledge2Resonite.config.GetValue(Sledge2Resonite.invertNormalmapG);

            MaterialOrb.ConstructMaterialOrb(lutMat, targetSlot);
        }

        static void ClearDictionaries()
        {
            vmtDictionary.Clear();
            vtfDictionary.Clear();
        }

        static async Task UpdateProgressAsync(IProgressIndicator indicator, string progressInfo, string detailInfo)
        {
            await default(ToWorld);
            indicator.UpdateProgress(0f, progressInfo, detailInfo);
            await default(ToBackground);
        }
    }
}
