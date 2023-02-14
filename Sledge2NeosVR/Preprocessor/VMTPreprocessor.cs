﻿using BaseX;
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
            UniLog.Log("Remove enclosing quotation marks from first line");
            // remove trailing comments
            parsedFile = replaceCommentsMulti.Replace(input, replaceCommentsSubstitutionMulti); // works :)
            UniLog.Log(parsedFile);
            UniLog.Log("add missing quotation marks");
            // add missing quotation marks
            parsedFile = suffixReplacementMulti.Replace(parsedFile, suffixReplacementSubstitutionMulti);
            UniLog.Log(parsedFile);
            parsedFile = parsedFile.Replace("\"\"{", "\"{");
            parsedFile = parsedFile.Replace("}\"\"", "}\"");
            // remove the quotation marks from the first line
            UniLog.Log("remove the quotation marks from the first line");
            string firstline = parsedFile.Substring(0, parsedFile.IndexOf(Environment.NewLine));
            UniLog.Log(firstline);
            string parsedFirstLine = firstline.Replace("\"", "");
            UniLog.Log(parsedFirstLine);
            parsedFile = parsedFile.Replace(firstline, parsedFirstLine);
            UniLog.Log(parsedFile);

            return true;
        }
        catch (Exception e)
        {
            UniLog.Log($"ParseVmt failed with error: {e}");
            parsedFile = string.Empty;
        }

        return false;
    }
}