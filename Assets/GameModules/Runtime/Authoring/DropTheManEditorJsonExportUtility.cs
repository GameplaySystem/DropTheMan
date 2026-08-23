using System;
using System.IO;
using DropAwayPrototype.Runtime;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Prototype-owned export helper for play-mode authoring JSON.
    /// It reuses the existing Drop The Man JSON schema/export path and only owns file writing.
    /// </summary>
    public static class DropTheManEditorJsonExportUtility
    {
        public const string DefaultExportDirectory =
            "Assets/Resources/DropTheMan/Levels";

        public static string BuildSuggestedProjectRelativeExportPath(string levelId)
        {
            string fileStem = BuildSuggestedFileStem(levelId);
            return $"{DefaultExportDirectory}/{fileStem}.json";
        }

        public static string NormalizeLevelId(string rawLevelId)
        {
            string trimmedLevelId = string.IsNullOrWhiteSpace(rawLevelId)
                ? "Level 1"
                : rawLevelId.Trim();

            return IsDigitsOnly(trimmedLevelId)
                ? $"Level {trimmedLevelId}"
                : trimmedLevelId;
        }

        public static string BuildLevelIdFieldDisplayValue(string storedLevelId)
        {
            string normalizedLevelId = NormalizeLevelId(storedLevelId);
            return TryExtractCanonicalLevelNumber(normalizedLevelId, out string levelNumber)
                ? levelNumber
                : normalizedLevelId;
        }

        public static bool TryBuildJson(
            DropTheManDevLevelData levelData,
            out string jsonText,
            out string failureReason)
        {
            return DropTheManJsonLevelDefinitionProvider.TryExportDevLevelDataToJson(
                levelData,
                out jsonText,
                out failureReason);
        }

        public static bool TryExportToFile(
            DropTheManDevLevelData levelData,
            string requestedPath,
            out string resolvedAbsolutePath,
            out string jsonText,
            out string failureReason)
        {
            resolvedAbsolutePath = string.Empty;

            if (!TryBuildJson(levelData, out jsonText, out failureReason))
            {
                return false;
            }

            string exportPath = string.IsNullOrWhiteSpace(requestedPath)
                ? BuildSuggestedProjectRelativeExportPath(levelData?.LevelId)
                : requestedPath;

            if (!TryResolveAbsolutePath(exportPath, out resolvedAbsolutePath, out failureReason))
            {
                return false;
            }

            try
            {
                string directoryPath = Path.GetDirectoryName(resolvedAbsolutePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(resolvedAbsolutePath, jsonText);
            }
            catch (Exception exception)
            {
                failureReason =
                    $"Drop The Man JSON export could not be written to '{resolvedAbsolutePath}': {exception.Message}";
                return false;
            }

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif

            failureReason = string.Empty;
            return true;
        }

        public static bool TryImportFromFile(
            string requestedPath,
            out DropTheManDevLevelData levelData,
            out string resolvedAbsolutePath,
            out string failureReason)
        {
            levelData = null;
            resolvedAbsolutePath = string.Empty;

            string importPath = string.IsNullOrWhiteSpace(requestedPath)
                ? BuildSuggestedProjectRelativeExportPath("Level 2")
                : requestedPath;

            if (!TryResolveAbsolutePath(importPath, out resolvedAbsolutePath, out failureReason))
            {
                return false;
            }

            if (!File.Exists(resolvedAbsolutePath))
            {
                failureReason =
                    $"Drop The Man JSON import file was not found at '{resolvedAbsolutePath}'.";
                return false;
            }

            string jsonText;
            try
            {
                jsonText = File.ReadAllText(resolvedAbsolutePath);
            }
            catch (Exception exception)
            {
                failureReason =
                    $"Drop The Man JSON import could not read '{resolvedAbsolutePath}': {exception.Message}";
                return false;
            }

            return DropTheManJsonLevelDefinitionProvider.TryImportJsonToDevLevelData(
                jsonText,
                out levelData,
                out failureReason);
        }

        public static string BuildSuggestedFileStem(string levelId)
        {
            string source = NormalizeLevelId(levelId);

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            bool previousWasSeparator = false;
            System.Text.StringBuilder builder = new();

            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (Array.IndexOf(invalidCharacters, character) >= 0)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator)
                {
                    continue;
                }

                builder.Append('_');
                previousWasSeparator = true;
            }

            string fileStem = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(fileStem)
                ? "level_1"
                : fileStem;
        }

        private static bool TryExtractCanonicalLevelNumber(
            string normalizedLevelId,
            out string levelNumber)
        {
            const string Prefix = "Level ";
            if (normalizedLevelId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = normalizedLevelId.Substring(Prefix.Length).Trim();
                if (IsDigitsOnly(suffix))
                {
                    levelNumber = suffix;
                    return true;
                }
            }

            levelNumber = string.Empty;
            return false;
        }

        private static bool IsDigitsOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryResolveAbsolutePath(
            string requestedPath,
            out string absolutePath,
            out string failureReason)
        {
            string normalizedPath = requestedPath.Trim();

            if (Path.IsPathRooted(normalizedPath))
            {
                absolutePath = Path.GetFullPath(normalizedPath);
                failureReason = string.Empty;
                return true;
            }

            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            absolutePath = normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                           normalizedPath.StartsWith(@"Assets\", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFullPath(Path.Combine(projectRoot, normalizedPath))
                : Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));

            failureReason = string.Empty;
            return true;
        }
    }
}
