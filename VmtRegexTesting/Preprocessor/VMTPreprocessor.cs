using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sledge2NeosVR;

public class VMTPreprocessor : Preprocessor
{
    public bool ParseVmt(string input, out string parsedFile)
    {
        try
        {
            if (!Preprocess(input))
            {
                Console.WriteLine("Invalid file string array supplied");
                parsedFile = string.Empty;
                return false;
            }

            // Remove enclosing quotation marks from first line
            Console.WriteLine("Remove enclosing quotation marks from first line");
            // remove trailing comments
            parsedFile = replaceCommentsMulti.Replace(input, replaceCommentsSubstitutionMulti); // works :)
            Console.WriteLine(parsedFile);
            // add missing quotation marks
            parsedFile = suffixReplacementMulti.Replace(parsedFile, suffixReplacementSubstitutionMulti);
            Console.WriteLine(parsedFile);
            parsedFile = parsedFile.Replace("\"\"{", "\"{");
            parsedFile = parsedFile.Replace("}\"\"", "}\"");
            // remove the quotation marks from the first line
            string firstline = parsedFile.Substring(0, parsedFile.IndexOf(Environment.NewLine));
            Console.WriteLine(firstline);
            string parsedFirstLine = firstline.Replace("\"", "");
            Console.WriteLine(parsedFirstLine);
            parsedFile = parsedFile.Replace(firstline, parsedFirstLine);
            Console.WriteLine(parsedFile);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"ParseVmt failed with error: {e}");
            parsedFile = string.Empty;  
        }

        return false;
    }
}