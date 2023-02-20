using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
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
using Sledge.Formats.Tokens.Readers;
using System.Runtime.InteropServices.ComTypes;
using static NeosAssets.Graphics.LogiX;
using UnityEngine;

namespace Sledge2NeosVR
{
    public class Sledge2NeosVR : NeosMod
    {
        public override string Name => "Sledge2NeosVR";
        public override string Author => "Elektrospy";
        public override string Version => "0.1.0";
        public override string Link => "https://github.com/Elektrospy/Sledge2NeosVR";


        private static ModConfiguration config;
        // start of mod boiler plate code
        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(0, 1, 0))
                .AutoSave(true);
        }
        // add new config options to nml settings menu
        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> importTexture = new("importTexture", "Import Textures", () => true);
        [AutoRegisterConfigKey]
        private static ModConfigurationKey<int> importTextureRow = new("Texture rows", "Import Textures number of rows", () => 5);
        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> importMaterial = new("importMaterial", "Import Materials", () => true);
        [AutoRegisterConfigKey]
        private static ModConfigurationKey<int> importMaterialRow = new("Material rows", "Import Materials number of rows", () => 5);

        public override void OnEngineInit()
        {
            new Harmony("net.Elektrospy.Sledge2NeosVR").PatchAll();
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => AssetPatch());
        }

        private static void AssetPatch()
        {
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Special].Add("vtf");
            aExt.Value[AssetClass.Special].Add("vmt");
        }

        [HarmonyPatch(typeof(UniversalImporter), "ImportTask", typeof(AssetClass), typeof(IEnumerable<string>), typeof(World), typeof(float3), typeof(floatQ), typeof(float3), typeof(bool))]
        public static class UniversalImporterPatch
        {
            static bool Prefix(ref IEnumerable<string> files, ref Task __result, World world)
            {
                var query = files.Where(x =>
                    x.EndsWith("vtf", StringComparison.InvariantCultureIgnoreCase) ||
                    x.EndsWith("vmt", StringComparison.InvariantCultureIgnoreCase));

                if (query.Any())
                {
                    Msg("Importing sledge asset");
                    __result = ProcessSledgeImport(query, world);
                }

                return true;
            }
        }

        // Data caches
        public static Dictionary<string, VtfFile> vtfDictionary = new Dictionary<string, VtfFile>();
        public static Dictionary<string, SerialisedObject> vmtDictionary = new Dictionary<string, SerialisedObject>();
        //private static Dictionary<string, StaticTexture2D> textureDictionary = new Dictionary<string, StaticTexture2D>();
        // preset values and look up lists
        private static readonly HashSet<string> shadersWithTexturesHashSet = new HashSet<string>() {
            "LightmappedGeneric", "VertexLitGeneric", "UnlitGeneric"
        };

        protected static readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>()
        {
            "$basetexture", "$detail", "$normalmap", "$bumpmap", "$envmapmask"
        };
        private static async Task ProcessSledgeImport(IEnumerable<string> inputFiles, World world)
        {
            await default(ToBackground);
            Msg("create dictionary entries from input files");
            await ParseInputFiles(inputFiles, true);
            await default(ToWorld);
            Msg("imported sledge files, clear dictionaries");
            ClearDictionaries();
            Msg("dictionaries cleared");
        }

        private static void ClearDictionaries()
        {
            vmtDictionary.Clear();
            vtfDictionary.Clear();
        }

        private static async Task ParseInputFiles(IEnumerable<string> inputFiles, bool createQuads = false)
        {
            SerialisedObjectFormatter ValveSerialiser = new SerialisedObjectFormatter();
            string[] filesArr = inputFiles.ToArray();
            int vtfCounter = 0;
            int vmtCounter = 0;

            for (int i = 0; i < filesArr.Count(); ++i)
            {
                if (!File.Exists(filesArr[i]))
                {
                    continue;
                }

                FileInfo currentFileInfo;
                FileStream fs;
                try {
                    Msg(string.Format("try to get fileinfo for: {0}", filesArr[i]));
                    currentFileInfo = new FileInfo(filesArr[i]);
                    Msg(string.Format("try to open file: {0}", filesArr[i]));
                    fs = File.Open(filesArr[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch(Exception ex)
                {
                    Error(string.Format("Got an exception while open the file: {0}, {1}", filesArr[i], ex.ToString()));
                    continue;
                }

                string currentFileName = currentFileInfo.Name.Split('.').First();
                string currentFileEnding = currentFileInfo.Extension;
                // decide what to do, base on file ending
                switch (currentFileEnding)
                {
                    case ".vtf":
                        Msg("got vtf file");
                        VtfFile tempVtf = new VtfFile(fs);
                        try
                        {
                            if(vtfDictionary.ContainsKey(currentFileName))
                            {
                                Msg($"grab vtf {currentFileName} from dictionary");
                                tempVtf = vtfDictionary[currentFileName];
                            }
                            else
                            {
                                Msg($"add vtf {currentFileName} to dictionary");
                                vtfDictionary.Add(currentFileName, tempVtf);
                            }
                        } 
                        catch (Exception e)
                        {
                            Error($"couldn't add vtf {currentFileName}, error: {e}");
                        }

                        if (!createQuads) return;

                        // add undoable slot to world, so we can append components
                        Msg($"add undoable slot \"Texture: {currentFileName}\" to world");
                        Slot currentSlot;
                        await default(ToWorld);
                        try
                        {
                            currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Texture: " + currentFileName);
                            currentSlot.CreateSpawnUndoPoint();
                        }
                        catch(Exception e) {
                            Error($"couldn't add Slot \"Texture: {currentFileName}\" to world, error: {e}");
                            continue;
                        }

                        // check if this is the first time
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
                        Msg($"add Texture quad to world");
                        await CreateTextureQuadFromVtf(currentFileName, tempVtf, currentSlot);
                        break;
                    case ".vmt":
                        Msg("got vmt file, create new VMTPreprocessor");
                        VMTPreprocessor vmtPrePros = new VMTPreprocessor();
                        string fileLines;
                        try
                        {
                            Msg("read all lines from vmt");
                            fileLines = File.ReadAllText(filesArr[i]);
                        }
                        catch(Exception ex)
                        {
                            Error($"file read all lines error: {ex}");
                            continue;
                        }
                        // clean up the vmt, before handing it over to format library for parsing
                        Msg("clean up the vmt, before handing it over to format library for parsing");
                        vmtPrePros.ParseVmt(fileLines, out fileLines);
                        Msg("create new List<SerialisedObject>");
                        List<SerialisedObject> tempSerialzeObjectList = new();
                        Msg("create new memoryStream and add parsed VMT lines");
                        using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileLines)))
                        {
                            try
                            {
                                Msg("try to deserialize VMT memoryStream");
                                tempSerialzeObjectList = ValveSerialiser.Deserialize(memoryStream).ToList();
                            }
                            catch (Exception e)
                            {
                                Error($"valve deserialize error: {e}");
                                continue;
                            }
                        }

                        Msg("finished deserialize of vmt file");
                        var firstVmtObject = tempSerialzeObjectList.First();

                        Msg($"add vmt {currentFileName} to dictionary");
                        if (!vmtDictionary.ContainsKey(firstVmtObject.Name))
                        {
                            vmtDictionary.Add(currentFileName, firstVmtObject);
                        }

                        Msg($"try to grab all necessary textures from material {currentFileName}");
                        // propertyTextureNamesHashSet -> hashset to compare against
                        foreach (KeyValuePair<string, string> currentProperty in firstVmtObject.Properties)
                        {
                            if (propertyTextureNamesHashSet.Contains(currentProperty.Key))
                            {
                                Msg($"Property {currentProperty.Key} matched valid texture type, try to import!");
                                string tempTexturePath = MergeTextureNameAndPath(currentProperty.Value, filesArr[i]);
                                if (!string.IsNullOrEmpty(tempTexturePath)) {
                                    Msg($"got usable texture path {tempTexturePath}");
                                    ParseInputFiles(new string[] { tempTexturePath });
                                }
                                else
                                {
                                    Error("texture path is empty or null!");
                                }
                            }
                        }

                        // try to create material orb from dictionary
                        if (vmtDictionary.ContainsKey(currentFileName))
                        {
                            Msg("create material orb from vmt inside dictionary");
                            await CreateMaterialOrbFromVmt(currentFileName, firstVmtObject);
                        }
                        else
                        {
                            Error($"Couldn't find {currentFileName} in vmt dictionary");
                        }
                        vmtCounter++;
                        break;
                    default:
                        Error($"Unknown file ending: {currentFileEnding}");
                        break;
                }

            }
        }

        private static string MergeTextureNameAndPath(string textureName, string materialPath)
        {
            if (string.IsNullOrEmpty(textureName) || string.IsNullOrEmpty(materialPath))
            {
                return string.Empty;
            }

            Msg($"textureName: {textureName}, materialPath: {materialPath}");
            
            // example paths for merging
            // texture path from vmt:    "models\props_blackmesa\lamppost03_grey_on"
            // material path:   "C:\Steam\steamapps\common\Black Mesa\bms\materials\models\props_blackmesa"
            // flip "/" to "\" for texture path
            textureName = textureName.Replace("/", "\\");
            const string baseFolderName = "\\materials\\";
            try
            {
                materialPath = materialPath.Substring(0, materialPath.LastIndexOf(baseFolderName));
                // D:\Steam\steamapps\common\Black Mesa\bmsconsole\background01.vtf
                string resultTexturePath = materialPath + baseFolderName + textureName + ".vtf";
                Msg($"texture path: {resultTexturePath}");
                return resultTexturePath;
            }
            catch (Exception ex)
            {
                Error($"substring error: {ex}");
                return string.Empty;
            }
        }

        private static async Task CreateMaterialOrbFromVmt(string currentVmtName, SerialisedObject currentSerialisedObject)
        {
            if (string.IsNullOrEmpty(currentVmtName))
            {
                Error("Vtf name is empty!");
                return;
            }

            Msg("CreateMaterialOrbFromVmt: ToBackground");
            await default(ToBackground);
            Msg("CreateMaterialOrbFromVmt: create new VertexLitGenericParser");
            VertexLitGenericParser vertexLitGenericParser = new VertexLitGenericParser();
            Msg("CreateMaterialOrbFromVmt: CreateMaterial()");
            await vertexLitGenericParser.ParseMaterial(currentSerialisedObject.Properties, currentVmtName);
            Msg("Post");
        }

        private static async Task CreateTextureQuadFromVtf(string currentVtfName, VtfFile currentVtf, Slot currentSlot)
        {
            await default(ToWorld);
            Msg("creating texture quad from vtf: " + currentVtfName);
            currentSlot.PositionInFrontOfUser();
            //await default(ToBackground);
            // currently we only grab the last frame from the image for the highest resolution, first if you want the lowest
            // TODO: add handling of multi frames textures and generate spirtesheet
            VtfImage currentVtfImage = currentVtf.Images.GetLast();
            // create new Bitmap2D to hold our raw image data, disable mipmaps and Y axis flip
            var newBitmap = new Bitmap2D(currentVtfImage.GetBgra32Data(), currentVtfImage.Width, currentVtfImage.Height, CodeX.TextureFormat.BGRA32, false, false);
            // creating save asset aysnc to local db
            var currentUri = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);
            await default(ToWorld);
            Msg($"creating staticTexture2D on: {currentSlot} with Uri {currentUri}");
            // create texture object and assign attributes
            StaticTexture2D currentTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
            currentTexture2D.URL.Value = currentUri;

            // set filtering mode
            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Pointsample)) 
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Point;
            }
            else if(currentVtf.Header.Flags.HasFlag(VtfImageFlag.Trilinear))
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Trilinear;
            }
            else 
            {
                currentTexture2D.FilterMode.Value = TextureFilterMode.Anisotropic;
                currentTexture2D.AnisotropicLevel.Value = 8;
            }

            // check if normalmap flag is set and the name contains "_normal"
            // come textures contain "flawed" flags within the header
            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Normal) && currentVtfName.ToLower().Contains("_normal"))
            {
                currentTexture2D.IsNormalMap.Value = true;
                // Source engine uses the DirectX standard for normal maps, NeosVR uses OpenGL
                // so we need to invert the green channel
                // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)
                currentTexture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
            }

            // spawn texture quad in world
            // TODO: maybe change to ImageImporter.ImportImage(uri, slot) method
            ImageImporter.SetupTextureProxyComponents(currentSlot, currentTexture2D, StereoLayout.None, ImageProjection.Perspective, false);
            ImageImporter.CreateQuad(currentSlot, currentTexture2D, StereoLayout.None, true);
            // make the quad grabbable
            currentSlot.AttachComponent<Grabbable>().Scalable.Value = true;
            Msg("creating texture (hopefully) succesfull!");
        }
    }
}
