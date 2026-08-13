using UnityEngine;
using PuzzleFramework.Presentation;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Minimal pre-placed scene adapter for a Drop The Man stickman.
    /// Collection eligibility is already decided by runtime rules before this component is
    /// notified at the visual trigger threshold. The placeholder completes synchronously.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManStickmanView : MonoBehaviour, IDropTheManStickmanView
    {
        [SerializeField] private string runtimeId = string.Empty;
        [SerializeField] private bool hideRenderersOnCollectionStarted = true;
        [SerializeField] private bool disableCollidersOnCollectionStarted = true;
        [SerializeField] private bool destroySpawnedViewOnCollectionStarted = true;
        [SerializeField] private Renderer[] renderersToHide;
        [SerializeField] private Collider[] collidersToDisable;

        private bool _collectionStarted;
        private bool _templateHidden;
        private bool _isSpawnedClone;

        public string RuntimeId => runtimeId;
        public bool CollectionStarted => _collectionStarted;

        private void Awake()
        {
            if (renderersToHide == null || renderersToHide.Length == 0)
            {
                renderersToHide = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (collidersToDisable == null || collidersToDisable.Length == 0)
            {
                collidersToDisable = GetComponentsInChildren<Collider>(includeInactive: true);
            }
        }

        public void OnCollectionStarted(StickmanRuntimeState stickman)
        {
            if (_collectionStarted)
            {
                return;
            }

            _collectionStarted = true;

            if (_isSpawnedClone && destroySpawnedViewOnCollectionStarted)
            {
                Destroy(gameObject);
                return;
            }

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

            if (!hideRenderersOnCollectionStarted || renderersToHide == null)
            {
                return;
            }

            for (int i = 0; i < renderersToHide.Length; i++)
            {
                if (renderersToHide[i] != null)
                {
                    renderersToHide[i].enabled = false;
                }
            }
        }

        public void ConfigureSpawnedView(
            string newRuntimeId,
            Vector3 worldPosition,
            ColorIdentity colorIdentity)
        {
            runtimeId = newRuntimeId;
            transform.position = worldPosition;
            _collectionStarted = false;
            _templateHidden = false;
            _isSpawnedClone = true;
            EnsurePresentationTargets();

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

            DropTheManViewPresentationUtility.ApplyColor(renderersToHide, colorIdentity);
            ApplyTemplateHiddenState();
        }

        public void SetTemplateHidden(bool isHidden)
        {
            _templateHidden = isHidden;
            ApplyTemplateHiddenState();
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
        }

        private void ApplyTemplateHiddenState()
        {
            if (collidersToDisable != null)
            {
                for (int i = 0; i < collidersToDisable.Length; i++)
                {
                    if (collidersToDisable[i] != null)
                    {
                        collidersToDisable[i].enabled = !_templateHidden && !_collectionStarted;
                    }
                }
            }

            if (renderersToHide != null)
            {
                for (int i = 0; i < renderersToHide.Length; i++)
                {
                    if (renderersToHide[i] != null)
                    {
                        renderersToHide[i].enabled = !_templateHidden && !_collectionStarted;
                    }
                }
            }
        }
    }
}
