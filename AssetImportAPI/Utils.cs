using CodeX;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AssetImportAPI
{
    public static class Utils
    {
        /// <summary>
        /// Add a custom file extension (no period, IE "wav") to the list of supported extensions, with the optional given category.
        /// Specified categories (IE meshes) have different import logic, which is necessary to support.
        /// </summary>
        /// <param name="fileExtension"> The name of the file extension to patch in.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the supplied file extension is:
        /// 1) Null
        /// 2) Empty
        /// 3) Contains a Unicode character
        /// </exception>
        public static void AssetPatch(string fileExtension, AssetClass assetClass = AssetClass.Special)
        {
            if (string.IsNullOrEmpty(fileExtension) && !ContainsUnicodeCharacter(fileExtension))
            {
                var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
                aExt.Value[assetClass].Add(fileExtension);
            }
            else
            {
                throw new ArgumentException($"Supplied file extension {fileExtension} was invalid.");
            }
        }
        
        /// <summary>
        /// Utility method to generate a MD5 hash for a given filepath.
        /// Credit to delta for this method https://github.com/XDelta/
        /// </summary>
        /// <param name="filepath">The filepath to generate an MD5 for.</param>
        /// <returns>A MD5 representation of the string.</returns>
        public static string GenerateMD5(string filepath)
        {
            using var hasher = MD5.Create();
            using var stream = File.OpenRead(filepath);
            var hash = hasher.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }

        /// <summary>
        /// Utility method to identify unicode characters in a given string.
        /// </summary>
        /// <param name="input">The string to test against</param>
        /// <returns>A boolean indicating if the string contains a unicode character.</returns>
        public static bool ContainsUnicodeCharacter(string input)
        {
            const int MaxAnsiCode = 255;
            return input.Any(c => c > MaxAnsiCode);
        }
    }
}
