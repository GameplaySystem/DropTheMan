using System;
using System.Collections.Generic;
using PuzzleFramework.Presentation;
using UnityEngine;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Prototype-owned authoring configuration for the scene-based Drop The Man editor shell.
    /// This stores authoring visuals and palette data only; it is not runtime gameplay state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DropTheManEditorConfig",
        menuName = "Drop Away/Drop The Man Editor Config")]
    public sealed class DropTheManEditorConfig : ScriptableObject
    {
        [Min(1)] public int defaultBoardWidth = 5;
        [Min(1)] public int defaultBoardHeight = 5;
        public ModularBoardCellView editorCellPrefab;
        public GameObject stickmanPreviewPrefab;
        public Material blockedCellMaterial;
        public List<Material> colorSlotMaterials = new();
        public List<DropTheManEditorHolePaletteEntry> holePaletteEntries = new();
    }

    /// <summary>
    /// Prototype-owned palette entry describing one hole authoring option.
    /// This is editor data only and does not replace runtime payload definitions.
    /// </summary>
    [Serializable]
    public sealed class DropTheManEditorHolePaletteEntry
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public ColorIdentity defaultColorIdentity = ColorIdentity.Slot0;
        public GameObject previewPrefab;
        public List<Vector2Int> footprintOffsets = new() { Vector2Int.zero };
    }
}
