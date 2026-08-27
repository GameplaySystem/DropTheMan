using System;
using PuzzleFramework.Presentation;
using UnityEngine;
using UnityEngine.Serialization;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Minimal spawned presentation adapter for a Drop The Man hole.
    /// This component exposes authored runtime id and transform/collider hooks only.
    /// Gameplay rules remain owned by runtime services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManHoleView : MonoBehaviour, IDropTheManHoleView
    {
        [SerializeField] private string runtimeId = string.Empty;
        [FormerlySerializedAs("selectionCollider")]
        [SerializeField, HideInInspector] private Collider legacySelectionCollider;
        [SerializeField] private Collider[] selectionColliders;
        [SerializeField] private bool isSelectable = true;
        [SerializeField] private bool preserveWorldYOnApply = true;
        [SerializeField] private bool hideRenderersWhenNotSelectable = true;
        [SerializeField] private Renderer[] renderersToHideWhenNotSelectable;
        [SerializeField] private bool destroySpawnedViewOnCompleted = true;
        [SerializeField, Min(0.01f)] private float spawnedViewScaleMultiplier = 0.88f;
        [SerializeField] private DropTheManHolePresentation completionPresentation;

        private bool _isSpawnedClone;
        private bool _hasCapturedPrefabScale;
        private bool _completionPresentationStarted;
        private Vector3 _prefabLocalScale;

        public string RuntimeId => runtimeId;
        public Vector3 WorldPosition => transform.position;
        public bool IsSelectable => isSelectable && isActiveAndEnabled;

        private void Awake()
        {
            CapturePrefabScale();

            EnsureSelectionColliders();

            if (renderersToHideWhenNotSelectable == null || renderersToHideWhenNotSelectable.Length == 0)
            {
                renderersToHideWhenNotSelectable = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (completionPresentation == null)
            {
                TryGetComponent(out completionPresentation);
            }

            ApplySelectableState();
        }

        /// <summary>
        /// Disables interaction, then starts the optional concrete completion presentation.
        /// Missing or invalid presentation invokes the callback immediately so visuals cannot
        /// block gameplay completion.
        /// </summary>
        public bool TryPlayCompletionPresentation(
            Action completionCallback,
            out string failureReason)
        {
            if (_completionPresentationStarted)
            {
                failureReason =
                    $"Hole view '{runtimeId}' already started completion presentation.";
                return false;
            }

            _completionPresentationStarted = true;
            isSelectable = false;
            ApplyInteractionState();

            if (completionPresentation == null)
            {
                completionCallback?.Invoke();
                failureReason = string.Empty;
                return true;
            }

            if (completionPresentation.TryPlayCompletion(
                    completionCallback,
                    out string presentationFailureReason))
            {
                failureReason = string.Empty;
                return true;
            }

            Debug.LogWarning(
                $"Hole view '{runtimeId}' is using immediate completion fallback: {presentationFailureReason}",
                this);
            completionCallback?.Invoke();
            failureReason = string.Empty;
            return true;
        }

        public void ApplyWorldPosition(Vector3 worldPosition)
        {
            if (preserveWorldYOnApply)
            {
                worldPosition.y = transform.position.y;
            }

            transform.position = worldPosition;
        }

        public void SetSelectable(bool selectable)
        {
            isSelectable = selectable;

            if (_isSpawnedClone && !selectable && destroySpawnedViewOnCompleted)
            {
                Destroy(gameObject);
                return;
            }

            ApplySelectableState();
        }

        public void ConfigureSpawnedView(
            string newRuntimeId,
            Vector3 worldPosition,
            ColorIdentity colorIdentity)
        {
            runtimeId = newRuntimeId;
            _isSpawnedClone = true;
            _completionPresentationStarted = false;
            ApplyWorldPosition(worldPosition);
            ApplySpawnedScale();
            isSelectable = true;
            EnsurePresentationTargets();
            DropTheManViewPresentationUtility.ApplyColor(
                renderersToHideWhenNotSelectable,
                colorIdentity);
            ApplySelectableState();
        }

        private void ApplySelectableState()
        {
            ApplyInteractionState();

            if (!hideRenderersWhenNotSelectable || renderersToHideWhenNotSelectable == null)
            {
                return;
            }

            for (int i = 0; i < renderersToHideWhenNotSelectable.Length; i++)
            {
                if (renderersToHideWhenNotSelectable[i] != null)
                {
                    renderersToHideWhenNotSelectable[i].enabled = isSelectable;
                }
            }
        }

        private void ApplyInteractionState()
        {
            EnsureSelectionColliders();

            for (int i = 0; i < selectionColliders.Length; i++)
            {
                if (selectionColliders[i] != null)
                {
                    selectionColliders[i].enabled = isSelectable;
                }
            }
        }

        private void EnsurePresentationTargets()
        {
            CapturePrefabScale();

            EnsureSelectionColliders();

            if (renderersToHideWhenNotSelectable == null ||
                renderersToHideWhenNotSelectable.Length == 0)
            {
                renderersToHideWhenNotSelectable =
                    GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (completionPresentation == null)
            {
                TryGetComponent(out completionPresentation);
            }
        }

        private void EnsureSelectionColliders()
        {
            if (HasConfiguredSelectionCollider())
            {
                return;
            }

            if (legacySelectionCollider != null)
            {
                selectionColliders = new[] { legacySelectionCollider };
                return;
            }

            selectionColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        private bool HasConfiguredSelectionCollider()
        {
            if (selectionColliders == null)
            {
                return false;
            }

            for (int i = 0; i < selectionColliders.Length; i++)
            {
                if (selectionColliders[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void CapturePrefabScale()
        {
            if (_hasCapturedPrefabScale)
            {
                return;
            }

            _prefabLocalScale = transform.localScale;
            _hasCapturedPrefabScale = true;
        }

        private void ApplySpawnedScale()
        {
            CapturePrefabScale();
            transform.localScale =
                _prefabLocalScale * Mathf.Max(0.01f, spawnedViewScaleMultiplier);
        }
    }

    internal static class DropTheManViewPresentationUtility
    {
        private static readonly Color[] SlotColors =
        {
            new(0.89f, 0.25f, 0.24f, 1f),
            new(0.17f, 0.48f, 0.92f, 1f),
            new(0.17f, 0.67f, 0.34f, 1f),
            new(0.92f, 0.74f, 0.17f, 1f),
            new(0.86f, 0.38f, 0.13f, 1f),
            new(0.55f, 0.29f, 0.74f, 1f),
            new(0.15f, 0.69f, 0.74f, 1f),
            new(0.90f, 0.44f, 0.64f, 1f),
            new(0.47f, 0.62f, 0.20f, 1f),
            new(0.36f, 0.36f, 0.40f, 1f)
        };

        public static void ApplyColor(Renderer[] renderers, ColorIdentity colorIdentity)
        {
            if (renderers == null)
            {
                return;
            }

            Color fallbackColor = ResolveSlotColor(colorIdentity);
            MaterialPropertyBlock propertyBlock = new();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                propertyBlock.Clear();
                propertyBlock.SetColor("_BaseColor", fallbackColor);
                propertyBlock.SetColor("_Color", fallbackColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static Color ResolveSlotColor(ColorIdentity colorIdentity)
        {
            if (colorIdentity == ColorIdentity.None)
            {
                return SlotColors[0];
            }

            int rawValue = Mathf.Clamp(
                (int)colorIdentity - (int)ColorIdentity.Slot0,
                0,
                SlotColors.Length - 1);
            return SlotColors[rawValue];
        }
    }
}
