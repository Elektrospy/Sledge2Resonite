using System.Globalization;
using System.Text.RegularExpressions;

public static class Helpers
{
    public static string DivideNumbersBy255(string input)
    {
        // Create a regular expression pattern to match numbers in the input string
        string pattern = @"-?\d+\.?\d*";

        // Create a Regex object to match the pattern
        Regex regex = new Regex(pattern);

        // Find all matches in the input string
        MatchCollection matches = regex.Matches(input);

        // enforce "." instead of ","
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";

        // Loop through each match and divide the number by 255
        for (int i = 0; i < matches.Count; i++)
        {
            float value = float.Parse(matches[i].Value);
            float result = value / 255f;
            Console.WriteLine($"By255 {i}: {value.ToString(nfi)} -> {result.ToString(nfi)}");
            input = input.Replace(matches[i].Value, result.ToString(CultureInfo.InvariantCulture));
        }
        Console.WriteLine($"By255 result: {input}");

        return input;
    }

    public static bool ParseValveNumberString(string str, out string parsed)
    {
        str = str.Replace("{", "[");
        str = str.Replace("}", "]");
        str = str.Trim();
        Console.WriteLine($"helper trim: {str}");

        const string magic1 = "((?<=([[]))[ ]*)|([ ]*(?=([]])))"; // matches all spaces behind [ or { and spaces before ] or }
        const string magic2 = "[ \t]+"; // matches all spaces between numbers
        str = Regex.Replace(str, magic1, "");
        Console.WriteLine($"helper magic1: {str}");
        str = Regex.Replace(str, magic2, ";");
        Console.WriteLine($"helper magic2: {str}");

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
}