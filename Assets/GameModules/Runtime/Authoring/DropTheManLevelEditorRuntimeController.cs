using PuzzleFramework.Presentation;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Play-mode authoring input bridge for the dedicated Drop The Man level editor scene.
    /// It owns runtime hotkeys and pointer placement only; authored level data stays on the
    /// board controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManLevelEditorRuntimeController : MonoBehaviour
    {
        [SerializeField] private DropTheManEditorBoardController boardController;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private DropTheManLevelEditorHud hud;
        [SerializeField] private bool blockWorldInputWhenPointerOverHud = true;
        [SerializeField] private bool allowRightClickErase = true;

        public DropTheManEditorBoardController BoardController => boardController;

        public void BindToBoardController(DropTheManEditorBoardController controller)
        {
            boardController = controller;
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            boardController?.SetFramingCamera(worldCamera);
        }

        private void Awake()
        {
            if (boardController == null)
            {
                boardController = GetComponent<DropTheManEditorBoardController>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Start()
        {
            EnsureReferences();
            boardController?.RefreshVisuals();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureReferences();
            if (boardController == null)
            {
                return;
            }

            HandleHotkeys();
            HandlePlacementInput();
        }

        private void EnsureReferences()
        {
            if (boardController == null)
            {
                boardController = GetComponent<DropTheManEditorBoardController>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main != null
                    ? Camera.main
                    : FindFirstObjectByType<Camera>();
            }

            boardController?.SetFramingCamera(worldCamera);

            EnsureHud();
        }

        private void EnsureHud()
        {
            if (hud == null)
            {
                hud = FindFirstObjectByType<DropTheManLevelEditorHud>();
            }

            if (hud == null)
            {
                GameObject hudObject = new("DropTheManLevelEditorHud");
                hud = hudObject.AddComponent<DropTheManLevelEditorHud>();
            }

            hud.Bind(this, boardController);
        }

        private void HandleHotkeys()
        {
            if (WasKeyPressed(KeyCode.O))
            {
                boardController.SetPlacementMode(DropTheManEditorPlacementMode.Obstacle);
            }

            if (WasKeyPressed(KeyCode.M))
            {
                boardController.SetPlacementMode(DropTheManEditorPlacementMode.Stickman);
            }

            if (WasKeyPressed(KeyCode.H))
            {
                boardController.SetPlacementMode(DropTheManEditorPlacementMode.Hole);
            }

            if (WasKeyPressed(KeyCode.R))
            {
                boardController.SetPlacementMode(DropTheManEditorPlacementMode.HoleRotation);
            }

            if (TryGetPressedColorSlot(out ColorIdentity colorIdentity))
            {
                boardController.SetSelectedColor(colorIdentity);
            }

            if (boardController.CurrentMode == DropTheManEditorPlacementMode.Hole &&
                TryGetScrollDelta(out float scrollDelta) &&
                Mathf.Abs(scrollDelta) > 0.01f)
            {
                boardController.CycleHolePalette(scrollDelta > 0f ? -1 : 1);
            }
        }

        private void HandlePlacementInput()
        {
            if (worldCamera == null || !TryGetMouseScreenPosition(out Vector2 screenPosition))
            {
                return;
            }

            if (blockWorldInputWhenPointerOverHud && hud != null && hud.IsPointerOverHud(screenPosition))
            {
                return;
            }

            bool placePressed = WasMouseButtonPressed(0);
            bool erasePressed = allowRightClickErase && WasMouseButtonPressed(1);
            if (!placePressed && !erasePressed)
            {
                return;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!boardController.TryGetCellFromRay(ray, out Vector2Int coordinate))
            {
                return;
            }

            bool changed = placePressed
                ? boardController.ApplyPrimaryActionAt(coordinate)
                : boardController.ApplyEraseActionAt(coordinate);
            if (!changed)
            {
                return;
            }

            boardController.RefreshVisuals();
            hud?.SyncEditableFieldsFromBoard();
        }

        private static bool TryGetPressedColorSlot(out ColorIdentity colorIdentity)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            KeyCode[] legacyKeys =
            {
                KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
                KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
                KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
                KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
            };

            for (int i = 0; i < legacyKeys.Length; i++)
            {
                if (Input.GetKeyDown(legacyKeys[i]) &&
                    DropTheManEditorBoardController.TryMapKeyCodeToColorSlot(
                        legacyKeys[i],
                        out colorIdentity))
                {
                    return true;
                }
            }
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Key[] digitKeys =
                {
                    Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                    Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
                };

                for (int i = 0; i < digitKeys.Length; i++)
                {
                    if (Keyboard.current[digitKeys[i]].wasPressedThisFrame)
                    {
                        colorIdentity = (ColorIdentity)((int)ColorIdentity.Slot0 + i);
                        return true;
                    }
                }

                Key[] keypadKeys =
                {
                    Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
                    Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
                };

                for (int i = 0; i < keypadKeys.Length; i++)
                {
                    if (Keyboard.current[keypadKeys[i]].wasPressedThisFrame)
                    {
                        colorIdentity = (ColorIdentity)((int)ColorIdentity.Slot0 + i);
                        return true;
                    }
                }
            }
#endif

            colorIdentity = ColorIdentity.None;
            return false;
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(keyCode))
            {
                return true;
            }
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
            {
                return false;
            }

            return keyCode switch
            {
                KeyCode.O => Keyboard.current.oKey.wasPressedThisFrame,
                KeyCode.M => Keyboard.current.mKey.wasPressedThisFrame,
                KeyCode.H => Keyboard.current.hKey.wasPressedThisFrame,
                KeyCode.R => Keyboard.current.rKey.wasPressedThisFrame,
                _ => false
            };
#else
            return false;
#endif
        }

        private static bool WasMouseButtonPressed(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(button))
            {
                return true;
            }
#endif

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
            {
                return false;
            }

            return button switch
            {
                0 => Mouse.current.leftButton.wasPressedThisFrame,
                1 => Mouse.current.rightButton.wasPressedThisFrame,
                _ => false
            };
#else
            return false;
#endif
        }

        private static bool TryGetMouseScreenPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            return true;
#else
            screenPosition = default;
            return false;
#endif
        }

        private static bool TryGetScrollDelta(out float scrollDelta)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                scrollDelta = Mouse.current.scroll.ReadValue().y;
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            scrollDelta = Input.mouseScrollDelta.y;
            return true;
#else
            scrollDelta = 0f;
            return false;
#endif
        }
    }
}
