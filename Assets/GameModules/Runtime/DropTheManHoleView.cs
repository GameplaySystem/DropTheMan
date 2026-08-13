using UnityEngine;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Minimal pre-placed scene adapter for a Drop The Man hole.
    /// This component exposes authored runtime id and transform/collider hooks only.
    /// Gameplay rules remain owned by runtime services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManHoleView : MonoBehaviour, IDropTheManHoleView
    {
        [SerializeField] private string runtimeId = string.Empty;
        [SerializeField] private Collider selectionCollider;
        [SerializeField] private bool isSelectable = true;
        [SerializeField] private bool preserveWorldYOnApply = true;
        [SerializeField] private bool hideRenderersWhenNotSelectable = true;
        [SerializeField] private Renderer[] renderersToHideWhenNotSelectable;
        [SerializeField] private bool destroySpawnedViewOnCompleted = true;
        [SerializeField, Min(0.01f)] private float spawnedViewScaleMultiplier = 0.88f;

        private bool _templateHidden;
        private bool _isSpawnedClone;
        private bool _hasCapturedTemplateScale;
        private Vector3 _templateLocalScale;

        public string RuntimeId => runtimeId;
        public Vector3 WorldPosition => transform.position;
        public bool IsSelectable => isSelectable && isActiveAndEnabled;

        private void Awake()
        {
            CaptureTemplateScale();

            if (selectionCollider == null)
            {
                TryGetComponent(out selectionCollider);
            }

            if (renderersToHideWhenNotSelectable == null || renderersToHideWhenNotSelectable.Length == 0)
            {
                renderersToHideWhenNotSelectable = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            ApplySelectableState();
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
            _templateHidden = false;
            _isSpawnedClone = true;
            ApplyWorldPosition(worldPosition);
            ApplySpawnedScale();
            isSelectable = true;
            EnsurePresentationTargets();
            DropTheManViewPresentationUtility.ApplyColor(
                renderersToHideWhenNotSelectable,
                colorIdentity);
            ApplySelectableState();
        }

        public void SetTemplateHidden(bool isHidden)
        {
            _templateHidden = isHidden;
            ApplySelectableState();
        }

        private void ApplySelectableState()
        {
            if (selectionCollider != null)
            {
                selectionCollider.enabled = !_templateHidden && isSelectable;
            }

            if (!hideRenderersWhenNotSelectable || renderersToHideWhenNotSelectable == null)
            {
                return;
            }

            for (int i = 0; i < renderersToHideWhenNotSelectable.Length; i++)
            {
                if (renderersToHideWhenNotSelectable[i] != null)
                {
                    renderersToHideWhenNotSelectable[i].enabled =
                        !_templateHidden && isSelectable;
                }
            }
        }

        private void EnsurePresentationTargets()
        {
            CaptureTemplateScale();

            if (selectionCollider == null)
            {
                TryGetComponent(out selectionCollider);
            }

            if (renderersToHideWhenNotSelectable == null ||
                renderersToHideWhenNotSelectable.Length == 0)
            {
                renderersToHideWhenNotSelectable =
                    GetComponentsInChildren<Renderer>(includeInactive: true);
            }
        }

        private void CaptureTemplateScale()
        {
            if (_hasCapturedTemplateScale)
            {
                return;
            }

            _templateLocalScale = transform.localScale;
            _hasCapturedTemplateScale = true;
        }

        private void ApplySpawnedScale()
        {
            CaptureTemplateScale();
            transform.localScale =
                _templateLocalScale * Mathf.Max(0.01f, spawnedViewScaleMultiplier);
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
