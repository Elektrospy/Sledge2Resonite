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
using System.Threading.Tasks;
using File = System.IO.File;
using FrooxEngine.Undo;

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
        private static ModConfigurationKey<bool> importMaterial = new("importMaterial", "Import Materials", () => true);

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
        // end of mod boiler plate code


        // data caches
        private static Dictionary<string, VtfFile> vtfDictionary = new Dictionary<string, VtfFile>();
        private static Dictionary<string, SerialisedObject> vmtDictionary = new Dictionary<string, SerialisedObject>();
        //private static Dictionary<string, StaticTexture2D> textureDictionary = new Dictionary<string, StaticTexture2D>();
        // preset values and look up lists
        private static readonly HashSet<string> shadersWithTexturesHashSet = new HashSet<string>() {
            "LightmappedGeneric", "VertexLitGeneric", "UnlitGeneric"
        };

        private static readonly HashSet<string> propertyTextureNamesHashSet = new HashSet<string>() {
            "$basetexture", "$detail", "$normalmap", "$bumpmap", "$envmapmask"
        };
        private static async Task ProcessSledgeImport(IEnumerable<string> inputFiles, World world)
        {
            await default(ToBackground);
            Msg("create dictionary entries from input files");
            await ParseInputFiles(inputFiles);
            await default(ToWorld);
            //var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Sledge Import");
            //slot.PositionInFrontOfUser();
            Msg("imported sledge files");
        }

        private static async Task ParseInputFiles(IEnumerable<string> inputFiles)
        {

            SerialisedObjectFormatter ValveSerialiser = new SerialisedObjectFormatter();
            var filesArr = inputFiles.ToArray();

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

                string currentFileName = currentFileInfo.Name;
                string currentFileEnding = currentFileInfo.Extension;

                switch (currentFileEnding)
                {
                    case ".vtf":
                        Msg("got vtf file");
                        VtfFile tempVtf = new VtfFile(fs);
                        try
                        {
                            Msg(string.Format("add vtf {0} to dictionary", currentFileName));
                            vtfDictionary.Add(currentFileName, tempVtf);
                            await CreateTextureFromVtf(currentFileName, tempVtf);
                        } catch (Exception e)
                        {
                            Error(string.Format("couldn't add vtf {0}, error: {1}", currentFileName, e.ToString()));
                        }
                        break;
                    case ".vmt":
                        Msg("got vmt file, start cleanup / fixing");
                        VMTPreprocessor vmtPrePros = new VMTPreprocessor();
                        string[] fileLines;
                        try
                        {
                            fileLines = File.ReadAllLines(filesArr[i]);
                        }
                        catch(Exception ex)
                        {
                            Error(string.Format("file read all lines error: {0}", ex.ToString()));
                            continue;
                        }
                        // clean up the vmt, before handing it over to format library for parsing
                        vmtPrePros.Preprocess(fileLines, out fileLines);
                        // create memory stream as fake FileStream and add cleaned up vmt string array
                        MemoryStream memoryStream = new MemoryStream();
                        StreamWriter writer = new StreamWriter(memoryStream);
                        writer.Write(fileLines);
                        // try to deserialze stream into list of serialised valve objects
                        Msg("start vmt deserialize");
                        List<SerialisedObject> tempSerialzeObjectList;
                        try
                        {
                            tempSerialzeObjectList = ValveSerialiser.Deserialize(memoryStream).ToList();

                        } 
                        catch(Exception ex)
                        {
                            Error(string.Format("valve deserialize error: {0}", ex.ToString()));
                            continue;
                        }

                        Msg("finished deserialize of vmt file");
                        foreach (var currentObj in tempSerialzeObjectList)
                        {
                            Msg(string.Format("current object: {0}", currentObj.Name));
                            try
                            {
                                Msg(string.Format("add vmt {0} to dictionary", currentFileName));
                                vmtDictionary.Add(currentFileName, currentObj);
                                // does the material contain textures?
                                if (!shadersWithTexturesHashSet.Contains(currentObj.Name))
                                {
                                    Error(string.Format("current object doesn't contain texture: {0}", currentObj.Name));
                                    continue;
                                }
                                // grab textures from vmt properties
                                foreach (KeyValuePair<string, string> currentProperty in currentObj.Properties)
                                {
                                    if (propertyTextureNamesHashSet.Contains(currentProperty.Key))
                                    {
                                        string fullPath = MergeTextureNameAndPath(currentProperty.Value, currentFileInfo.FullName);
                                        await ParseInputFiles(new string[] { fullPath });
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                Error($"couldn't add vmt {currentFileName}");
                            }
                        }
                        break;
                    default:
                        Error(string.Format("Unknown file ending: {0}", currentFileEnding));
                        break;
                }

            }
        }

        private static string MergeTextureNameAndPath(string currentFullName, string currentPath)
        {
            if(String.IsNullOrEmpty(currentFullName) || String.IsNullOrEmpty(currentPath))
            {
                return "";
            }
            string currentTextureName = currentFullName.Split('/').Last();
            return currentPath + '/' + currentTextureName;
        }

        private static async Task CreateMaterialOrbsFromDictionary()
        {
            int counter = 1;
            foreach (var currentEntry in vmtDictionary)
            {
                await default(ToWorld); 
                var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot(currentEntry.Key);
                slot.PositionInFrontOfUser();
                slot.GlobalPosition = slot.GlobalPosition + new float3(counter * .2f, 0f, 0f);
                await default(ToBackground);
                await CreateMaterialOrbFromVmt(currentEntry.Key, currentEntry.Value);
            }
        }

        private static async Task CreateMaterialOrbFromVmt(string currentVmtName, SerialisedObject currentSerialisedObject)
        {
            if (currentVmtName == "")
            {
                Error("Vtf name is empty!");
                return;
            }

            var currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Material: " + currentVmtName);
            currentSlot.PositionInFrontOfUser();
            await default(ToBackground);
            //await SetupTextures(currentVtfFile, )
            //await SetupSpecular(currentSerialisedObject, currentSlot);
            VertexLitGenericParser vertexLitGenericParser = new VertexLitGenericParser();
            await vertexLitGenericParser.CreateMaterialFromProperties(currentSerialisedObject.Properties);
        }

        private static async Task CreateTextureFromVtf(string currentVtfName, VtfFile currentVtf)
        {
            await default(ToWorld);
            Msg("creating texture from vtf: " + currentVtfName);
            var currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Texture: " + currentVtfName);
            currentSlot.PositionInFrontOfUser();
            //await default(ToBackground);
            // currently we only grab the last frame from the image for highest resolution, first for lowest
            // TODO: add handeling of multi frames textures and generate spirtesheet
            VtfImage tempVtfImage = currentVtf.Images.GetLast();
            // create new Bitmap2D to hold our raw image data, disable midmaps and Y axis flip
            var newBitmap = new Bitmap2D(tempVtfImage.GetBgra32Data(), tempVtfImage.Width, tempVtfImage.Height, TextureFormat.BGRA32, false, false);
            // creating save asset aysnc to local db
            var tempUri = await currentSlot.World.Engine.LocalDB.SaveAssetAsync(newBitmap);
            await default(ToWorld);
            Msg($"creating staticTexture2D on: {currentSlot} with Uri {tempUri}");
            // create texture object and assign attributes
            StaticTexture2D tempTexture2D = currentSlot.AttachComponent<StaticTexture2D>();
            tempTexture2D.URL.Value = tempUri;
            // set filtering mode
            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Pointsample)) 
            {
                tempTexture2D.FilterMode.Value = TextureFilterMode.Point;
            }
            else if(currentVtf.Header.Flags.HasFlag(VtfImageFlag.Trilinear))
            {
                tempTexture2D.FilterMode.Value = TextureFilterMode.Trilinear;
            }
            else 
            {
                tempTexture2D.FilterMode.Value = TextureFilterMode.Anisotropic;
                tempTexture2D.AnisotropicLevel.Value = 8;
            }
            // check if normalmap
            if (currentVtf.Header.Flags.HasFlag(VtfImageFlag.Normal))
            {
                tempTexture2D.IsNormalMap.Value = true;
                // source engine uses the DirectX standard for normal maps, NeosVR uses OpenGL
                // so we need to invert the green channel
                // DirectX is referred as Y- (top-down), OpenGL is referred as Y+ (bottom-up)
                tempTexture2D.ProcessPixels((color c) => new color(c.r, 1f - c.g, c.b, c.a));
            }
            // spawn texture quad in world
            // TODO: maybe change to ImageImporter.ImportImage(uri, slot) method
            ImageImporter.SetupTextureProxyComponents(currentSlot, tempTexture2D, StereoLayout.None, ImageProjection.Perspective, false);
            ImageImporter.CreateQuad(currentSlot, tempTexture2D, StereoLayout.None, true);
            // make the quad grabbable
            currentSlot.AttachComponent<Grabbable>().Scalable.Value = true;
            // make undoable
            currentSlot.CreateSpawnUndoPoint();
            Msg("creating texture (hopefully) succesfull!");
        }


        private static async Task SetupSpecular(SerialisedObject currentSerialisedObject, Slot slot)
        {
            await new ToBackground();
            LocalDB localDb = slot.World.Engine.LocalDB;

            slot.CreateMaterialOrb<PBS_Specular>();
            var neosMat = slot.GetComponent<PBS_Specular>();
            StaticTexture2D tex = null;

            Msg(string.Format("Create specular material from vmt {0}", currentSerialisedObject.Name));

            //// grab textures from vmt properties
            //foreach (KeyValuePair<string, string> currentProperty in currentSerialisedObject.Properties)
            //{
            //    // handle specific textures
            //    if (propertyTextureNamesHashSet.Contains(currentProperty.Key))
            //    {
            //        string currentTextureName = currentProperty.Value.Split('/').Last();
            //        if(!vtfDictionary.ContainsKey(currentTextureName))
            //        {
            //            Error(string.Format("couldn't find texture {0} in dictionary, skipping", currentTextureName));
            //            continue;
            //        }

            //        VtfFile tempVtf;
            //        vtfDictionary.TryGetValue(currentTextureName, out tempVtf);
            //        VtfImage tempVtfImage = tempVtf.Images.GetFirst();

            //        // encode 
            //        var newBitmap = new Bitmap2D(tempVtfImage.GetBgra32Data(), tempVtfImage.Width, tempVtfImage.Height, TextureFormat.BGRA32, false);
            //        var tempUri = await localDb.SaveAssetAsync(newBitmap).ConfigureAwait(false);
            //        StaticTexture2D tempTexture2D = slot.AttachComponent<StaticTexture2D>();
            //        tempTexture2D.URL.Value = tempUri;

            //        switch(currentProperty.Key)
            //        {
            //            case "$basetexture":
            //                neosMat.AlbedoTexture.Target = tempTexture2D;
            //                break;
            //            case "$detail":
            //                neosMat.DetailAlbedoTexture.Target = tempTexture2D;
            //                break;
            //            case "$normalmap":
            //            case "$bumpmap":
            //                tempTexture2D.IsNormalMap.Value = true;
            //                neosMat.NormalMap.Target = tempTexture2D;
            //                break;
            //            case "$envmapmask":
            //                break;
            //        }
            //    }

            //    // handle render specific keys
            //    switch (currentProperty.Key)
            //    {
            //        case "$alphatest":
            //            if(currentProperty.Value == "1")
            //            {
            //                neosMat.BlendMode.Value = BlendMode.Cutout;
            //            }
            //            break;
            //        case "$alphatestreference":
            //            if (float.TryParse(currentProperty.Value, out float parsed))
            //                neosMat.AlphaCutoff.Value = parsed;
            //            else
            //                neosMat.AlphaCutoff.Value = 1f;
            //            break;
            //        case "$translucent":
            //            if (currentProperty.Value == "1")
            //            {
            //                neosMat.BlendMode.Value = BlendMode.Cutout;
            //            }
            //            break;
            //    }
            //}
        }

    }
}
