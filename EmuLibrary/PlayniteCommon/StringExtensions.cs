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
            
            // Add CamelCase splitting - insert space before capital letters that aren't at the start
            // and aren't preceded by a capital letter
            newName = Regex.Replace(newName, @"(?<=[a-z])(?=[A-Z])", " ");
            
            // Handle special case like "S P" which should be "SP" (single letters separated by space)
            newName = Regex.Replace(newName, @"\b([A-Za-z])\s([A-Za-z])\b", "$1$2");
            
            newName = RemoveTrademarks(newName);
            // Replace special apostrophe with standard one
            newName = newName.Replace(''', '\'');
            newName = newName.Replace(''', '\'');
            newName = Regex.Replace(newName, @"\[.*?\]", "");
            newName = Regex.Replace(newName, @"\(.*?\)", "");
            newName = Regex.Replace(newName, @"\s*:\s*", ": ");
            newName = Regex.Replace(newName, @"\s+", " ");
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
        /// <summary>
        /// Calculate the Levenshtein distance between two strings.
        /// This measures how many character changes are needed to transform one string into another.
        /// </summary>
        /// <param name="s">First string</param>
        /// <param name="t">Second string</param>
        /// <returns>The edit distance between the strings</returns>
        public static int LevenshteinDistance(this string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            // Normalize case for comparison
            s = s.ToLowerInvariant();
            t = t.ToLowerInvariant();

            // Create two work vectors of integer distances
            int[] v0 = new int[t.Length + 1];
            int[] v1 = new int[t.Length + 1];

            // Initialize v0 (the previous row of distances)
            // This row is A[0][i]: edit distance for an empty s
            // The distance is just the number of characters to delete from t
            for (int i = 0; i <= t.Length; i++)
            {
                v0[i] = i;
            }

            for (int i = 0; i < s.Length; i++)
            {
                // Calculate v1 (current row distances) from the previous row v0
                v1[0] = i + 1;

                // Fill in the rest of the row
                for (int j = 0; j < t.Length; j++)
                {
                    int cost = (s[i] == t[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(
                        Math.Min(v1[j] + 1, v0[j + 1] + 1),
                        v0[j] + cost);
                }

                // Swap v1 (current row) and v0 (previous row) for next iteration
                int[] temp = v0;
                v0 = v1;
                v1 = temp;
            }

            // Return the last value calculated
            return v0[t.Length];
        }
        
        /// <summary>
        /// Determines if two strings are similar using fuzzy matching
        /// </summary>
        /// <param name="s">First string</param>
        /// <param name="t">Second string</param>
        /// <param name="threshold">Similarity threshold (0.0 to 1.0, where 1.0 is exact match)</param>
        /// <returns>True if the strings are considered similar</returns>
        public static bool IsSimilarTo(this string s, string t, double threshold = 0.7)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t))
            {
                return string.IsNullOrEmpty(s) && string.IsNullOrEmpty(t);
            }
            
            // Normalize both strings for better comparison
            string normalized1 = s.NormalizeGameName().ToLowerInvariant();
            string normalized2 = t.NormalizeGameName().ToLowerInvariant();
            
            // If strings are equal after normalization, they're similar
            if (normalized1 == normalized2)
            {
                return true;
            }
            
            // Simple containment check (if one string contains the other)
            if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
            {
                return true;
            }
            
            // Calculate edit distance
            int distance = LevenshteinDistance(normalized1, normalized2);
            
            // Calculate similarity as a value between 0 and 1
            int maxLength = Math.Max(normalized1.Length, normalized2.Length);
            if (maxLength == 0) return true; // Both strings are empty or null
            
            double similarity = 1.0 - ((double)distance / maxLength);
            
            return similarity >= threshold;
        }
    }
}