using System.Text.RegularExpressions;
using System.Globalization;

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

        // Loop through each match and divide the number by 255
        for (int i = 0; i < matches.Count; i++)
        {
            float value = float.Parse(matches[i].Value);
            float result = value / 255;
            input = input.Replace(matches[i].Value, result.ToString(CultureInfo.InvariantCulture));
        }

        return input;
    }
}