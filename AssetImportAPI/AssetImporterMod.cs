using AssetImportAPI.Archive;
using AssetImportAPI.Single;
using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssetImportAPI
{
    public class AssetImporterMod : NeosMod
    {
        public override string Name => "AssetImporterTemplate";
        public override string Author => "dfgHiatus";
        public override string Version => "1.1.0";
        public override string Link => "https://github.com/dfgHiatus/AssetImportAPI";

        public static ModConfiguration config;
        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(1, 0, 0))
                .AutoSave(true);
        }

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importAsRawFiles =
             new("importAsRawFiles",
             "Import files directly into Neos. Archives can be very large, keep this true unless you know what you're doing!",
             () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importText =
            new("importText", "Import Text", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importTexture =
            new("importTexture", "Import Textures", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importDocument =
            new("importDocument", "Import Documents", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importMesh =
            new("importMesh", "Import Mesh", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importPointCloud =
            new("importPointCloud", "Import Point Clouds", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importAudio =
            new("importAudio", "Import Audio", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importFont =
            new("importFont", "Import Fonts", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> importVideo =
            new("importVideo", "Import Videos", () => true);

        private static SingleImporter SingleImporter;
        private static ArchiveImporter ArchiveImporter;

        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.AssetImporterTemplate").PatchAll();
            config = GetConfiguration();

            SingleImporter = new SingleImporter();
            ArchiveImporter = new ArchiveImporter();
            SingleImporter.Initiallize("", AssetClass.Audio);
            ArchiveImporter.Initiallize("", AssetClass.Special);
        }

        /// <summary>
        /// For single file imports, use the supplied template. Likewise for archives.
        /// </summary>
        [HarmonyPatch(typeof(UniversalImporter), "Import", typeof(AssetClass), typeof(IEnumerable<string>),
           typeof(World), typeof(float3), typeof(floatQ), typeof(bool))]
        public class UniversalImporterPatch
        {
            static bool Prefix(ref IEnumerable<string> files, ref Task __result)
            {
                // Handle incoming files here
                return true;
            }
        }
    }
}
