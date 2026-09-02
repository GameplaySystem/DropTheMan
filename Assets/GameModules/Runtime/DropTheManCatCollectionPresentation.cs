using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Prototype-owned cat collection motion and animation. Gameplay collection remains runtime-owned
    /// and advances only after the completion callback is invoked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManCatCollectionPresentation : MonoBehaviour
    {
        [Header("Prefab Contract")]
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private Animator animator;
        [Tooltip("Direct FBX clips in the same order as controller states Fall_1, Fall_2, ...")]
        [SerializeField] private AnimationClip[] fallingClips;

        [Header("Collection Motion")]
        [SerializeField, Min(0f)] private float riseHeight = 0.8f;
        [SerializeField, Min(0.01f)] private float approachDuration = 0.35f;
        [SerializeField] private Ease approachEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float fallDepth = 3.5f;
        [SerializeField, Min(0.01f)] private float fallDuration = 0.7f;
        [SerializeField] private Ease fallEase = Ease.InQuad;
        [SerializeField, Range(0f, 1f)] private float finalScaleMultiplier = 0.05f;
        [SerializeField] private Ease shrinkEase = Ease.InQuad;
        [SerializeField] private bool useUnscaledTime;

        private Sequence _collectionSequence;
        private Action _completionCallback;
        private Transform _liveSocket;
        private Vector3 _approachStartWorldPosition;
        private Vector3 _authoredPresentationScale;
        private bool _hasCapturedPresentationScale;
        private int _assignedVariantIndex = -1;
        private int _assignedStateHash;
        private static readonly List<int> VariantBag = new();
        private static int _configuredVariantCount;
        private static int _lastAssignedVariantIndex = -1;

        public bool IsPlaying =>
            _collectionSequence != null &&
            _collectionSequence.IsActive() &&
            _collectionSequence.IsPlaying();

        private void Awake()
        {
            CaptureAuthoredState();
            TryAssignFallingVariant();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVariantSelection()
        {
            VariantBag.Clear();
            _configuredVariantCount = 0;
            _lastAssignedVariantIndex = -1;
        }

        private void OnDisable()
        {
            Cancel(resetPresentation: false);
        }

        private void OnDestroy()
        {
            Cancel(resetPresentation: false);
        }

        private void OnValidate()
        {
            riseHeight = Mathf.Max(0f, riseHeight);
            approachDuration = Mathf.Max(0.01f, approachDuration);
            fallDepth = Mathf.Max(0.01f, fallDepth);
            fallDuration = Mathf.Max(0.01f, fallDuration);
            finalScaleMultiplier = Mathf.Clamp01(finalScaleMultiplier);
        }

        /// <summary>
        /// Starts the preassigned Animator state and follows the supplied live socket. The view
        /// reports successful disappearance through the callback; this does not award capacity.
        /// </summary>
        public bool TryPlay(
            Transform liveSocket,
            Action completionCallback,
            out string failureReason)
        {
            if (_collectionSequence != null && _collectionSequence.IsActive())
            {
                failureReason = "Cat collection presentation is already active.";
                return false;
            }

            if (!TryValidateConfiguration(liveSocket, out failureReason))
            {
                return false;
            }

            CaptureAuthoredState();
            if (!_hasCapturedPresentationScale)
            {
                failureReason = "The authored cat presentation scale could not be captured.";
                return false;
            }

            _liveSocket = liveSocket;
            _completionCallback = completionCallback;
            _approachStartWorldPosition = transform.position;

            animator.Play(_assignedStateHash, 0, 0f);
            animator.Update(0f);

            Tween approachTween = DOTween.To(
                    () => 0f,
                    progress => ApplyApproachPosition(progress),
                    1f,
                    approachDuration)
                .SetEase(approachEase);
            Tween fallTween = DOTween.To(
                    () => 0f,
                    progress => ApplyFallPosition(progress),
                    1f,
                    fallDuration)
                .SetEase(fallEase);
            Tween shrinkTween = presentationRoot
                .DOScale(_authoredPresentationScale * finalScaleMultiplier,
                    approachDuration + fallDuration)
                .SetEase(shrinkEase);
            _collectionSequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(approachTween)
                .Append(fallTween)
                .Insert(0f, shrinkTween)
                .OnComplete(HandleCompleted)
                .OnKill(HandleKilled);

            failureReason = string.Empty;
            return true;
        }

        /// <summary>Restores authored scale and Idle, cancelling motion without a completion callback.</summary>
        public void ResetPresentation()
        {
            CaptureAuthoredState();
            Cancel(resetPresentation: true);
        }

        private bool TryValidateConfiguration(
            Transform liveSocket,
            out string failureReason)
        {
            if (presentationRoot == null)
            {
                failureReason = "A cat presentation root is required.";
                return false;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                failureReason = "An Animator with a runtime controller is required.";
                return false;
            }

            if (liveSocket == null)
            {
                failureReason = "A live hole collection socket is required.";
                return false;
            }

            if (!TryAssignFallingVariant())
            {
                failureReason = "Falling clips must be configured without missing entries.";
                return false;
            }

            if (!animator.HasState(0, _assignedStateHash))
            {
                failureReason =
                    $"Animator state Fall_{_assignedVariantIndex + 1} is not configured.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private bool TryAssignFallingVariant()
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                fallingClips == null ||
                fallingClips.Length == 0)
            {
                return false;
            }

            for (int candidateIndex = 0;
                 candidateIndex < fallingClips.Length;
                 candidateIndex++)
            {
                if (fallingClips[candidateIndex] == null)
                {
                    return false;
                }
            }

            if (_assignedVariantIndex < 0 ||
                _assignedVariantIndex >= fallingClips.Length)
            {
                _assignedVariantIndex = SelectNextVariantIndex(fallingClips.Length);
                _assignedStateHash = Animator.StringToHash($"Base Layer.Fall_{_assignedVariantIndex + 1}");
            }

            return true;
        }

        private static int SelectNextVariantIndex(int variantCount)
        {
            if (_configuredVariantCount != variantCount)
            {
                _configuredVariantCount = variantCount;
                VariantBag.Clear();
                _lastAssignedVariantIndex = -1;
            }

            if (VariantBag.Count == 0)
            {
                RefillVariantBag();
            }

            int selectionIndex = VariantBag.Count - 1;
            int selectedVariantIndex = VariantBag[selectionIndex];
            VariantBag.RemoveAt(selectionIndex);
            _lastAssignedVariantIndex = selectedVariantIndex;
            return selectedVariantIndex;
        }

        private static void RefillVariantBag()
        {
            for (int i = 0; i < _configuredVariantCount; i++)
            {
                VariantBag.Add(i);
            }
            for (int i = VariantBag.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (VariantBag[i], VariantBag[swapIndex]) =
                    (VariantBag[swapIndex], VariantBag[i]);
            }

            int nextSelectionIndex = VariantBag.Count - 1;
            if (VariantBag.Count > 1 &&
                VariantBag[nextSelectionIndex] == _lastAssignedVariantIndex)
            {
                (VariantBag[0], VariantBag[nextSelectionIndex]) =
                    (VariantBag[nextSelectionIndex], VariantBag[0]);
            }
        }

        private void ApplyApproachPosition(float progress)
        {
            if (_liveSocket == null)
            {
                return;
            }

            Vector3 liveApex = _liveSocket.position + Vector3.up * riseHeight;
            transform.position = Vector3.LerpUnclamped(
                _approachStartWorldPosition,
                liveApex,
                progress);
        }

        private void ApplyFallPosition(float progress)
        {
            if (_liveSocket == null)
            {
                return;
            }

            Vector3 liveApex = _liveSocket.position + Vector3.up * riseHeight;
            Vector3 liveBottom = _liveSocket.position - Vector3.up * fallDepth;
            transform.position = Vector3.LerpUnclamped(liveApex, liveBottom, progress);
        }

        private void CaptureAuthoredState()
        {
            if (_hasCapturedPresentationScale || presentationRoot == null)
            {
                return;
            }

            _authoredPresentationScale = presentationRoot.localScale;
            _hasCapturedPresentationScale = true;
        }

        private void Cancel(bool resetPresentation)
        {
            _completionCallback = null;
            _liveSocket = null;

            Sequence sequence = _collectionSequence;
            _collectionSequence = null;
            if (sequence != null && sequence.IsActive())
            {
                sequence.Kill(complete: false);
            }

            if (resetPresentation && _hasCapturedPresentationScale && presentationRoot != null)
            {
                presentationRoot.localScale = _authoredPresentationScale;
            }

            if (resetPresentation && animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void HandleCompleted()
        {
            _collectionSequence = null;
            _liveSocket = null;
            Action callback = _completionCallback;
            _completionCallback = null;
            callback?.Invoke();
        }

        private void HandleKilled()
        {
            _collectionSequence = null;
            _liveSocket = null;
            _completionCallback = null;
        }
    }
}
