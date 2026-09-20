using System.Collections.Generic;
using DropAwayPrototype.Editor;
using DropAwayPrototype.Runtime;
using NUnit.Framework;
using UnityEngine;

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
                Object.DestroyImmediate(host);
            }
        }
    }
}
