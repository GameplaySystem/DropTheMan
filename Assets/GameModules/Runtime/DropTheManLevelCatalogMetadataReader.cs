using System;
using System.Globalization;
using PuzzleFramework.Content;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Drop The Man adapter that validates one JSON level and maps its canonical Level N id to
    /// generic framework catalog metadata.
    /// </summary>
    public sealed class DropTheManLevelCatalogMetadataReader : ILevelCatalogMetadataReader
    {
        private const string LevelIdPrefix = "Level ";

        public bool TryReadMetadata(
            TextAsset levelAsset,
            out LevelCatalogMetadata metadata,
            out string failureReason)
        {
            metadata = default;

            if (levelAsset == null)
            {
                failureReason = "Drop The Man level asset is required.";
                return false;
            }

            DropTheManJsonLevelDefinitionProvider provider = new(levelAsset);
            if (!provider.TryGetLevelDefinition(
                    out LevelDefinition levelDefinition,
                    out failureReason))
            {
                return false;
            }

            string levelId = levelDefinition.Metadata.LevelId?.Trim();
            if (!TryParseSequenceNumber(levelId, out int sequenceNumber))
            {
                failureReason =
                    $"Drop The Man catalog level id '{levelId}' must use the canonical " +
                    "'Level N' format with a positive integer.";
                return false;
            }

            metadata = new LevelCatalogMetadata(levelId, sequenceNumber);
            failureReason = string.Empty;
            return true;
        }

        public static bool TryParseSequenceNumber(string levelId, out int sequenceNumber)
        {
            sequenceNumber = 0;
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            string trimmedLevelId = levelId.Trim();
            if (!trimmedLevelId.StartsWith(LevelIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string numberText = trimmedLevelId.Substring(LevelIdPrefix.Length).Trim();
            return int.TryParse(
                       numberText,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out sequenceNumber) &&
                   sequenceNumber > 0;
        }
    }
}
