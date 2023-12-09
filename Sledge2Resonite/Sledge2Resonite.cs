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
using System.Xml.Linq;

namespace Sledge2Resonite
{
    public class Sledge2Resonite : ResoniteMod
    {
        public override string Name => "Sledge2Resonite";
        public override string Author => "Elektrospy";
        public override string Version => "0.1.1";
        public override string Link => "https://github.com/Elektrospy/Sledge2Resonite";

        internal static ModConfiguration config;

        private static Slot importSlot;

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(0, 1, 1))
                .AutoSave(true);
        }

        #region ModConfigurationKeys

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<int> importObjPerRow = new("importRows", "Number of objects per row", () => 5);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> tintSpecular = new("tintSpecular", "Tint Specular Textures on Import", () => false);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> generateTextureAtlas = new("Generate frame atlas", "Auto generate atlas of multiframe textures", () => true);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> SSBumpAutoConvert = new("SSBump auto convert", "Auto convert SSBump to NormalMap", () => true);

        [AutoRegisterConfigKey]
        internal static ModConfigurationKey<bool> invertNormalmapG = new("Invert normal map G", "Invert the green color channel of normal maps", () => true);

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

            if (!inputFiles.Any())
            {
                Error($"Nothing to import!");
                return;
            }

            // create collective slot if there is multiple files
            if(inputFiles.Count() > 1) 
            {
                await CreateImportSlot();
            }
            

            await ParseInputFiles(inputFiles, world, true);

            if (inputFiles.Count() > 1)
            {
                try
                {
                    // add self destruct, which triggers if there is no objects parented underneatn
                    await default(ToWorld);
                    importSlot.AttachComponent<DestroyWithoutChildren>();
                    await default(ToBackground);
                }
                catch (Exception e)
                {
                    Error($"Couldn't add Component \"DestroyWithoutChildren\" to importSlot, error: {e.Message}");
                    await default(ToBackground);
                }
            }

            ClearDictionaries();
            
            await default(ToWorld);
        }

        private static async Task<bool> CreateImportSlot()
        {
            try
            {
                await default(ToWorld); // we need to wait, or else the slot is not accessible
                importSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Imported Sledge Files");
                importSlot.PositionInFrontOfUser();
                importSlot.CreateSpawnUndoPoint();
                await default(ToBackground);
                return true;
            }
            catch (Exception e)
            {
                Error($"Couldn't add Slot \"Imported Sledge Files\" to world, error: {e.Message}");
                await default(ToBackground);
                return false;
            }
        }

        private static async Task ParseInputFiles(IEnumerable<string> inputFiles, World world, bool createQuads = false)
        {
            SerialisedObjectFormatter ValveSerialiser = new SerialisedObjectFormatter();
            string[] filesArr = inputFiles.ToArray();
            // Add parent slot to world, so we can add importing files underneath
            Slot currentParentSlot = importSlot;

            //small test to see how stuff happens
            Msg($"Importing {inputFiles.Count()} files");

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

                // split file name and ending
                string currentFileName = currentFileInfo.Name.Split('.').First();
                if (string.IsNullOrEmpty(currentFileName))
                {
                    Error("Filename is empty!");
                    continue;
                }

                // decide what todo based on file ending
                string currentFileEnding = currentFileInfo.Extension;
                switch (currentFileEnding)
                {
                    case ".vtf":
                        // import texture
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
                        // create texture from VTF file
                        Slot currentSlot = await NewTextureFromVTF(tempVtf, currentFileName, currentParentSlot);

                        // Check if this is the first time
                        if (i != 0)
                        {
                            await default(ToWorld);
                            currentSlot.LocalPosition += UniversalImporter.GridOffset(ref i, config.GetValue(importObjPerRow));
                            await default(ToBackground);
                        }

                        // create texture quad in world
                        await CreateTextureQuadFromVtf(currentFileName, tempVtf, currentSlot);

                        break;
                    case ".vmt":
                        // import material
                        VMTPreprocessor vmtPrePros = new();
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
                                {
                                    // TODO: change from general parser, to more specific method
                                    await ParseInputFiles(new string[] { tempTexturePath }, world);
                                }
                                else {
                                    Error("Texture path is empty or null!");
                                }
                            }
                        }

                        // Try to create material orb from dictionary
                        if (vmtDictionary.ContainsKey(currentFileName))
                        {
                            var currentMaterialSlot = currentParentSlot.AddSlot("Material: " + currentFileName);
                            await CreateMaterialOrbFromVmt(firstVmtObject, currentMaterialSlot);
                        }
                        else
                            Error($"Couldn't find {currentFileName} in vmt dictionary");

                        break;

                    case ".raw":
                        // luts
                        // Add undoable slot to world, so we can append components
                        Slot lutSlot;
                        try
                        {
                            await default(ToWorld);
                            lutSlot = currentParentSlot.AddSlot("LUT: " + currentFileName);
                            await default(ToBackground);
                        }
                        catch (Exception ex)
                        {
                            Error($"Couldn't add Slot \"LUT: {currentFileName}\" to world, error: {ex.Message}");
                            continue;
                        }

                        // Check if this is the first time
                        if (i != 0)
                        {
                            lutSlot.LocalPosition += UniversalImporter.GridOffset(ref i, config.GetValue(importObjPerRow));
                        }
                        await NewLUTImport(filesArr[i], lutSlot);
                        break;
                    default:
                        Error($"Unsupported file ending: {currentFileEnding}");
                        break;
                }

                if (fs != null) fs.Dispose();
            }

            await default(ToWorld);
            await default(ToBackground);
        }

        private static async Task CreateMaterialOrbFromVmt(SerialisedObject currentSerialisedObject, Slot currentSlot)
        {
            Msg($"Current material shader: {currentSerialisedObject.Name}");
            // TODO: add more material shader parsers
            await default(ToBackground);
            VertexLitGenericParser vertexLitGenericParser = new VertexLitGenericParser();
            await vertexLitGenericParser.ParseMaterial(currentSerialisedObject.Properties, currentSlot);
        }

        private static async Task CreateTextureQuadFromVtf(string currentVtfName, VtfFile currentVtf, Slot currentSlot)
        {
            await default(ToWorld);
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
                await currentTexture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
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

        private static async Task<Slot> NewTextureFromVTF(VtfFile tempVtf, string currentFileName, Slot targetSlot) {
            Slot currentSlot = null;

            try
            {
                string newSlotName = "Texture: " + currentFileName;
                Msg("Adding new slot for texture import: \"" + newSlotName + "\"");
                await default(ToWorld);
                // Add slot to world, so we can append components
                currentSlot = targetSlot.AddSlot(newSlotName);
                await default(ToBackground);
            }
            catch (Exception e)
            {
                Error($"Couldn't add Slot \"Texture: {currentFileName}\" to world, error: {e.Message}");
                return currentSlot;
            }

            return currentSlot;
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
