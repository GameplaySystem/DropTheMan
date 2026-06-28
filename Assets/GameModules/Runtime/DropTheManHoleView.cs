using UnityEngine;

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

        public string RuntimeId => runtimeId;
        public Vector3 WorldPosition => transform.position;
        public bool IsSelectable => isSelectable && isActiveAndEnabled;

        private void Awake()
        {
            if (selectionCollider == null)
            {
                TryGetComponent(out selectionCollider);
            }

            ApplySelectableState();
        }

        public void ApplyWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        public void SetSelectable(bool selectable)
        {
            isSelectable = selectable;
            ApplySelectableState();
        }

        private void ApplySelectableState()
        {
            if (selectionCollider != null)
            {
                selectionCollider.enabled = isSelectable;
            }
        }
    }
}
