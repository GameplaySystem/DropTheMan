using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned visual presentation for a concrete hole prefab.
    /// Gameplay lifecycle, capacity, occupancy, and outcome routing remain runtime-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManHolePresentation : MonoBehaviour
    {
        private const string DefaultCloseBlendShapeName = "Close";

        [Header("Prefab Contract")]
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private SkinnedMeshRenderer capRenderer;
        [SerializeField] private string capCloseBlendShapeName = DefaultCloseBlendShapeName;
        [SerializeField] private Renderer stencilApertureRenderer;
        [SerializeField] private Transform[] collectionSockets = Array.Empty<Transform>();

        [Header("Completion Sequence")]
        [SerializeField, Range(0f, 100f)] private float closedCapBlendShapeWeight = 100f;
        [SerializeField, Min(0.01f)] private float capCloseDuration = 0.25f;
        [SerializeField] private Ease capCloseEase = Ease.InOutSine;
        [SerializeField, Min(0f)] private float shrinkDelay;
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.2f;
        [SerializeField] private Ease shrinkEase = Ease.InBack;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool resetOnEnable = true;

        private Sequence _completionSequence;
        private Action _completionCallback;
        private Vector3 _authoredPresentationScale;
        private float _authoredCapBlendShapeWeight;
        private int _capCloseBlendShapeIndex = -1;
        private bool _hasCapturedPresentationScale;
        private bool _hasCapturedCapWeight;
        private readonly Dictionary<string, Transform> _claimedCollectionSockets = new();

        /// <summary>
        /// Presentation targets used later by cat collection animation. Runtime footprint capacity
        /// remains authoritative and must not be derived from this list.
        /// </summary>
        public IReadOnlyList<Transform> CollectionSockets =>
            collectionSockets ?? Array.Empty<Transform>();

        public Renderer StencilApertureRenderer => stencilApertureRenderer;

        public bool IsCompletionPlaying =>
            _completionSequence != null &&
            _completionSequence.IsActive() &&
            _completionSequence.IsPlaying();

        private void Awake()
        {
            CaptureAuthoredState();
        }

        private void OnEnable()
        {
            if (resetOnEnable)
            {
                ResetPresentation();
            }
        }

        private void OnDisable()
        {
            CancelCompletion(resetPresentation: false);
        }

        private void OnDestroy()
        {
            CancelCompletion(resetPresentation: false);
        }

        private void OnValidate()
        {
            capCloseDuration = Mathf.Max(0.01f, capCloseDuration);
            shrinkDelay = Mathf.Max(0f, shrinkDelay);
            shrinkDuration = Mathf.Max(0.01f, shrinkDuration);
            _capCloseBlendShapeIndex = -1;
        }

        /// <summary>
        /// Validates the concrete prefab references without making gameplay changes.
        /// </summary>
        public bool TryValidateConfiguration(out string failureReason)
        {
            if (presentationRoot == null)
            {
                failureReason = "A presentation root is required.";
                return false;
            }

            if (presentationRoot == transform)
            {
                failureReason =
                    "Presentation root must be a child transform so visual scaling does not scale the logical hole root.";
                return false;
            }

            if (capRenderer == null || capRenderer.sharedMesh == null)
            {
                failureReason = "A cap SkinnedMeshRenderer with a shared mesh is required.";
                return false;
            }

            if (!TryResolveCapBlendShape(out _, out failureReason))
            {
                return false;
            }

            if (stencilApertureRenderer == null)
            {
                failureReason = "A stencil aperture renderer is required.";
                return false;
            }

            return TryValidateCollectionSockets(out failureReason);
        }

        /// <summary>
        /// Claims the closest unclaimed authored socket for one already-reserved collectible.
        /// Runtime capacity remains authoritative; this claim exists only to prevent visual overlap.
        /// </summary>
        public bool TryClaimCollectionSocket(
            string collectibleId,
            Vector3 collectibleWorldPosition,
            out Transform collectionSocket,
            out string failureReason)
        {
            collectionSocket = null;
            if (string.IsNullOrWhiteSpace(collectibleId))
            {
                failureReason = "A collectible runtime id is required to claim a collection socket.";
                return false;
            }

            if (_claimedCollectionSockets.TryGetValue(collectibleId, out collectionSocket) &&
                collectionSocket != null)
            {
                failureReason = string.Empty;
                return true;
            }

            if (!TryValidateCollectionSockets(out failureReason))
            {
                return false;
            }

            float closestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < collectionSockets.Length; i++)
            {
                Transform candidate = collectionSockets[i];
                if (_claimedCollectionSockets.ContainsValue(candidate))
                {
                    continue;
                }

                float distanceSquared =
                    (candidate.position - collectibleWorldPosition).sqrMagnitude;
                if (distanceSquared >= closestDistanceSquared)
                {
                    continue;
                }

                closestDistanceSquared = distanceSquared;
                collectionSocket = candidate;
            }

            if (collectionSocket == null)
            {
                failureReason = "No unclaimed collection socket is available.";
                return false;
            }

            _claimedCollectionSockets.Add(collectibleId, collectionSocket);
            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Plays cap-close followed by visual-root shrink and invokes the callback exactly once.
        /// The caller remains responsible for runtime lifecycle finalization.
        /// </summary>
        public bool TryPlayCompletion(
            Action completionCallback,
            out string failureReason)
        {
            if (_completionSequence != null && _completionSequence.IsActive())
            {
                failureReason = "Hole completion presentation is already active.";
                return false;
            }

            if (!TryValidateConfiguration(out failureReason))
            {
                return false;
            }

            CaptureAuthoredState();
            if (!_hasCapturedPresentationScale || !_hasCapturedCapWeight)
            {
                failureReason = "The authored hole presentation state could not be captured.";
                return false;
            }

            _completionCallback = completionCallback;

            Tween capTween = DOTween.To(
                    () => capRenderer.GetBlendShapeWeight(_capCloseBlendShapeIndex),
                    weight =>
                    {
                        if (capRenderer != null)
                        {
                            capRenderer.SetBlendShapeWeight(_capCloseBlendShapeIndex, weight);
                        }
                    },
                    closedCapBlendShapeWeight,
                    capCloseDuration)
                .SetEase(capCloseEase);

            _completionSequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(capTween);

            if (shrinkDelay > 0f)
            {
                _completionSequence.AppendInterval(shrinkDelay);
            }

            _completionSequence
                .Append(presentationRoot.DOScale(Vector3.zero, shrinkDuration).SetEase(shrinkEase))
                .OnComplete(HandleCompletionFinished)
                .OnKill(HandleCompletionKilled);

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Cancels any active tween and restores the authored cap weight and presentation scale.
        /// No completion callback is invoked by a reset.
        /// </summary>
        [ContextMenu("Reset Presentation")]
        public void ResetPresentation()
        {
            CaptureAuthoredState();
            CancelCompletion(resetPresentation: true);
            _claimedCollectionSockets.Clear();
        }

        [ContextMenu("Validate Configuration")]
        private void ValidateConfigurationFromContextMenu()
        {
            if (TryValidateConfiguration(out string failureReason))
            {
                Debug.Log("Drop The Man hole presentation configuration is valid.", this);
                return;
            }

            Debug.LogError(
                $"Drop The Man hole presentation configuration is invalid: {failureReason}",
                this);
        }

        [ContextMenu("Play Completion Preview")]
        private void PlayCompletionPreviewFromContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Hole completion preview is available only in Play mode.",
                    this);
                return;
            }

            if (!TryPlayCompletion(completionCallback: null, out string failureReason))
            {
                Debug.LogError(
                    $"Drop The Man hole completion preview could not start: {failureReason}",
                    this);
            }
        }

        private void CaptureAuthoredState()
        {
            if (!_hasCapturedPresentationScale && presentationRoot != null)
            {
                _authoredPresentationScale = presentationRoot.localScale;
                _hasCapturedPresentationScale = true;
            }

            if (!_hasCapturedCapWeight &&
                TryResolveCapBlendShape(out int blendShapeIndex, out _))
            {
                _capCloseBlendShapeIndex = blendShapeIndex;
                _authoredCapBlendShapeWeight =
                    capRenderer.GetBlendShapeWeight(_capCloseBlendShapeIndex);
                _hasCapturedCapWeight = true;
            }
        }

        private bool TryResolveCapBlendShape(
            out int blendShapeIndex,
            out string failureReason)
        {
            blendShapeIndex = -1;
            if (capRenderer == null || capRenderer.sharedMesh == null)
            {
                failureReason = "A cap SkinnedMeshRenderer with a shared mesh is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(capCloseBlendShapeName))
            {
                failureReason = "A cap-close blend-shape name is required.";
                return false;
            }

            blendShapeIndex = capRenderer.sharedMesh.GetBlendShapeIndex(capCloseBlendShapeName);
            if (blendShapeIndex < 0)
            {
                failureReason =
                    $"Cap mesh '{capRenderer.sharedMesh.name}' has no blend shape named '{capCloseBlendShapeName}'.";
                return false;
            }

            _capCloseBlendShapeIndex = blendShapeIndex;
            failureReason = string.Empty;
            return true;
        }

        private bool TryValidateCollectionSockets(out string failureReason)
        {
            if (collectionSockets == null || collectionSockets.Length == 0)
            {
                failureReason = "At least one collection socket is required.";
                return false;
            }

            HashSet<Transform> uniqueSockets = new();
            for (int i = 0; i < collectionSockets.Length; i++)
            {
                Transform socket = collectionSockets[i];
                if (socket == null)
                {
                    failureReason = $"Collection socket {i} is missing.";
                    return false;
                }

                if (!uniqueSockets.Add(socket))
                {
                    failureReason = $"Collection socket {i} duplicates another socket reference.";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private void CancelCompletion(bool resetPresentation)
        {
            _completionCallback = null;

            Sequence sequence = _completionSequence;
            _completionSequence = null;
            if (sequence != null && sequence.IsActive())
            {
                sequence.Kill(complete: false);
            }

            if (!resetPresentation)
            {
                return;
            }

            if (_hasCapturedPresentationScale && presentationRoot != null)
            {
                presentationRoot.localScale = _authoredPresentationScale;
            }

            if (_hasCapturedCapWeight && capRenderer != null)
            {
                capRenderer.SetBlendShapeWeight(
                    _capCloseBlendShapeIndex,
                    _authoredCapBlendShapeWeight);
            }
        }

        private void HandleCompletionFinished()
        {
            _completionSequence = null;
            Action callback = _completionCallback;
            _completionCallback = null;
            callback?.Invoke();
        }

        private void HandleCompletionKilled()
        {
            _completionSequence = null;
            _completionCallback = null;
        }
    }
}
