#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace GSPAWN
{
    // Mirrors RLD's canonical and semantic-name-family matching rules.
    internal static class SpawnActionContextMatcher
    {
        public static bool matchesNames(string recordedName, string targetName)
        {
            return MatchesFallbackName(BuildCanonicalName(recordedName), recordedName, targetName);
        }

        public static string getDisplayName(string name)
        {
            return GetDisplayName(name);
        }

        public static string GetDisplayName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return string.Empty;

            int end = GetComparableNameEnd(objectName);
            int start = 0;
            while (start < end && char.IsWhiteSpace(objectName[start]))
                ++start;

            return start < end ? objectName.Substring(start, end - start) : string.Empty;
        }

        public static string BuildCanonicalName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return string.Empty;

            int end = GetComparableNameEnd(objectName);
            StringBuilder builder = new StringBuilder(end);
            for (int i = 0; i < end; ++i)
            {
                char character = objectName[i];
                if (char.IsLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        public static bool MatchesCanonicalName(string canonicalName, string objectName)
        {
            if (string.IsNullOrEmpty(canonicalName) || string.IsNullOrEmpty(objectName))
                return false;

            int end = GetComparableNameEnd(objectName);
            int canonicalIndex = 0;
            for (int i = 0; i < end; ++i)
            {
                char character = objectName[i];
                if (!char.IsLetterOrDigit(character))
                    continue;

                if (canonicalIndex >= canonicalName.Length ||
                    canonicalName[canonicalIndex] != char.ToLowerInvariant(character))
                    return false;

                ++canonicalIndex;
            }

            return canonicalIndex == canonicalName.Length;
        }

        public static bool MatchesNameFamily(string nameA, string nameB)
        {
            return MatchesNameFamilyInternal(nameA, nameB);
        }

        static bool MatchesFallbackName(string recordedCanonicalName, string recordedName, string targetName)
        {
            if (MatchesCanonicalName(recordedCanonicalName, targetName))
                return true;

            return MatchesNameFamilyInternal(recordedName, targetName);
        }

        static bool MatchesNameFamilyInternal(string nameA, string nameB)
        {
            if (string.IsNullOrEmpty(nameA) || string.IsNullOrEmpty(nameB))
                return false;

            int endA = GetComparableNameEnd(nameA);
            int endB = GetComparableNameEnd(nameB);
            int canonicalLengthA = GetCanonicalNameLength(nameA, endA);
            int canonicalLengthB = GetCanonicalNameLength(nameB, endB);
            if (canonicalLengthA == 0 || canonicalLengthB == 0)
                return false;

            int indexA                          = 0;
            int indexB                          = 0;
            int sharedCharacterCount            = 0;
            int sharedCharacterCountBeforeToken = 0;
            bool hasAlignedTokenStart           = false;
            while (true)
            {
                bool hasCharacterA = TryGetNextComparableCharacter(nameA, endA, ref indexA,
                    out char characterA, out bool boundaryA);
                bool hasCharacterB = TryGetNextComparableCharacter(nameB, endB, ref indexB,
                    out char characterB, out bool boundaryB);

                if (!hasCharacterA || !hasCharacterB)
                {
                    if (!hasCharacterA && !hasCharacterB)
                        return sharedCharacterCount != 0;
                    if (sharedCharacterCount == 0)
                        return false;

                    return !hasCharacterA ? boundaryB : boundaryA;
                }

                if (characterA != characterB)
                {
                    int familyCharacterCount = sharedCharacterCount;
                    if (!boundaryA || !boundaryB)
                    {
                        if (!hasAlignedTokenStart)
                            return false;

                        familyCharacterCount = sharedCharacterCountBeforeToken;
                    }

                    if (familyCharacterCount < 4)
                        return false;

                    int shorterLength = Mathf.Min(canonicalLengthA, canonicalLengthB);
                    return familyCharacterCount * 5 >= shorterLength * 3;
                }

                if (boundaryA || boundaryB)
                {
                    if (boundaryA && boundaryB)
                    {
                        sharedCharacterCountBeforeToken = sharedCharacterCount;
                        hasAlignedTokenStart             = true;
                    }
                    else hasAlignedTokenStart = false;
                }

                ++sharedCharacterCount;
            }
        }

        static int GetCanonicalNameLength(string name, int end)
        {
            int length = 0;
            for (int i = 0; i < end; ++i)
            {
                if (char.IsLetterOrDigit(name[i]))
                    ++length;
            }

            return length;
        }

        static bool TryGetNextComparableCharacter(string name, int end, ref int index,
            out char character, out bool tokenBoundary)
        {
            bool skippedSeparator = false;
            while (index < end && !char.IsLetterOrDigit(name[index]))
            {
                skippedSeparator = true;
                ++index;
            }

            if (index >= end)
            {
                character     = '\0';
                tokenBoundary = false;
                return false;
            }

            char sourceCharacter = name[index];
            tokenBoundary = index == 0 || skippedSeparator;
            if (!tokenBoundary && index > 0)
            {
                char previousCharacter = name[index - 1];
                bool digitBoundary = char.IsDigit(sourceCharacter) != char.IsDigit(previousCharacter);
                bool camelCaseBoundary = char.IsUpper(sourceCharacter) && char.IsLower(previousCharacter);
                bool acronymBoundary = false;

                if (char.IsUpper(sourceCharacter) && char.IsUpper(previousCharacter) && index + 1 < end)
                    acronymBoundary = char.IsLower(name[index + 1]);

                tokenBoundary = digitBoundary || camelCaseBoundary || acronymBoundary;
            }

            character = char.ToLowerInvariant(sourceCharacter);
            ++index;
            return true;
        }

        static int GetComparableNameEnd(string objectName)
        {
            int end = objectName != null ? objectName.Length : 0;
            TrimTrailingWhitespace(objectName, ref end);

            bool strippedSuffix;
            do
            {
                strippedSuffix = false;

                const string cloneSuffix = "(Clone)";
                if (EndsWith(objectName, end, cloneSuffix))
                {
                    end -= cloneSuffix.Length;
                    strippedSuffix = true;
                }
                else
                if (end > 2 && objectName[end - 1] == ')')
                {
                    int openParen = end - 2;
                    while (openParen >= 0 && objectName[openParen] != '(')
                        --openParen;

                    if (openParen >= 0 && openParen < end - 2)
                    {
                        bool numeric = true;
                        for (int i = openParen + 1; i < end - 1; ++i)
                        {
                            if (!char.IsDigit(objectName[i]))
                            {
                                numeric = false;
                                break;
                            }
                        }

                        if (numeric)
                        {
                            end = openParen;
                            strippedSuffix = true;
                        }
                    }
                }

                TrimTrailingWhitespace(objectName, ref end);
            }
            while (strippedSuffix);

            return end;
        }

        static bool EndsWith(string source, int sourceEnd, string suffix)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(suffix) ||
                sourceEnd < suffix.Length)
                return false;

            int sourceStart = sourceEnd - suffix.Length;
            for (int i = 0; i < suffix.Length; ++i)
            {
                if (char.ToLowerInvariant(source[sourceStart + i]) !=
                    char.ToLowerInvariant(suffix[i]))
                    return false;
            }

            return true;
        }

        static void TrimTrailingWhitespace(string source, ref int end)
        {
            while (end > 0 && char.IsWhiteSpace(source[end - 1]))
                --end;
        }
    }
}
#endif
