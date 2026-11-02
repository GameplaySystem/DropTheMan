using System;
using System.Collections.Generic;
using PuzzleFramework.RuntimeFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Authored Canvas adapter for the Drop The Man gameplay scene.
    /// Gameplay and progression decisions remain owned by the bootstrapper and runtime controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManGameplayUiPresenter : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private DropTheManDevSceneBootstrapper bootstrapper;
        [SerializeField] private DropTheManSceneController sceneController;

        [Header("Gameplay HUD")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Button chooseLevelButton;

        [Header("Outcome")]
        [SerializeField] private GameObject outcomePanel;
        [SerializeField] private TMP_Text outcomeTitleText;
        [SerializeField] private TMP_Text outcomeMessageText;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button replayLevelButton;
        [SerializeField] private Button chooseLevelFromOutcomeButton;

        [Header("Level Selection")]
        [SerializeField] private GameObject levelSelectionPanel;
        [SerializeField] private Transform levelListContent;
        [SerializeField] private Button levelButtonTemplate;
        [SerializeField] private Button closeLevelSelectionButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text feedbackText;

        private readonly List<Button> _levelButtons = new();
        private bool _levelSelectionOpenedFromOutcome;
        private GameState _terminalState = GameState.NotStarted;
        private int _displayedRemainingSeconds = -1;

        private void Awake()
        {
            ApplyFallbackFontIfNeeded();
            outcomePanel?.SetActive(false);
            levelSelectionPanel?.SetActive(false);
            SetFeedback(string.Empty);
        }

        private void OnEnable()
        {
            if (bootstrapper == null || sceneController == null)
            {
                Debug.LogError("DTM gameplay UI requires bootstrapper and scene controller references.", this);
                enabled = false;
                return;
            }

            bootstrapper.LegacyGameplayGuiEnabled = false;
            bootstrapper.LevelLoaded += HandleLevelLoaded;
            bootstrapper.TerminalOutcomeAccepted += HandleTerminalOutcome;

            chooseLevelButton?.onClick.AddListener(OpenLevelSelection);
            nextLevelButton?.onClick.AddListener(LoadNextLevel);
            replayLevelButton?.onClick.AddListener(RestartLevel);
            chooseLevelFromOutcomeButton?.onClick.AddListener(OpenLevelSelection);
            closeLevelSelectionButton?.onClick.AddListener(CloseLevelSelection);

            RefreshLevelInformation();
            RebuildLevelSelection();
        }

        private void OnDisable()
        {
            if (bootstrapper != null)
            {
                bootstrapper.LevelLoaded -= HandleLevelLoaded;
                bootstrapper.TerminalOutcomeAccepted -= HandleTerminalOutcome;
            }

            chooseLevelButton?.onClick.RemoveListener(OpenLevelSelection);
            nextLevelButton?.onClick.RemoveListener(LoadNextLevel);
            replayLevelButton?.onClick.RemoveListener(RestartLevel);
            chooseLevelFromOutcomeButton?.onClick.RemoveListener(OpenLevelSelection);
            closeLevelSelectionButton?.onClick.RemoveListener(CloseLevelSelection);

            if (levelSelectionPanel != null && levelSelectionPanel.activeSelf &&
                !HasAcceptedTerminalOutcome())
            {
                sceneController?.EnableInput();
            }
        }

        private void Update()
        {
            RefreshTimer();
        }

        public void OpenLevelSelection()
        {
            if (levelSelectionPanel == null)
            {
                return;
            }

            _levelSelectionOpenedFromOutcome = outcomePanel != null && outcomePanel.activeSelf;
            outcomePanel?.SetActive(false);
            SetFeedback(string.Empty);
            RebuildLevelSelection();
            sceneController.CancelDrag();
            sceneController.DisableInput();
            levelSelectionPanel.SetActive(true);
        }

        public void CloseLevelSelection()
        {
            if (levelSelectionPanel == null)
            {
                return;
            }

            levelSelectionPanel.SetActive(false);
            SetFeedback(string.Empty);

            if (_levelSelectionOpenedFromOutcome && IsTerminalState(_terminalState))
            {
                outcomePanel?.SetActive(true);
            }
            else if (!HasAcceptedTerminalOutcome())
            {
                sceneController.EnableInput();
            }

            _levelSelectionOpenedFromOutcome = false;
        }

        private void HandleLevelLoaded()
        {
            _terminalState = GameState.NotStarted;
            _levelSelectionOpenedFromOutcome = false;
            outcomePanel?.SetActive(false);
            levelSelectionPanel?.SetActive(false);
            SetFeedback(string.Empty);
            RefreshLevelInformation();
            RebuildLevelSelection();
        }

        private void HandleTerminalOutcome(GameState state)
        {
            if (!IsTerminalState(state))
            {
                return;
            }

            _terminalState = state;
            _levelSelectionOpenedFromOutcome = false;
            levelSelectionPanel?.SetActive(false);
            RebuildLevelSelection();

            bool won = state == GameState.Won;
            if (outcomeTitleText != null)
            {
                outcomeTitleText.text = won ? "LEVEL COMPLETE!" : "TIME'S UP!";
            }
            if (outcomeMessageText != null)
            {
                outcomeMessageText.text = won
                    ? "Every cat made it home."
                    : "Give the cats another route.";
            }

            nextLevelButton?.gameObject.SetActive(won);
            outcomePanel?.SetActive(true);
        }

        private void RefreshLevelInformation()
        {
            if (levelText != null)
            {
                string displayName = string.IsNullOrWhiteSpace(bootstrapper.CurrentLevelDisplayName)
                    ? "Loading level..."
                    : bootstrapper.CurrentLevelDisplayName;
                string levelId = bootstrapper.CurrentLevelId;
                levelText.text = string.IsNullOrWhiteSpace(levelId)
                    ? displayName
                    : $"{displayName}\n<size=55%>{levelId}</size>";
            }

            _displayedRemainingSeconds = -1;
            RefreshTimer();
        }

        private void RefreshTimer()
        {
            bool hasTimer = bootstrapper != null && bootstrapper.CurrentLevelHasTimer;
            timerPanel?.SetActive(hasTimer);
            if (!hasTimer || timerText == null)
            {
                return;
            }

            int remainingSeconds = Mathf.Max(
                0,
                Mathf.CeilToInt(bootstrapper.CurrentLevelRemainingSeconds));
            if (remainingSeconds == _displayedRemainingSeconds)
            {
                return;
            }

            _displayedRemainingSeconds = remainingSeconds;
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
            timerText.color = remainingSeconds <= 10
                ? new Color(1f, 0.46f, 0.35f)
                : Color.white;
        }

        private void RebuildLevelSelection()
        {
            ClearLevelButtons();
            if (bootstrapper == null || levelButtonTemplate == null || levelListContent == null)
            {
                return;
            }

            IReadOnlyList<DropTheManLevelSelectionOption> options =
                bootstrapper.GetLevelSelectionOptions();
            for (int i = 0; i < options.Count; i++)
            {
                DropTheManLevelSelectionOption option = options[i];
                Button button = Instantiate(levelButtonTemplate, levelListContent);
                button.name = $"LevelButton_{option.LevelId}";
                button.gameObject.SetActive(true);
                button.interactable = option.CanReplay;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = option.CanReplay
                        ? option.DisplayName
                        : $"{option.DisplayName}  (Locked)";
                }

                string levelId = option.LevelId;
                button.onClick.AddListener(() => ReplayLevel(levelId));
                _levelButtons.Add(button);
            }
        }

        private void ClearLevelButtons()
        {
            for (int i = 0; i < _levelButtons.Count; i++)
            {
                if (_levelButtons[i] != null)
                {
                    _levelButtons[i].gameObject.SetActive(false);
                    Destroy(_levelButtons[i].gameObject);
                }
            }
            _levelButtons.Clear();
        }

        private void LoadNextLevel()
        {
            if (!bootstrapper.TryLoadNextLevel(out string failureReason))
            {
                SetFeedback(failureReason);
            }
        }

        private void RestartLevel()
        {
            if (!bootstrapper.TryRestartCurrentLevel(out string failureReason))
            {
                SetFeedback(failureReason);
            }
        }

        private void ReplayLevel(string levelId)
        {
            if (!bootstrapper.TryReplayLevel(levelId, out string failureReason))
            {
                SetFeedback(failureReason);
            }
        }

        private void SetFeedback(string message)
        {
            if (feedbackText == null)
            {
                return;
            }

            feedbackText.text = message ?? string.Empty;
            feedbackText.gameObject.SetActive(!string.IsNullOrWhiteSpace(feedbackText.text));
        }

        private bool HasAcceptedTerminalOutcome()
        {
            return sceneController != null &&
                sceneController.RuntimeController != null &&
                sceneController.RuntimeController.TerminalOutcomeAccepted;
        }

        private static bool IsTerminalState(GameState state)
        {
            return state == GameState.Won || state == GameState.Lost;
        }

        private void ApplyFallbackFontIfNeeded()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            TMP_FontAsset fontAsset = null;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].font == null)
                {
                    fontAsset ??= TMP_Settings.instance != null
                        ? TMP_Settings.defaultFontAsset
                        : Resources.Load<TMP_FontAsset>(
                            "Fonts & Materials/LiberationSans SDF");
                    if (fontAsset == null)
                    {
                        Debug.LogError("DTM gameplay UI requires a TMP font asset.", this);
                        return;
                    }
                    labels[i].font = fontAsset;
                }
            }
        }
    }
}
