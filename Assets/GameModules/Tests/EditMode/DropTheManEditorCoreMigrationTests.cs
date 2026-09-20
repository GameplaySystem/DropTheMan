using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DropAwayPrototype.Editor;
using DropAwayPrototype.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropAwayPrototype.Tests.EditMode
{
    public sealed class DropTheManEditorCoreMigrationTests
    {
        [Test]
        public void SharedFitRotationAndCellPickingPreserveEditorActions()
        {
            GameObject host = new("EditorCoreMigrationTest");
            try
            {
                DropTheManEditorBoardController editor =
                    host.AddComponent<DropTheManEditorBoardController>();
                editor.LevelData.Holes.Add(new DropTheManDevHoleData
                {
                    Id = "hole-a",
                    Coordinate = new Vector2Int(1, 1),
                    FootprintOffsets = new List<Vector2Int>
                    {
                        Vector2Int.zero,
                        Vector2Int.right
                    }
                });
                editor.RefreshVisuals();

                editor.SetPlacementMode(DropTheManEditorPlacementMode.Obstacle);
                Assert.IsFalse(editor.ApplyPrimaryActionAt(new Vector2Int(2, 1)));
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(4, 4)));

                editor.SetPlacementMode(DropTheManEditorPlacementMode.HoleRotation);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(1, 1)));
                CollectionAssert.AreEquivalent(new[] { Vector2Int.zero, Vector2Int.down },
                    editor.LevelData.Holes[0].FootprintOffsets);

                Ray ray = new(new Vector3(1f, 5f, 1f), Vector3.down);
                Assert.IsTrue(editor.TryGetCellFromRay(ray, out Vector2Int picked));
                Assert.AreEqual(new Vector2Int(1, 1), picked);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExistingLevelImportsWithCenterAnchoredPickingAndHoleSelection()
        {
            GameObject host = new("ExistingLevelEditorTest");
            try
            {
                DropTheManEditorBoardController editor = host.AddComponent<DropTheManEditorBoardController>();
                string levelPath = Path.Combine(Application.dataPath,
                    "Resources/DropTheMan/Levels/level_1.json");
                Assert.IsTrue(editor.TryImportLevelFromPath(levelPath, out _, out string reason), reason);
                Assert.AreEqual("Level 1", editor.LevelData.LevelId);
                Assert.Greater(editor.LevelData.Holes.Count, 0);
                Assert.Greater(editor.LevelData.Stickmen.Count, 0);
                Assert.AreEqual(25, host.transform.Find("BoardVisuals").childCount);

                Assert.IsTrue(editor.TryGetCellFromRay(new Ray(new Vector3(0.49f, 5f, 0f), Vector3.down),
                    out Vector2Int left));
                Assert.AreEqual(new Vector2Int(0, 0), left);
                Assert.IsTrue(editor.TryGetCellFromRay(new Ray(new Vector3(0.51f, 5f, 0f), Vector3.down),
                    out Vector2Int right));
                Assert.AreEqual(new Vector2Int(1, 0), right);
                Assert.IsFalse(editor.TryGetCellFromRay(new Ray(new Vector3(-0.51f, 5f, 0f), Vector3.down),
                    out _));

                DropTheManDevHoleData hole = editor.LevelData.Holes[0];
                editor.SetPlacementMode(DropTheManEditorPlacementMode.HoleRotation);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(hole.Coordinate));
                Assert.AreEqual(hole.Id, editor.SelectedAuthoredItemId);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase("level_2.json")]
        [TestCase("level_3.json")]
        public void OtherShippedLevelsRestoreIntoTheLiveSession(string fileName)
        {
            GameObject host = new("ShippedLevelEditorTest");
            try
            {
                DropTheManEditorBoardController editor = host.AddComponent<DropTheManEditorBoardController>();
                string levelPath = Path.Combine(Application.dataPath,
                    "Resources/DropTheMan/Levels", fileName);
                Assert.IsTrue(editor.TryImportLevelFromPath(levelPath, out _, out string reason), reason);
                Assert.IsTrue(editor.TryGetCellFromRay(new Ray(new Vector3(0f, 5f, 0f), Vector3.down),
                    out Vector2Int picked));
                Assert.AreEqual(Vector2Int.zero, picked);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void CheckedInEditorSceneRetainsItsConfiguredBoardVisuals()
        {
            string scenePath = "Assets/Scenes/DropTheManLevelEditor.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                DropTheManEditorBoardController editor = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    editor ??= root.GetComponentInChildren<DropTheManEditorBoardController>(true);
                }
                Assert.NotNull(editor);
                Assert.NotNull(editor.Config);
                editor.RefreshVisuals();
                Assert.Greater(editor.transform.Find("BoardVisuals").childCount, 0);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void LiveSessionPlacesBlocksRotatesHolesAndErasesByFootprintCell()
        {
            GameObject host = new("LiveEditorActionsTest");
            DropTheManEditorConfig config = ScriptableObject.CreateInstance<DropTheManEditorConfig>();
            try
            {
                config.holePaletteEntries.Add(new DropTheManEditorHolePaletteEntry
                {
                    id = "two-cells",
                    footprintOffsets = new List<Vector2Int> { Vector2Int.zero, Vector2Int.right }
                });
                DropTheManEditorBoardController editor = host.AddComponent<DropTheManEditorBoardController>();
                typeof(DropTheManEditorBoardController).GetField("config",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(editor, config);
                editor.RefreshVisuals();

                editor.SetPlacementMode(DropTheManEditorPlacementMode.Stickman);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(0, 0)));
                Assert.AreEqual("stickman_001", editor.SelectedAuthoredItemId);
                editor.SetPlacementMode(DropTheManEditorPlacementMode.Hole);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(1, 1)));
                Assert.IsFalse(editor.ApplyPrimaryActionAt(new Vector2Int(0, 0)));
                Assert.AreEqual(1, editor.LevelData.Holes.Count);

                editor.SetPlacementMode(DropTheManEditorPlacementMode.Obstacle);
                Assert.IsFalse(editor.ApplyPrimaryActionAt(new Vector2Int(2, 1)));
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(4, 4)));
                Assert.IsTrue(editor.ApplyEraseActionAt(new Vector2Int(4, 4)));

                editor.SetPlacementMode(DropTheManEditorPlacementMode.HoleRotation);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(2, 1)));
                CollectionAssert.AreEquivalent(new[] { Vector2Int.zero, Vector2Int.down },
                    editor.LevelData.Holes[0].FootprintOffsets);
                Assert.AreEqual(editor.LevelData.Holes[0].Id, editor.SelectedAuthoredItemId);
                Assert.IsTrue(editor.ApplyEraseActionAt(new Vector2Int(1, 0)));
                Assert.IsEmpty(editor.LevelData.Holes);
                Assert.IsNull(editor.SelectedAuthoredItemId);

                editor.SetPlacementMode(DropTheManEditorPlacementMode.Stickman);
                Assert.IsTrue(editor.ApplyEraseActionAt(new Vector2Int(0, 0)));
                Assert.IsEmpty(editor.LevelData.Stickmen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResizePrunesOnlyAffectedContentAndWarns()
        {
            GameObject host = new("EditorResizeTest");
            try
            {
                DropTheManEditorBoardController editor = host.AddComponent<DropTheManEditorBoardController>();
                editor.LevelData.BlockedCells.Add(new Vector2Int(4, 4));
                editor.LevelData.Stickmen.Add(new DropTheManDevStickmanData
                    { Id = "remove-man", Coordinate = new Vector2Int(4, 0) });
                editor.LevelData.Stickmen.Add(new DropTheManDevStickmanData
                    { Id = "keep-man", Coordinate = new Vector2Int(0, 0) });
                editor.LevelData.Holes.Add(new DropTheManDevHoleData
                {
                    Id = "remove-hole", Coordinate = new Vector2Int(3, 1),
                    FootprintOffsets = new List<Vector2Int> { Vector2Int.zero, Vector2Int.right }
                });
                editor.LevelData.Holes.Add(new DropTheManDevHoleData
                    { Id = "keep-hole", Coordinate = new Vector2Int(1, 2) });
                editor.RefreshVisuals();

                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("removed 1 blocked cells, 1 stickmen, and 1 holes"));
                Assert.IsTrue(editor.ApplyBoardDimensions(4, 4));
                Assert.AreEqual(4, editor.LevelData.BoardWidth);
                Assert.AreEqual(4, editor.LevelData.BoardHeight);
                Assert.IsEmpty(editor.LevelData.BlockedCells);
                Assert.AreEqual("keep-man", editor.LevelData.Stickmen[0].Id);
                Assert.AreEqual("keep-hole", editor.LevelData.Holes[0].Id);
                Assert.AreEqual(16, host.transform.Find("BoardVisuals").childCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void ExistingLevelSaveLoadRoundTripAndInvalidImportLeaveSessionIntact()
        {
            GameObject host = new("EditorRoundTripTest");
            string savedPath = Path.Combine(Path.GetTempPath(), $"dtm-editor-{Guid.NewGuid():N}.json");
            string invalidPath = Path.Combine(Path.GetTempPath(), $"dtm-editor-invalid-{Guid.NewGuid():N}.json");
            try
            {
                DropTheManEditorBoardController editor = host.AddComponent<DropTheManEditorBoardController>();
                string levelPath = Path.Combine(Application.dataPath,
                    "Resources/DropTheMan/Levels/level_1.json");
                Assert.IsTrue(editor.TryImportLevelFromPath(levelPath, out _, out string reason), reason);
                int shippedStickmen = editor.LevelData.Stickmen.Count;
                Assert.IsTrue(editor.ApplyBoardDimensions(6, 5));
                editor.SetPlacementMode(DropTheManEditorPlacementMode.Obstacle);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(5, 4)));
                editor.SetPlacementMode(DropTheManEditorPlacementMode.Stickman);
                Assert.IsTrue(editor.ApplyPrimaryActionAt(new Vector2Int(5, 0)));
                Assert.IsTrue(editor.TryExportCurrentLevel(savedPath, out _, out string json,
                    out reason), reason);
                Assert.That(json, Does.Contain("\"FormatVersion\""));
                int originalHoles = editor.LevelData.Holes.Count;
                int originalStickmen = editor.LevelData.Stickmen.Count;
                Assert.AreEqual(shippedStickmen + 1, originalStickmen);
                editor.ApplyBoardDimensions(7, 5);
                Assert.IsTrue(editor.TryImportLevelFromPath(savedPath, out _, out reason), reason);
                Assert.AreEqual(6, editor.LevelData.BoardWidth);
                CollectionAssert.Contains(editor.LevelData.BlockedCells, new Vector2Int(5, 4));
                Assert.AreEqual(originalHoles, editor.LevelData.Holes.Count);
                Assert.AreEqual(originalStickmen, editor.LevelData.Stickmen.Count);

                File.WriteAllText(invalidPath, "{ invalid json");
                Assert.IsFalse(editor.TryImportLevelFromPath(invalidPath, out _, out _));
                Assert.AreEqual(6, editor.LevelData.BoardWidth);
                Assert.AreEqual(originalHoles, editor.LevelData.Holes.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (File.Exists(savedPath)) File.Delete(savedPath);
                if (File.Exists(invalidPath)) File.Delete(invalidPath);
            }
        }
    }
}
