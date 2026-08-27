using System;
using System.Collections.Generic;
using DropAwayPrototype.Runtime;
using PuzzleFramework.Presentation;
using UnityEngine;
using UnityEngine.Serialization;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Prototype-owned visual configuration shared by the Drop The Man authoring and gameplay
    /// composition layers. This stores prefab and palette references, never runtime gameplay state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DropTheManEditorConfig",
        menuName = "Drop Away/Drop The Man Editor Config")]
    public sealed class DropTheManEditorConfig : ScriptableObject
    {
        [Min(1)] public int defaultBoardWidth = 5;
        [Min(1)] public int defaultBoardHeight = 5;
        public ModularBoardCellView editorCellPrefab;
        [FormerlySerializedAs("stickmanPreviewPrefab")]
        public DropTheManStickmanView collectableViewPrefab;
        public Material blockedCellMaterial;
        public List<Material> colorSlotMaterials = new();
        public List<DropTheManEditorHolePaletteEntry> holePaletteEntries = new();
    }

    /// <summary>
    /// Prototype-owned visual palette entry shared by authoring previews and gameplay spawning.
    /// This presentation mapping does not replace or extend runtime payload definitions.
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
