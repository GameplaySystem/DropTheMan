using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.Progression;

namespace DropAwayPrototype.Runtime
{
    /// <summary>One profile's load/save lifecycle. Invalid loads are never overwritten automatically.</summary>
    public sealed class DropTheManProgressionSession
    {
        private readonly IProgressSaveLoadService _storage;
        private readonly string _path;
        private bool _winRecorded;
        public DropTheManLevelProgression Levels { get; private set; }
        public string ActiveLevelId { get; private set; }
        public bool IsReplay { get; private set; }
        public bool IsDirty { get; private set; }
        public string Error { get; private set; } = string.Empty;

        public DropTheManProgressionSession(IProgressSaveLoadService storage, string path)
        {
            _storage = storage ?? throw new System.ArgumentNullException(nameof(storage));
            _path = path;
        }

        public bool TryLoad(IReadOnlyList<LevelCatalogMetadata> levels, int first, int last)
        {
            if (Levels != null)
            {
                Error = "This profile is already loaded; create a new session to reload from disk.";
                return false;
            }
            ProgressLoadResult result = _storage.Load(_path);
            if (result.Status != ProgressLoadStatus.Loaded && result.Status != ProgressLoadStatus.NotFound)
            {
                Error = $"Progress load failed ({result.Status}): {result.FailureReason}";
                return false;
            }
            try
            {
                Levels = new DropTheManLevelProgression(result.Progress ?? new PlayerProgressData(), levels, first, last);
                Error = string.Empty;
                return true;
            }
            catch (System.ArgumentException e) { Error = e.Message; return false; }
        }

        /// <summary>Commit the session selection only after its level successfully constructs.</summary>
        public void BeginLevel(string levelId, bool isReplay)
        {
            if (Levels == null || (isReplay ? !Levels.CanReplay(levelId) : Levels.GetContinueLevelId() != levelId))
                throw new System.InvalidOperationException("Invalid progression session selection.");
            ActiveLevelId = levelId;
            IsReplay = isReplay;
            _winRecorded = false;
        }

        public void RecordAcceptedWin()
        {
            if (_winRecorded || Levels == null || ActiveLevelId == null) return;
            Levels.RecordWin(ActiveLevelId, IsReplay);
            _winRecorded = true;
            if (!IsReplay)
            {
                IsDirty = true;
                TrySave();
            }
        }

        /// <summary>Returns a post-win destination without starting it or mutating saved progress.</summary>
        public bool TryGetNextLevelAfterWin(out string levelId, out bool isReplay, out string failureReason)
        {
            levelId = null;
            isReplay = false;
            if (Levels == null || !_winRecorded)
            {
                failureReason = "Complete the active level before loading the next level.";
                return false;
            }
            levelId = IsReplay
                ? Levels.GetLevelAfterReplay(ActiveLevelId, out isReplay)
                : Levels.GetContinueLevelId();
            failureReason = string.Empty;
            return true;
        }

        public bool TrySave()
        {
            if (!IsDirty || Levels == null) return true;
            ProgressSaveResult result = _storage.Save(Levels.Progress, _path);
            Error = result.Success ? string.Empty : result.FailureReason;
            IsDirty = !result.Success;
            return result.Success;
        }
    }
}
