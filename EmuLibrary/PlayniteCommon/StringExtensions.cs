using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace System
{
    public static class StringExtensions
    {
        private static readonly CultureInfo enUSCultInfo = new CultureInfo("en-US", false);

        public static string MD5(this string s)
        {
            using (var provider = System.Security.Cryptography.MD5.Create())
            {
                var builder = new StringBuilder();

                foreach (byte b in provider.ComputeHash(Encoding.UTF8.GetBytes(s)))
                {
                    builder.Append(b.ToString("x2").ToLower());
                }

                return builder.ToString();
            }
        }

        public static string ConvertToSortableName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var newName = name;
            newName = Regex.Replace(newName, @"^the\s+", "", RegexOptions.IgnoreCase);
            newName = Regex.Replace(newName, @"^a\s+", "", RegexOptions.IgnoreCase);
            newName = Regex.Replace(newName, @"^an\s+", "", RegexOptions.IgnoreCase);
            return newName;
        }

        public static string RemoveTrademarks(this string str, string remplacement = "")
        {
            if (str.IsNullOrEmpty())
            {
                return str;
            }

            return Regex.Replace(str, @"[™©®]", remplacement);
        }

        public static bool IsNullOrEmpty(this string source)
        {
            return string.IsNullOrEmpty(source);
        }

        public static bool IsNullOrWhiteSpace(this string source)
        {
            return string.IsNullOrWhiteSpace(source);
        }

        public static string Format(this string source, params object[] args)
        {
            return string.Format(source, args);
        }

        public static string TrimEndString(this string source, string value, StringComparison comp = StringComparison.Ordinal)
        {
            if (!source.EndsWith(value, comp))
            {
                return source;
            }

            return source.Remove(source.LastIndexOf(value, comp));
        }

        public static string ToTileCase(this string source, CultureInfo culture = null)
        {
            if (source.IsNullOrEmpty())
            {
                return source;
            }

            if (culture != null)
            {
                return culture.TextInfo.ToTitleCase(source);
            }
            else
            {
                return enUSCultInfo.TextInfo.ToTitleCase(source);
            }
        }

        public static string NormalizeGameName(this string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var newName = name;
            newName = newName.RemoveTrademarks();
            newName = newName.Replace("_", " ");
            newName = newName.Replace(".", " ");
            newName = RemoveTrademarks(newName);
            newName = newName.Replace('’', '\'');
            
            // Remove brackets and parentheses (often contain release info, version numbers, etc.)
            newName = Regex.Replace(newName, @"\[.*?\]", "");
            newName = Regex.Replace(newName, @"\(.*?\)", "");
            
            // Remove common release group names and scene tags (case-insensitive)
            // Common release groups: RELOADED, CODEX, SKIDROW, PLAZA, CPY, GOG, FitGirl, DODI, etc.
            var releaseGroupPatterns = new[]
            {
                @"\b(RELOADED|CODEX|SKIDROW|PLAZA|CPY|GOG|FitGirl|DODI|KaOs|Xatab|Razor1911|PROPHET|TiNYiSO|ALI213|3DM|REVOLT|HOODLUM|FLT|TENOKE|RUNE|EMPRESS|Goldberg|P2P)\b",
                @"\b(Repack|MULTi|MULTi\d+|MULTi-\d+)\b",
                @"\b(v?\d+\.\d+(\.\d+)?(\.\d+)?)\b", // Version numbers like v1.0, 2.3.4, etc.
                @"\b(Update|Patch|Hotfix|DLC|Expansion|Addon)\s+\d+.*?$", // Update/Patch with numbers
                @"\b(Build|b)\s*\d+.*?$", // Build numbers
                @"\b(Mod|MOD|Enhanced|ENHANCED|Remastered|REMASTERED|Definitive|DEFINITIVE)\b",
                @"\b(Crack|Cracked|Unlocked|UNLOCKED|NoDVD|No-DVD)\b",
                @"\b(Full|FULL)\s*(Game|GAME|Version|VERSION)\b",
                @"\b(Pre|PRE)\s*(Installed|INSTALLED|Cracked|CRACKED)\b",
                @"\b(Install|INSTALL|Setup|SETUP)\s*(Guide|GUIDE|Instructions|INSTRUCTIONS)?\b",
                @"\b(Readme|README|NFO|Info|INFO)\b",
                @"\b(PC|Windows|Win\d+|x86|x64|32bit|64bit)\b", // Platform tags
                @"\b(EN|US|EU|DE|FR|IT|ES|RU|JP|CN|KR)\b", // Language/region codes
                @"\b(Original|ORIGINAL|Clean|CLEAN|Unmodified|UNMODIFIED)\b",
                @"\b(Steam|Epic|Origin|Uplay|Battle\.net)\s*(Rip|RIP|Version|VERSION)?\b",
                @"\b(DRM|DRM-Free|NoDRM|No-DRM)\s*(Free|FREE)?\b",
                @"\b(Online|OFFLINE|Offline)\s*(Mode|MODE|Fix|FIX)?\b",
                @"\b(Size|SIZE)\s*:?\s*\d+.*?$", // Size indicators
                @"\b(GB|MB|TB)\s*:?\s*\d+.*?$", // Size with units
            };
            
            foreach (var pattern in releaseGroupPatterns)
            {
                newName = Regex.Replace(newName, pattern, "", RegexOptions.IgnoreCase);
            }
            
            // Remove common prefixes that might appear at the start
            newName = Regex.Replace(newName, @"^(patch|update|installer|setup|gog\s*installer|disc\s*\d+|cd\s*\d+|dvd\s*\d+)\s+", "", RegexOptions.IgnoreCase);
            
            // Remove common suffixes
            newName = Regex.Replace(newName, @"\s+(patch|update|installer|setup|repack|multii?|cracked|unlocked|mod|enhanced|remastered|definitive|gog|steam|epic)$", "", RegexOptions.IgnoreCase);
            
            // Remove standalone version numbers at the end (e.g., "Game Name 1.2.3")
            newName = Regex.Replace(newName, @"\s+v?\d+\.\d+(\.\d+)?(\.\d+)?$", "", RegexOptions.IgnoreCase);
            
            // Remove year patterns that are likely release years (4 digits, but be careful not to remove game titles with years)
            // Only remove if it's at the end and looks like a release year (1900-2099)
            newName = Regex.Replace(newName, @"\s+(19|20)\d{2}$", "", RegexOptions.IgnoreCase);
            
            // Clean up spacing and formatting
            newName = Regex.Replace(newName, @"\s*:\s*", ": ");
            newName = Regex.Replace(newName, @"\s+", " ");
            
            // Handle "The" at the end
            if (Regex.IsMatch(newName, @",\s*The$"))
            {
                newName = "The " + Regex.Replace(newName, @",\s*The$", "", RegexOptions.IgnoreCase);
            }

            return newName.Trim();
        }

        public static string GetSHA256Hash(this string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        public static string GetPathWithoutAllExtensions(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return Regex.Replace(path, @"(\.[A-Za-z0-9]+)+$", "");
        }

        public static bool Contains(this string str, string value, StringComparison comparisonType)
        {
            return str.IndexOf(value, 0, comparisonType) != -1;
        }

        public static bool ContainsAny(this string str, char[] chars)
        {
            return str.IndexOfAny(chars) >= 0;
        }

        public static bool IsHttpUrl(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            return Regex.IsMatch(str, @"^https?:\/\/", RegexOptions.IgnoreCase);
        }

        // Courtesy of https://stackoverflow.com/questions/6275980/string-replace-ignoring-case
        public static string Replace(this string str, string oldValue, string @newValue, StringComparison comparisonType)
        {
            // Check inputs.
            if (str == null)
            {
                // Same as original .NET C# string.Replace behavior.
                throw new ArgumentNullException(nameof(str));
            }
            if (str.Length == 0)
            {
                // Same as original .NET C# string.Replace behavior.
                return str;
            }
            if (oldValue == null)
            {
                // Same as original .NET C# string.Replace behavior.
                throw new ArgumentNullException(nameof(oldValue));
            }
            if (oldValue.Length == 0)
            {
                // Same as original .NET C# string.Replace behavior.
                throw new ArgumentException("String cannot be of zero length.");
            }

            // Prepare string builder for storing the processed string.
            // Note: StringBuilder has a better performance than String by 30-40%.
            StringBuilder resultStringBuilder = new StringBuilder(str.Length);

            // Analyze the replacement: replace or remove.
            bool isReplacementNullOrEmpty = string.IsNullOrEmpty(@newValue);

            // Replace all values.
            const int valueNotFound = -1;
            int foundAt;
            int startSearchFromIndex = 0;
            while ((foundAt = str.IndexOf(oldValue, startSearchFromIndex, comparisonType)) != valueNotFound)
            {
                // Append all characters until the found replacement.
                int @charsUntilReplacment = foundAt - startSearchFromIndex;
                bool isNothingToAppend = @charsUntilReplacment == 0;
                if (!isNothingToAppend)
                {
                    resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilReplacment);
                }

                // Process the replacement.
                if (!isReplacementNullOrEmpty)
                {
                    resultStringBuilder.Append(@newValue);
                }

                // Prepare start index for the next search.
                // This needed to prevent infinite loop, otherwise method always start search
                // from the start of the string. For example: if an oldValue == "EXAMPLE", newValue == "example"
                // and comparisonType == "any ignore case" will conquer to replacing:
                // "EXAMPLE" to "example" to "example" to "example" … infinite loop.
                startSearchFromIndex = foundAt + oldValue.Length;
                if (startSearchFromIndex == str.Length)
                {
                    // It is end of the input string: no more space for the next search.
                    // The input string ends with a value that has already been replaced.
                    // Therefore, the string builder with the result is complete and no further action is required.
                    return resultStringBuilder.ToString();
                }
            }

            // Append the last part to the result.
            int @charsUntilStringEnd = str.Length - startSearchFromIndex;
            resultStringBuilder.Append(str, startSearchFromIndex, @charsUntilStringEnd);
            return resultStringBuilder.ToString();
        }
    }
}
