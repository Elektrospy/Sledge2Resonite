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
using static System.Net.WebRequestMethods;
using File = System.IO.File;

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
        private static Dictionary<string, StaticTexture2D> textureDictionary = new Dictionary<string, StaticTexture2D>();
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
            await CreateDictionaryEntries(inputFiles);

            await default(ToWorld);
            var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Sledge Import");
            slot.PositionInFrontOfUser();

            await CreateMaterialsOrbsFromDictionary();

            Msg("imported sledge materials");
        }

        private static async Task CreateDictionaryEntries(IEnumerable<string> inputFiles)
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
                    //fs = File.OpenRead(filesArr[i]);
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
                        } catch (Exception)
                        {
                            Error(string.Format("couldn't add vtf {0}", currentFileName));
                        }
                        break;
                    case ".vmt":
                        Msg("got vmt file, start deserialize...");
                        List<SerialisedObject> tempSerialzeObject;
                        try
                        {
                            tempSerialzeObject = ValveSerialiser.Deserialize(fs).ToList();

                        } 
                        catch(Exception ex)
                        {
                            Error(string.Format("valve deserialize error: {0}", ex.ToString()));
                            continue;
                        }
                        Msg("finished deserialize of vmt file");
                        foreach (var currentObj in tempSerialzeObject)
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
                                        await CreateDictionaryEntries(new string[] { fullPath });
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                Error(string.Format("couldn't add vmt {0}", currentFileName));
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

        private static async Task CreateMaterialsOrbsFromDictionary()
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

        private static async Task CreateMaterialOrbFromVmt(string currentVtfName, SerialisedObject currentSerialisedObject)
        {
            if (currentVtfName == "")
            {
                return;
            }

            var currentSlot = Engine.Current.WorldManager.FocusedWorld.AddSlot("Material: " + currentVtfName);
            currentSlot.PositionInFrontOfUser();
            await default(ToBackground);
            //await SetupTextures(currentVtfFile, )
            await SetupSpecular(currentSerialisedObject, currentSlot);
        }

        private void CreateTexturesFromVtfDictionary()
        {
            foreach (var currentEntry in vtfDictionary)
            {
                CreateTextureFromVtf(currentEntry.Value);
            }
        }

        private void CreateTextureFromVtf(VtfFile currentVtf)
        {
            int counter = currentVtf.Images.Count;
            if (counter > 0)
            {
                return;
            }

            VtfImage tempVtfImage = currentVtf.Images.GetFirst();

            // TODO: create texture object in world and asign image data
            var newBitmap = new Bitmap2D(tempVtfImage.GetBgra32Data(), tempVtfImage.Width, tempVtfImage.Height, TextureFormat.BGRA32, false);
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

        private static async Task FixVmtFile()
        {

        }

        
    }
}
