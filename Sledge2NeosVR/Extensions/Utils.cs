using BaseX;
using System.Globalization;
using System.Text.RegularExpressions;
using System;

public static class Utils
{
    internal static string DivideNumbersBy255(string input)
    {
        // Create a regular expression pattern to match numbers in the input string
        string pattern = @"-?\d+\.?\d*";

        // Create a Regex object to match the pattern
        Regex regex = new Regex(pattern);

        // Find all matches in the input string
        MatchCollection matches = regex.Matches(input);

        // Loop through each match and divide the number by 255
        for (int i = 0; i < matches.Count; i++)
        {
            float value = float.Parse(matches[i].Value);
            float result = value / 255f;
            input = input.Replace(matches[i].Value, result.ToString(CultureInfo.InvariantCulture));
        }

        return input;
    }

    internal static bool ParseValveNumberString(string str, out string parsed)
    {
        str = str.Replace("{", "[");
        str = str.Replace("}", "]");
        str = str.Trim();

        // matches all spaces behind [ or { and spaces before ] or }
        const string magic1 = "((?<=([[]))[ ]*)|([ ]*(?=([]])))";
        // matches all spaces between numbers
        const string magic2 = "[ \t]+";

        str = Regex.Replace(str, magic1, "");
        str = Regex.Replace(str, magic2, ";");

        // float3 parse expected string format: "[0;0;0]"
        if (str.Contains("."))
        {
            parsed = str;
            return true;
        }
        else
        {
            parsed = DivideNumbersBy255(str);
            return true;
        }
    }

    internal static string MergeTextureNameAndPath(string textureName, string materialPath)
    {
        if (string.IsNullOrEmpty(textureName) || string.IsNullOrEmpty(materialPath))
        {
            return string.Empty;
        }

        // example paths for merging
        // texture path from vmt:    "models\props_blackmesa\lamppost03_grey_on"
        // material path:   "C:\Steam\steamapps\common\Black Mesa\bms\materials\models\props_blackmesa"
        // flip "/" to "\" for texture path
        textureName = textureName.Replace("/", "\\");
        const string baseFolderName = "\\materials\\";
        try
        {
            // D:\Steam\steamapps\common\Black Mesa\bmsconsole\background01.vtf
            materialPath = materialPath.Substring(0, materialPath.LastIndexOf(baseFolderName));
            string resultTexturePath = materialPath + baseFolderName + textureName + ".vtf";
            return resultTexturePath;
        }
        catch (Exception ex)
        {
            UniLog.Error($"Substring error in MergeTextureNameAndPath: {ex.Message}");
            return string.Empty;
        }
    }
}