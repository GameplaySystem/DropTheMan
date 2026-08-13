using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Narrow pointer adapter for the first playable Drop The Man scene.
    /// It owns hit-testing and screen-to-world conversion, then calls the runtime controller
    /// through the scene controller once per accepted pointer sample.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManPointerInputAdapter : MonoBehaviour
    {
        private const int MousePointerId = 0;

        [SerializeField] private DropTheManSceneController sceneController;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask holeLayerMask = ~0;
        [SerializeField] private float maxHitDistance = 500f;
        [SerializeField] private Transform boardPlaneOrigin;
        [SerializeField] private Vector3 boardPlaneNormal = Vector3.up;
        [SerializeField] private Vector3 dragWorldOffset;
        [SerializeField, Min(0f)] private float maxDragSpeedUnitsPerSecond = 12f;
        [SerializeField] private bool enableMouseInput = true;

        private bool _isDragging;
        private int _activePointerId = -1;
        private int _lastProcessedDragFrame = -1;
        private Vector3 _activeHoleToPointerWorldOffset;
        private DropTheManHoleView _activeHoleView;

        public bool IsDragging => _isDragging;

        private void Awake()
        {
            if (sceneController == null)
            {
                TryGetComponent(out sceneController);
            }
        }

        private void Update()
        {
            if (!enableMouseInput)
            {
                return;
            }

            if (sceneController == null || !sceneController.InputEnabled)
            {
                ClearLocalPointerState();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                ProcessPointerDown(Input.mousePosition, MousePointerId);
                return;
            }

            if (_isDragging && Input.GetMouseButton(0))
            {
                ProcessPointerDrag(Input.mousePosition, MousePointerId);
                return;
            }

            if (_isDragging && Input.GetMouseButtonUp(0))
            {
                ProcessPointerUp(MousePointerId);
            }
        }

        public void Bind(DropTheManSceneController controller)
        {
            sceneController = controller;
        }

        public bool ProcessPointerDown(Vector2 screenPosition, int pointerId)
        {
            if (!CanAcceptPointer(pointerId, requireActiveDrag: false))
            {
                return false;
            }

            if (!TryHitHoleView(screenPosition, out DropTheManHoleView holeView))
            {
                return false;
            }

            if (!TryScreenToBoardWorld(screenPosition, out Vector3 pointerWorldPosition))
            {
                return false;
            }

            if (!sceneController.TryBeginDrag(holeView))
            {
                return false;
            }

            _activeHoleToPointerWorldOffset = holeView.WorldPosition - pointerWorldPosition;
            _activeHoleView = holeView;
            _isDragging = true;
            _activePointerId = pointerId;
            _lastProcessedDragFrame = -1;
            return true;
        }

        public bool ProcessPointerDrag(Vector2 screenPosition, int pointerId)
        {
            if (!CanAcceptPointer(pointerId, requireActiveDrag: true))
            {
                return false;
            }

            if (_lastProcessedDragFrame == Time.frameCount)
            {
                return false;
            }

            if (!TryScreenToBoardWorld(screenPosition, out Vector3 worldPosition))
            {
                return false;
            }

            Vector3 targetWorldPosition = worldPosition + _activeHoleToPointerWorldOffset;
            if (_activeHoleView != null && maxDragSpeedUnitsPerSecond > 0f)
            {
                float maxDistance =
                    maxDragSpeedUnitsPerSecond * Mathf.Max(Time.deltaTime, 1f / 240f);
                targetWorldPosition = Vector3.MoveTowards(
                    _activeHoleView.WorldPosition,
                    targetWorldPosition,
                    maxDistance);
            }

            _lastProcessedDragFrame = Time.frameCount;
            bool updated = sceneController.TryUpdateDrag(targetWorldPosition);
            if (!updated ||
                !sceneController.InputEnabled ||
                sceneController.RuntimeController == null ||
                !sceneController.HasActiveRuntimeDrag)
            {
                ClearLocalPointerState();
            }

            return updated;
        }

        public void ProcessPointerUp(int pointerId)
        {
            if (!_isDragging || _activePointerId != pointerId)
            {
                return;
            }

            if (sceneController != null && sceneController.InputEnabled)
            {
                sceneController.ReleaseDrag();
            }

            ClearLocalPointerState();
        }

        public void ProcessPointerCancel(int pointerId)
        {
            if (!_isDragging || _activePointerId != pointerId)
            {
                return;
            }

            sceneController?.CancelDrag();
            ClearLocalPointerState();
        }

        public void ClearLocalPointerState()
        {
            _isDragging = false;
            _activePointerId = -1;
            _lastProcessedDragFrame = -1;
            _activeHoleToPointerWorldOffset = default;
            _activeHoleView = null;
        }

        private bool CanAcceptPointer(int pointerId, bool requireActiveDrag)
        {
            if (sceneController == null || !sceneController.InputEnabled)
            {
                return false;
            }

            if (requireActiveDrag)
            {
                return _isDragging && _activePointerId == pointerId;
            }

            return !_isDragging;
        }

        private bool TryHitHoleView(
            Vector2 screenPosition,
            out DropTheManHoleView holeView)
        {
            holeView = null;

            if (inputCamera == null)
            {
                return false;
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxHitDistance,
                    holeLayerMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            holeView = hit.collider.GetComponentInParent<DropTheManHoleView>();
            return holeView != null && holeView.IsSelectable;
        }

        private bool TryScreenToBoardWorld(
            Vector2 screenPosition,
            out Vector3 worldPosition)
        {
            worldPosition = default;

            if (inputCamera == null)
            {
                return false;
            }

            Vector3 normal = boardPlaneNormal.sqrMagnitude > 0f
                ? boardPlaneNormal.normalized
                : Vector3.up;
            Vector3 origin = boardPlaneOrigin != null
                ? boardPlaneOrigin.position
                : Vector3.zero;

            Plane boardPlane = new(normal, origin);
            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!boardPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            worldPosition = ray.GetPoint(enter) + dragWorldOffset;
            return true;
        }
    }
}
