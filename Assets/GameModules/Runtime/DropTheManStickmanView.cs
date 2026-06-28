using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Minimal pre-placed scene adapter for a Drop The Man stickman.
    /// Collection is already decided by runtime drag rules before this component is notified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropTheManStickmanView : MonoBehaviour, IDropTheManStickmanView
    {
        [SerializeField] private string runtimeId = string.Empty;
        [SerializeField] private bool hideRenderersOnCollectionStarted;
        [SerializeField] private Renderer[] renderersToHide;
        [SerializeField] private Collider[] collidersToDisable;

        private bool _collectionStarted;

        public string RuntimeId => runtimeId;
        public bool CollectionStarted => _collectionStarted;

        public void OnCollectionStarted(StickmanRuntimeState stickman)
        {
            if (_collectionStarted)
            {
                return;
            }

            _collectionStarted = true;

            if (collidersToDisable != null)
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
    }
}
