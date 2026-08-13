using UnityEngine;

namespace DropAwayPrototype.Editor
{
    /// <summary>
    /// Scene-safe board cell view used by the visual authoring shell.
    /// It owns only board-cell presentation and never stores gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManEditorCellView : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private Renderer[] targetRenderers;

        private Material[][] _baseSharedMaterials;
        private MaterialPropertyBlock _propertyBlock;

        public Vector2Int Coordinate => coordinate;

        public void Initialize(
            Vector2Int newCoordinate,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 rootScale)
        {
            coordinate = newCoordinate;
            gameObject.name = $"Cell_{coordinate.x}_{coordinate.y}";
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = rootScale;

            EnsureRenderers();
        }

        public void ApplyVisuals(
            bool isBlocked,
            Color activeColor,
            Color blockedColor,
            Material blockedMaterial)
        {
            EnsureRenderers();

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (isBlocked && blockedMaterial != null)
                {
                    renderer.sharedMaterial = blockedMaterial;
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                if (_baseSharedMaterials != null &&
                    i < _baseSharedMaterials.Length &&
                    _baseSharedMaterials[i] != null &&
                    _baseSharedMaterials[i].Length > 0)
                {
                    renderer.sharedMaterials = _baseSharedMaterials[i];
                }

                _propertyBlock.Clear();
                Color color = isBlocked ? blockedColor : activeColor;
                _propertyBlock.SetColor(BaseColorPropertyId, color);
                _propertyBlock.SetColor(ColorPropertyId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void EnsureRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                CreateFallbackVisual();
            }

            CacheBaseSharedMaterialsIfNeeded();
        }

        private void CreateFallbackVisual()
        {
            Transform existingVisual = transform.Find("CellVisual");
            GameObject visualObject = existingVisual != null
                ? existingVisual.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            visualObject.name = "CellVisual";
            visualObject.transform.SetParent(transform, worldPositionStays: false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = new Vector3(1f, 0.08f, 1f);

            Collider collider = visualObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            targetRenderers = visualObject.GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        private void CacheBaseSharedMaterialsIfNeeded()
        {
            if (_baseSharedMaterials != null &&
                targetRenderers != null &&
                _baseSharedMaterials.Length == targetRenderers.Length)
            {
                return;
            }

            _baseSharedMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                _baseSharedMaterials[i] = renderer != null
                    ? renderer.sharedMaterials
                    : new Material[0];
            }
        }
    }
}
