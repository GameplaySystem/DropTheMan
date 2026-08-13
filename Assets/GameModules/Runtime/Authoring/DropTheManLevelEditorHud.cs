using System.Globalization;
using DropAwayPrototype.Runtime;
using UnityEngine;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Lightweight play-mode HUD for the dedicated Drop The Man level editor scene.
    /// It exposes basic authored metadata and mode feedback without becoming a gameplay UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManLevelEditorHud : MonoBehaviour
    {
        private readonly Rect _panelRect = new(12f, 12f, 430f, 420f);

        private DropTheManLevelEditorRuntimeController _runtimeController;
        private DropTheManEditorBoardController _boardController;
        private Vector2 _scrollPosition;
        private string _levelIdInput = string.Empty;
        private string _displayNameInput = string.Empty;
        private string _boardWidthInput = "5";
        private string _boardHeightInput = "5";
        private bool _timerEnabledInput;
        private string _timerDurationInput = "60";
        private string _timerWarningInput = "0";
        private string _exportPathInput = string.Empty;
        private string _lastSuggestedExportPath = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _fieldsInitialized;

        public void Bind(
            DropTheManLevelEditorRuntimeController runtimeController,
            DropTheManEditorBoardController boardController)
        {
            _runtimeController = runtimeController;
            _boardController = boardController;

            if (!_fieldsInitialized)
            {
                SyncEditableFieldsFromBoard();
            }
        }

        public void SyncEditableFieldsFromBoard()
        {
            if (_boardController == null || _boardController.LevelData == null)
            {
                return;
            }

            DropTheManDevLevelData levelData = _boardController.LevelData;
            _levelIdInput =
                DropTheManEditorJsonExportUtility.BuildLevelIdFieldDisplayValue(
                    levelData.LevelId);
            _displayNameInput = levelData.DisplayName ?? string.Empty;
            _boardWidthInput = levelData.BoardWidth.ToString(CultureInfo.InvariantCulture);
            _boardHeightInput = levelData.BoardHeight.ToString(CultureInfo.InvariantCulture);
            _timerEnabledInput = levelData.TimerEnabled;
            _timerDurationInput =
                levelData.TimerDurationSeconds.ToString(CultureInfo.InvariantCulture);
            _timerWarningInput =
                levelData.TimerWarningThresholdSeconds.ToString(CultureInfo.InvariantCulture);
            RefreshSuggestedExportPath();
            _fieldsInitialized = true;
        }

        public bool IsPointerOverHud(Vector2 screenPosition)
        {
            Vector2 guiPoint = new(screenPosition.x, Screen.height - screenPosition.y);
            return _panelRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_boardController == null)
            {
                GUILayout.BeginArea(_panelRect, GUI.skin.window);
                GUILayout.Label("Drop The Man Level Editor");
                GUILayout.Label("Board controller not found.");
                GUILayout.EndArea();
                return;
            }

            if (!_fieldsInitialized)
            {
                SyncEditableFieldsFromBoard();
            }

            DropTheManDevLevelData levelData = _boardController.LevelData;

            GUILayout.BeginArea(_panelRect, GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.Width(_panelRect.width - 8f),
                GUILayout.Height(_panelRect.height - 28f));
            GUILayout.Label("Drop The Man Level Editor");
            GUILayout.Space(4f);
            GUILayout.Label(
                $"Mode: {_boardController.CurrentMode}    Color: {_boardController.SelectedColor}");
            GUILayout.Label(
                $"Hole palette: {_boardController.ResolveSelectedHolePaletteDisplayName()} ({_boardController.ResolveHolePaletteEntryCount()} configured)");
            GUILayout.Label(
                $"Rotation: {_boardController.SelectedHoleRotationDegrees} deg    Holes: {levelData.Holes.Count}    Stickmen: {levelData.Stickmen.Count}");
            GUILayout.Space(8f);

            GUILayout.Label("Level Id");
            string nextLevelIdInput = GUILayout.TextField(_levelIdInput);
            if (!string.Equals(nextLevelIdInput, _levelIdInput, System.StringComparison.Ordinal))
            {
                _levelIdInput = nextLevelIdInput;
                RefreshSuggestedExportPath();
            }

            GUILayout.Label("Display Name");
            _displayNameInput = GUILayout.TextField(_displayNameInput);

            GUILayout.Space(6f);
            GUILayout.Label("Board Size");
            GUILayout.BeginHorizontal();
            GUILayout.Label("W", GUILayout.Width(18f));
            _boardWidthInput = GUILayout.TextField(_boardWidthInput, GUILayout.Width(56f));
            GUILayout.Label("H", GUILayout.Width(18f));
            _boardHeightInput = GUILayout.TextField(_boardHeightInput, GUILayout.Width(56f));
            if (GUILayout.Button("Apply Board", GUILayout.Width(110f)))
            {
                ApplyBoardSize();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            _timerEnabledInput = GUILayout.Toggle(_timerEnabledInput, "Timer Enabled");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Duration", GUILayout.Width(56f));
            _timerDurationInput = GUILayout.TextField(_timerDurationInput, GUILayout.Width(80f));
            GUILayout.Label("Warn", GUILayout.Width(40f));
            _timerWarningInput = GUILayout.TextField(_timerWarningInput, GUILayout.Width(80f));
            if (GUILayout.Button("Apply Info", GUILayout.Width(110f)))
            {
                ApplyMetadataAndTimer();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Hotkeys: O obstacle, M stickman, H hole, R rotate, 0-9 color");
            GUILayout.Label("Mouse: Left place, Right erase, Wheel cycle hole palette");
            GUILayout.Label("This scene authors level data only. Gameplay play/test is separate.");

            GUILayout.Space(8f);
            GUILayout.Label("JSON Path");
            _exportPathInput = GUILayout.TextField(_exportPathInput);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load / Import JSON", GUILayout.Width(160f)))
            {
                ImportJson();
            }

            if (GUILayout.Button("Save / Export JSON", GUILayout.Width(160f)))
            {
                ExportJson();
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUILayout.Space(6f);
                GUILayout.Label(_statusMessage);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ApplyBoardSize()
        {
            if (!int.TryParse(
                    _boardWidthInput,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int width) ||
                !int.TryParse(
                    _boardHeightInput,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int height))
            {
                _statusMessage = "Board width and height must be valid integers.";
                return;
            }

            bool changed = _boardController.ApplyBoardDimensions(width, height);
            SyncEditableFieldsFromBoard();
            _statusMessage = changed
                ? $"Board resized to {_boardController.LevelData.BoardWidth} x {_boardController.LevelData.BoardHeight}."
                : "Board size unchanged.";
        }

        private void ApplyMetadataAndTimer()
        {
            if (TryApplyMetadataAndTimer(out string successMessage))
            {
                _statusMessage = successMessage;
            }
        }

        private bool TryApplyMetadataAndTimer(out string successMessage)
        {
            if (string.IsNullOrWhiteSpace(_levelIdInput))
            {
                _statusMessage = "Level Id cannot be empty.";
                successMessage = string.Empty;
                return false;
            }

            if (string.IsNullOrWhiteSpace(_displayNameInput))
            {
                _statusMessage = "Display Name cannot be empty.";
                successMessage = string.Empty;
                return false;
            }

            if (!float.TryParse(
                    _timerDurationInput,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float durationSeconds) ||
                !float.TryParse(
                    _timerWarningInput,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float warningThresholdSeconds))
            {
                _statusMessage = "Timer fields must be valid numbers.";
                successMessage = string.Empty;
                return false;
            }

            _boardController.SetLevelMetadata(_levelIdInput, _displayNameInput);
            _boardController.SetTimerSettings(
                _timerEnabledInput,
                durationSeconds,
                warningThresholdSeconds);
            SyncEditableFieldsFromBoard();
            successMessage = "Level metadata and timer fields applied.";
            return true;
        }

        private void ImportJson()
        {
            if (_boardController == null)
            {
                _statusMessage = "Board controller not found.";
                return;
            }

            RefreshSuggestedExportPath();

            if (!_boardController.TryImportLevelFromPath(
                    _exportPathInput,
                    out string resolvedAbsolutePath,
                    out string failureReason))
            {
                _statusMessage = failureReason;
                Debug.LogWarning(failureReason, _boardController);
                return;
            }

            SyncEditableFieldsFromBoard();
            _statusMessage = $"Imported JSON from {resolvedAbsolutePath}";
            Debug.Log(
                $"Drop The Man editor imported JSON from '{resolvedAbsolutePath}'.",
                _boardController);
        }

        private void ExportJson()
        {
            if (_boardController == null)
            {
                _statusMessage = "Board controller not found.";
                return;
            }

            if (!TryApplyMetadataAndTimer(out _))
            {
                return;
            }

            if (!_boardController.TryExportCurrentLevel(
                    _exportPathInput,
                    out string resolvedAbsolutePath,
                    out _,
                    out string failureReason))
            {
                _statusMessage = failureReason;
                Debug.LogWarning(failureReason, _boardController);
                return;
            }

            _statusMessage = $"Exported JSON to {resolvedAbsolutePath}";
            Debug.Log(
                $"Drop The Man editor exported JSON to '{resolvedAbsolutePath}'.",
                _boardController);
        }

        private void RefreshSuggestedExportPath()
        {
            string suggestedPath =
                DropTheManEditorJsonExportUtility.BuildSuggestedProjectRelativeExportPath(
                    _levelIdInput);
            if (string.IsNullOrWhiteSpace(_exportPathInput) ||
                _exportPathInput == _lastSuggestedExportPath)
            {
                _exportPathInput = suggestedPath;
            }

            _lastSuggestedExportPath = suggestedPath;
        }
    }
}
