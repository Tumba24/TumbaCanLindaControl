using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tumba.CanLindaControl.Model;

namespace Tumba.CanLindaControl.Helpers
{
    public class IniHelper
    {
        public static bool TryReadIniFromFile<T>(string filePath, out T ini, out string errorMessage)
            where T : BaseIni, new()
        {
            ini = new T();

            Dictionary<string, string> keyValues;
            if (!TryReadKeyValuesFromFile(filePath, out keyValues, out errorMessage))
            {
                return false;
            }

            ini.SetValues(keyValues);

            List<string> errors;
            if (!ini.ValidateIni(out errors))
            {
                StringBuilder errBuilder = new StringBuilder();
                errBuilder.AppendLine("Ini validation failed!  See error(s):");

                if (errors == null || errors.Count < 1)
                {
                    errBuilder.AppendLine("No error details specified!");
                }
                else
                {
                    foreach (string error in errors)
                    {
                        errBuilder.AppendLine(error);
                    }
                }

                errorMessage = errBuilder.ToString();
                return false;
            }

            return true;
        }
        
        public static bool TryReadKeyValuesFromFile(string filePath, out Dictionary<string, string> keyValues, out string errorMessage)
        {
            keyValues = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            List<string> linesToParse = new List<string>();

            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string cleanLine = line.Trim();
                        if (!string.IsNullOrEmpty(cleanLine) && !cleanLine.StartsWith("#"))
                        {
                            linesToParse.Add(cleanLine);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                errorMessage = string.Format("Failed to read key values from file!  See exception for details: {0}", exception);
                return false;
            }

            foreach (string line in linesToParse)
            {
                int spliterPos = line.IndexOf('=');
                if (spliterPos < 1)
                {
                    continue;
                }

                string key = line.Substring(0, spliterPos).Trim();
                string value = line.Substring(spliterPos + 1).Trim();

                if (keyValues.ContainsKey(key))
                {
                    errorMessage = string.Format("Failed to read key values from file! Duplicate key: '{0}' found!", key);
                    return false;
                }

                keyValues.Add(key, value);
            }

            errorMessage = null;
            return true;
        }
    }
}