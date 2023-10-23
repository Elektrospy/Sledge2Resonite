using Elements.Core;
using System.Globalization;
using System.Text.RegularExpressions;
using System;
using FrooxEngine;
using Elements.Assets;
using System.Numerics;

public static class Utils
{
    private static float OO_SQRT_3 = 0.57735025882720947f;
    static Vector3[] bumpBasisTranspose = new Vector3[]{
        new Vector3( 0.81649661064147949f, -0.40824833512306213f, -0.40824833512306213f ),
        new Vector3(  0.0f, 0.70710676908493042f, -0.7071068286895752f ),
        new Vector3(  OO_SQRT_3, OO_SQRT_3, OO_SQRT_3 )
    };

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

    internal static void SSBumpToNormal(StaticTexture2D texture2D)
    {
        texture2D.ProcessPixels(c =>
        {
            var ColorVector = new Vector3(c.r, c.g, c.b);
            return new color(
                        ConvertVector(ref ColorVector, 0),
                        ConvertVector(ref ColorVector, 1),
                        ConvertVector(ref ColorVector, 2),
                        c.a);
        });
    }
    internal static Bitmap2D SSBumpToNormal(Bitmap2D image)
    {
        // Assign new pixel values to albedo alpha channel
        for (int x = 0; x < image.Size.x; x++)
        {
            for (int y = 0; y < image.Size.y; y++)
            {
                var pixel = image.GetPixel(x, y);
                var readVector = new Vector3(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f);

                image.SetPixel(x, y,
                    new color(
                        ConvertVector(ref readVector, 0),
                        ConvertVector(ref readVector, 1),
                        ConvertVector(ref readVector, 2),
                        pixel.a));
            }
        }

        return image;
    }

    private static float ConvertVector(ref Vector3 vecIn, int index)
    {
        float newColor = Vector3.Dot(vecIn, bumpBasisTranspose[index]) * 0.5f + 0.5f;
        return MathX.Clamp(newColor, 0f, 1f);
    }
}