using UnityEngine;
using UnityEngine.Serialization;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Spawned presentation adapter for a Drop The Man collectable.
    /// Collection eligibility is already decided by runtime rules before this component is
    /// notified at the visual trigger threshold. The view reports visual completion by callback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManStickmanView : MonoBehaviour, IDropTheManStickmanView
    {
        [SerializeField] private string runtimeId = string.Empty;
        [FormerlySerializedAs("hideRenderersOnCollectionStarted")]
        [SerializeField] private bool hideRenderersOnCollectionCompleted = true;
        [SerializeField] private bool disableCollidersOnCollectionStarted = true;
        [FormerlySerializedAs("destroySpawnedViewOnCollectionStarted")]
        [SerializeField] private bool destroySpawnedViewOnCollectionCompleted = true;
        [SerializeField] private Renderer[] renderersToHide;
        [SerializeField, Min(0)] private int bodyMaterialIndex;
        [SerializeField] private Collider[] collidersToDisable;
        [SerializeField] private DropTheManCatCollectionPresentation collectionPresentation;

        private bool _collectionStarted;
        private bool _collectionCompleted;
        private bool _isSpawnedClone;

        public string RuntimeId => runtimeId;
        public bool CollectionStarted => _collectionStarted;

        private void Awake()
        {
            EnsurePresentationTargets();
        }

        public bool TryPlayCollectionPresentation(
            Transform collectionSocket,
            System.Action completionCallback,
            out string failureReason)
        {
            if (_collectionStarted)
            {
                failureReason = $"Stickman view '{runtimeId}' already started collection presentation.";
                return false;
            }

            _collectionStarted = true;

            if (disableCollidersOnCollectionStarted && collidersToDisable != null)
            {
                for (int i = 0; i < collidersToDisable.Length; i++)
                {
                    if (collidersToDisable[i] != null)
                    {
                        collidersToDisable[i].enabled = false;
                    }
                }
            }

            void CompleteOnce()
            {
                if (_collectionCompleted)
                {
                    return;
                }

                _collectionCompleted = true;
                ApplyCollectedVisualState();
                completionCallback?.Invoke();
            }

            EnsurePresentationTargets();
            string presentationFailureReason = string.Empty;
            if (collectionPresentation != null &&
                collectionPresentation.TryPlay(
                    collectionSocket,
                    CompleteOnce,
                    out presentationFailureReason))
            {
                failureReason = string.Empty;
                return true;
            }

            string fallbackReason = collectionPresentation == null
                ? "No cat collection presentation is configured."
                : presentationFailureReason;
            Debug.LogWarning(
                $"Stickman view '{runtimeId}' is using immediate collection fallback: {fallbackReason}",
                this);
            CompleteOnce();
            failureReason = string.Empty;
            return true;
        }

        public void ConfigureSpawnedView(
            string newRuntimeId,
            Vector3 worldPosition,
            ColorIdentity colorIdentity)
        {
            runtimeId = newRuntimeId;
            transform.position = worldPosition;
            _collectionStarted = false;
            _collectionCompleted = false;
            _isSpawnedClone = true;
            EnsurePresentationTargets();
            collectionPresentation?.ResetPresentation();

            if (collidersToDisable != null)
            {
                for (int i = 0; i < collidersToDisable.Length; i++)
                {
                    if (collidersToDisable[i] != null)
                    {
                        collidersToDisable[i].enabled = true;
                    }
                }
            }

            if (renderersToHide != null)
            {
                for (int i = 0; i < renderersToHide.Length; i++)
                {
                    if (renderersToHide[i] != null)
                    {
                        renderersToHide[i].enabled = true;
                    }
                }
            }

            DropTheManViewPresentationUtility.ApplyColor(renderersToHide, colorIdentity, bodyMaterialIndex);
        }

        private void EnsurePresentationTargets()
        {
            if (renderersToHide == null || renderersToHide.Length == 0)
            {
                renderersToHide = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (collidersToDisable == null || collidersToDisable.Length == 0)
            {
                collidersToDisable = GetComponentsInChildren<Collider>(includeInactive: true);
            }

            if (collectionPresentation == null)
            {
                TryGetComponent(out collectionPresentation);
            }
        }

        private void ApplyCollectedVisualState()
        {
            if (hideRenderersOnCollectionCompleted && renderersToHide != null)
            {
                for (int i = 0; i < renderersToHide.Length; i++)
                {
                    if (renderersToHide[i] != null)
                    {
                        renderersToHide[i].enabled = false;
                    }
                }
            }

            if (_isSpawnedClone && destroySpawnedViewOnCollectionCompleted)
            {
                Destroy(gameObject);
            }
        }
    }
}
