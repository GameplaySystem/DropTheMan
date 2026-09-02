using System;
using System.Collections.Generic;
using UnityEngine;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Minimal scene-side adapter for a pre-placed Drop The Man hole view.
    /// The view adapts selection and visual position only; gameplay truth remains in runtime state.
    /// </summary>
    public interface IDropTheManHoleView
    {
        string RuntimeId { get; }
        Vector3 WorldPosition { get; }
        void ApplyWorldPosition(Vector3 worldPosition);
        void SetSelectable(bool isSelectable);
        bool TryClaimCollectionSocket(
            string collectibleId,
            Vector3 collectibleWorldPosition,
            out Transform collectionSocket,
            out string failureReason);
        bool TryPlayCompletionPresentation(
            Action completionCallback,
            out string failureReason);
    }

    /// <summary>
    /// Minimal scene-side adapter for a pre-placed Drop The Man stickman view.
    /// Collection is reserved by runtime rules and has reached its visual trigger threshold
    /// before this hook is invoked. The view reports completion through the callback.
    /// </summary>
    public interface IDropTheManStickmanView
    {
        string RuntimeId { get; }
        bool TryPlayCollectionPresentation(
            Transform collectionSocket,
            Action completionCallback,
            out string failureReason);
    }

    /// <summary>
    /// Small result type for registry operations that can fail during setup or routing.
    /// </summary>
    public readonly struct DropTheManViewRegistryResult
    {
        private DropTheManViewRegistryResult(bool success, string failureReason)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public string FailureReason { get; }

        public static DropTheManViewRegistryResult Successful()
        {
            return new DropTheManViewRegistryResult(true, string.Empty);
        }

        public static DropTheManViewRegistryResult Failed(string failureReason)
        {
            return new DropTheManViewRegistryResult(false, failureReason);
        }
    }

    /// <summary>
    /// Prototype-owned id-to-view mapping for pre-placed Drop The Man scene adapters.
    /// This class does not own gameplay rules, spawning, scene searches, or presentation polish.
    /// </summary>
    public sealed class DropTheManViewRegistry
    {
        private readonly Dictionary<string, IDropTheManHoleView> _holeViews = new();
        private readonly Dictionary<string, IDropTheManStickmanView> _stickmanViews = new();

        public DropTheManViewRegistryResult RegisterHoleView(IDropTheManHoleView view)
        {
            if (view == null)
            {
                return DropTheManViewRegistryResult.Failed("Hole view is required.");
            }

            if (string.IsNullOrWhiteSpace(view.RuntimeId))
            {
                return DropTheManViewRegistryResult.Failed("Hole view runtime id is required.");
            }

            if (_holeViews.ContainsKey(view.RuntimeId))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"A hole view is already registered for runtime id '{view.RuntimeId}'.");
            }

            _holeViews.Add(view.RuntimeId, view);
            return DropTheManViewRegistryResult.Successful();
        }

        public DropTheManViewRegistryResult RegisterStickmanView(IDropTheManStickmanView view)
        {
            if (view == null)
            {
                return DropTheManViewRegistryResult.Failed("Stickman view is required.");
            }

            if (string.IsNullOrWhiteSpace(view.RuntimeId))
            {
                return DropTheManViewRegistryResult.Failed("Stickman view runtime id is required.");
            }

            if (_stickmanViews.ContainsKey(view.RuntimeId))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"A stickman view is already registered for runtime id '{view.RuntimeId}'.");
            }

            _stickmanViews.Add(view.RuntimeId, view);
            return DropTheManViewRegistryResult.Successful();
        }

        public bool TryGetHoleView(string runtimeId, out IDropTheManHoleView view)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                view = null;
                return false;
            }

            return _holeViews.TryGetValue(runtimeId, out view);
        }

        public bool TryGetStickmanView(string runtimeId, out IDropTheManStickmanView view)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                view = null;
                return false;
            }

            return _stickmanViews.TryGetValue(runtimeId, out view);
        }

        public DropTheManViewRegistryResult ValidateRuntimeMappings(
            DropTheManRuntimeModel runtimeModel)
        {
            if (runtimeModel == null)
            {
                return DropTheManViewRegistryResult.Failed("Runtime model is required.");
            }

            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                if (!_holeViews.ContainsKey(runtimeModel.Holes[i].Id))
                {
                    return DropTheManViewRegistryResult.Failed(
                        $"No hole view is registered for runtime hole '{runtimeModel.Holes[i].Id}'.");
                }
            }

            for (int i = 0; i < runtimeModel.Stickmen.Count; i++)
            {
                if (!_stickmanViews.ContainsKey(runtimeModel.Stickmen[i].Id))
                {
                    return DropTheManViewRegistryResult.Failed(
                        $"No stickman view is registered for runtime stickman '{runtimeModel.Stickmen[i].Id}'.");
                }
            }

            return DropTheManViewRegistryResult.Successful();
        }

        public DropTheManViewRegistryResult ApplyHoleWorldPosition(
            string runtimeId,
            Vector3 worldPosition)
        {
            if (!TryGetHoleView(runtimeId, out IDropTheManHoleView view))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"No hole view is registered for runtime id '{runtimeId}'.");
            }

            if (view is UnityEngine.Object unityObject && unityObject == null)
            {
                _holeViews.Remove(runtimeId);
                return DropTheManViewRegistryResult.Failed(
                    $"Hole view '{runtimeId}' was destroyed before its world position could be applied.");
            }

            view.ApplyWorldPosition(worldPosition);
            return DropTheManViewRegistryResult.Successful();
        }

        public DropTheManViewRegistryResult SetHoleSelectable(
            string runtimeId,
            bool isSelectable)
        {
            if (!TryGetHoleView(runtimeId, out IDropTheManHoleView view))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"No hole view is registered for runtime id '{runtimeId}'.");
            }

            if (view is UnityEngine.Object unityObject && unityObject == null)
            {
                _holeViews.Remove(runtimeId);
                return DropTheManViewRegistryResult.Failed(
                    $"Hole view '{runtimeId}' was destroyed before its selectable state could be updated.");
            }

            view.SetSelectable(isSelectable);
            return DropTheManViewRegistryResult.Successful();
        }

        public DropTheManViewRegistryResult BeginHoleCompletionPresentation(
            string runtimeId,
            Action completionCallback)
        {
            if (!TryGetHoleView(runtimeId, out IDropTheManHoleView view))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"No hole view is registered for runtime id '{runtimeId}'.");
            }

            if (view is UnityEngine.Object unityObject && unityObject == null)
            {
                _holeViews.Remove(runtimeId);
                return DropTheManViewRegistryResult.Failed(
                    $"Hole view '{runtimeId}' was destroyed before completion presentation could start.");
            }

            return view.TryPlayCompletionPresentation(
                completionCallback,
                out string failureReason)
                ? DropTheManViewRegistryResult.Successful()
                : DropTheManViewRegistryResult.Failed(failureReason);
        }

        public DropTheManViewRegistryResult BeginCollectionPresentation(
            StickmanRuntimeState stickman,
            Action completionCallback)
        {
            if (stickman == null)
            {
                return DropTheManViewRegistryResult.Failed("Stickman runtime state is required.");
            }

            if (!TryGetStickmanView(stickman.Id, out IDropTheManStickmanView view))
            {
                return DropTheManViewRegistryResult.Failed(
                    $"No stickman view is registered for runtime id '{stickman.Id}'.");
            }

            if (view is UnityEngine.Object unityObject && unityObject == null)
            {
                _stickmanViews.Remove(stickman.Id);
                return DropTheManViewRegistryResult.Failed(
                    $"Stickman view '{stickman.Id}' was destroyed before collection presentation could start.");
            }

            Transform collectionSocket = null;
            string socketFailureReason = string.Empty;
            if (!TryGetHoleView(stickman.ReservedHoleId, out IDropTheManHoleView holeView) ||
                (holeView is UnityEngine.Object holeObject && holeObject == null))
            {
                socketFailureReason =
                    $"No live hole view is registered for reserved hole '{stickman.ReservedHoleId}'.";
            }
            else if (!holeView.TryClaimCollectionSocket(
                         stickman.Id,
                         (view as Component)?.transform.position ?? Vector3.zero,
                         out collectionSocket,
                         out socketFailureReason))
            {
                collectionSocket = null;
            }

            if (!string.IsNullOrEmpty(socketFailureReason))
            {
                Debug.LogWarning(
                    $"Stickman '{stickman.Id}' is using immediate collection fallback: " +
                    socketFailureReason);
            }

            return view.TryPlayCollectionPresentation(
                collectionSocket,
                completionCallback,
                out string failureReason)
                ? DropTheManViewRegistryResult.Successful()
                : DropTheManViewRegistryResult.Failed(failureReason);
        }

        public bool TryResolveHoleState(
            DropTheManRuntimeModel runtimeModel,
            string runtimeId,
            out HoleRuntimeState hole,
            out string failureReason)
        {
            if (runtimeModel == null)
            {
                hole = null;
                failureReason = "Runtime model is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                hole = null;
                failureReason = "Runtime hole id is required.";
                return false;
            }

            for (int i = 0; i < runtimeModel.Holes.Count; i++)
            {
                if (string.Equals(runtimeModel.Holes[i].Id, runtimeId, StringComparison.Ordinal))
                {
                    hole = runtimeModel.Holes[i];
                    failureReason = string.Empty;
                    return true;
                }
            }

            hole = null;
            failureReason = $"No runtime hole exists for id '{runtimeId}'.";
            return false;
        }
    }
}
