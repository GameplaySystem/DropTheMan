using UnityEditor;
using UnityEngine;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Editor-only SceneView wrapper for the scene-based Drop The Man authoring shell.
    /// The attached controller remains scene-safe and lives outside the Editor assembly.
    /// </summary>
    [CustomEditor(typeof(DropTheManEditorBoardController))]
    public sealed class DropTheManEditorBoardControllerEditor : UnityEditor.Editor
    {
        private DropTheManEditorBoardController Controller =>
            (DropTheManEditorBoardController)target;

        private void OnSceneGUI()
        {
            if (Controller == null || Application.isPlaying)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (Controller.ShowSceneOverlay)
            {
                DrawOverlay();
            }

            if (!ShouldCaptureSceneInput(currentEvent))
            {
                return;
            }

            if (currentEvent.type == EventType.Layout ||
                currentEvent.type == EventType.MouseMove ||
                currentEvent.type == EventType.MouseDown ||
                currentEvent.type == EventType.MouseUp ||
                currentEvent.type == EventType.MouseDrag ||
                currentEvent.type == EventType.ScrollWheel)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (HandleModeHotkeys(currentEvent) ||
                HandleColorHotkeys(currentEvent) ||
                HandleHolePaletteCycling(currentEvent) ||
                HandlePlacementInput(currentEvent))
            {
                EditorUtility.SetDirty(Controller);
                SceneView.RepaintAll();
            }
        }

        private bool ShouldCaptureSceneInput(Event currentEvent)
        {
            if (!Controller.CaptureSceneInputWhenSelected ||
                EditorGUIUtility.editingTextField ||
                currentEvent.alt ||
                Selection.activeTransform == null)
            {
                return false;
            }

            return Selection.activeTransform == Controller.transform ||
                   Selection.activeTransform.IsChildOf(Controller.transform);
        }

        private void DrawOverlay()
        {
            Handles.BeginGUI();

            Rect panelRect = new(12f, 12f, 320f, 92f);
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);

            Rect swatchRect = new(panelRect.x + 10f, panelRect.y + 40f, 22f, 22f);
            EditorGUI.DrawRect(swatchRect, Controller.ResolveSelectedColorPreview());

            GUI.Label(
                new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 20f, 18f),
                "Drop The Man Editor",
                EditorStyles.boldLabel);
            GUI.Label(
                new Rect(panelRect.x + 40f, panelRect.y + 38f, panelRect.width - 50f, 18f),
                $"Mode: {Controller.CurrentMode}    Color: {Controller.SelectedColor}",
                EditorStyles.label);
            GUI.Label(
                new Rect(panelRect.x + 10f, panelRect.y + 62f, panelRect.width - 20f, 18f),
                $"Hole palette: {Controller.ResolveSelectedHolePaletteDisplayName()}",
                EditorStyles.label);

            Handles.EndGUI();
        }

        private bool HandleModeHotkeys(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return false;
            }

            if (currentEvent.keyCode == KeyCode.O)
            {
                Undo.RecordObject(Controller, "Drop The Man Editor Mode");
                Controller.SetPlacementMode(DropTheManEditorPlacementMode.Obstacle);
                currentEvent.Use();
                return true;
            }

            if (currentEvent.keyCode == KeyCode.M)
            {
                Undo.RecordObject(Controller, "Drop The Man Editor Mode");
                Controller.SetPlacementMode(DropTheManEditorPlacementMode.Stickman);
                currentEvent.Use();
                return true;
            }

            if (currentEvent.keyCode == KeyCode.H)
            {
                Undo.RecordObject(Controller, "Drop The Man Editor Mode");
                Controller.SetPlacementMode(DropTheManEditorPlacementMode.Hole);
                currentEvent.Use();
                return true;
            }

            if (currentEvent.keyCode == KeyCode.R)
            {
                Undo.RecordObject(Controller, "Drop The Man Editor Mode");
                Controller.SetPlacementMode(DropTheManEditorPlacementMode.HoleRotation);
                currentEvent.Use();
                return true;
            }

            return false;
        }

        private bool HandleColorHotkeys(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown ||
                !DropTheManEditorBoardController.TryMapKeyCodeToColorSlot(
                    currentEvent.keyCode,
                    out ColorIdentity colorIdentity))
            {
                return false;
            }

            Undo.RecordObject(Controller, "Drop The Man Editor Color");
            Controller.SetSelectedColor(colorIdentity);
            currentEvent.Use();
            return true;
        }

        private bool HandleHolePaletteCycling(Event currentEvent)
        {
            if (Controller.CurrentMode != DropTheManEditorPlacementMode.Hole ||
                currentEvent.type != EventType.ScrollWheel)
            {
                return false;
            }

            Undo.RecordObject(Controller, "Drop The Man Editor Hole Palette");
            Controller.CycleHolePalette(currentEvent.delta.y > 0f ? 1 : -1);
            currentEvent.Use();
            return true;
        }

        private bool HandlePlacementInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.MouseDown ||
                (currentEvent.button != 0 && currentEvent.button != 1))
            {
                return false;
            }

            if (!Controller.TryGetCellFromRay(
                    HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition),
                    out Vector2Int coordinate))
            {
                return false;
            }

            Undo.RecordObject(Controller, "Drop The Man Editor Placement");
            bool changed = currentEvent.button == 0
                ? Controller.ApplyPrimaryActionAt(coordinate)
                : Controller.ApplyEraseActionAt(coordinate);
            if (!changed)
            {
                return false;
            }

            Controller.RefreshVisuals();
            currentEvent.Use();
            return true;
        }
    }
}
