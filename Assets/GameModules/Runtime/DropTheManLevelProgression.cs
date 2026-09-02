using System;
using System.Collections.Generic;
using PuzzleFramework.Content;
using PuzzleFramework.Progression;

namespace DropAwayPrototype.Runtime
{
    /// <summary>
    /// Game-owned campaign/replay/loop policy. Storage contains IDs and history, not this policy.
    /// </summary>
    public sealed class DropTheManLevelProgression
    {
        private readonly List<LevelCatalogMetadata> _levels;
        private readonly List<string> _loopIds = new();
        private readonly HashSet<string> _knownIds = new(StringComparer.Ordinal);
        public PlayerProgressData Progress { get; }

        public DropTheManLevelProgression(
            PlayerProgressData progress, IReadOnlyList<LevelCatalogMetadata> levels,
            int loopFirstLevel, int loopLastLevel)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (levels == null || levels.Count == 0)
                throw new ArgumentException("Progression requires a nonempty level catalog.", nameof(levels));
            _levels = new List<LevelCatalogMetadata>(levels);
            _levels.Sort((left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
            int last = loopLastLevel == 0 ? _levels[_levels.Count - 1].SequenceNumber : loopLastLevel;
            if (loopFirstLevel < 1 || last < loopFirstLevel)
                throw new ArgumentException("Loop range must be positive and ordered; last=0 means last shipped level.");
            foreach (LevelCatalogMetadata level in _levels)
            {
                if (string.IsNullOrWhiteSpace(level.LevelId) || !_knownIds.Add(level.LevelId))
                    throw new ArgumentException("Progression level IDs must be nonblank and unique.");
                if (level.SequenceNumber >= loopFirstLevel && level.SequenceNumber <= last)
                    _loopIds.Add(level.LevelId);
            }
            if (_loopIds.Count == 0)
                throw new ArgumentException("The loop range contains no shipped levels.");
        }

        public string GetContinueLevelId()
        {
            foreach (LevelCatalogMetadata level in _levels)
                if (!Progress.IsCompleted(level.LevelId)) return level.LevelId;
            return _loopIds.Contains(Progress.ResumeLevelId) ? Progress.ResumeLevelId : _loopIds[0];
        }

        public bool CanReplay(string levelId) => _knownIds.Contains(levelId) && Progress.IsCompleted(levelId);

        /// <summary>
        /// Selects the next completed replay, or returns to campaign at an unfinished entry/end.
        /// This is a read-only selection; a failed level load must not alter the saved cursor.
        /// </summary>
        public string GetLevelAfterReplay(string levelId, out bool isReplay)
        {
            if (!CanReplay(levelId))
                throw new InvalidOperationException("Only completed shipped levels may be replayed.");
            for (int i = 0; i < _levels.Count - 1; i++)
            {
                if (_levels[i].LevelId != levelId) continue;
                string nextId = _levels[i + 1].LevelId;
                if (CanReplay(nextId))
                {
                    // Matching the saved loop ID does not turn a manual replay into campaign play.
                    isReplay = true;
                    return nextId;
                }
                break;
            }
            isReplay = false;
            return GetContinueLevelId();
        }

        /// <summary>Called only for an accepted win of the active session, never for Next or loss.</summary>
        public void RecordWin(string levelId, bool isReplay)
        {
            if (isReplay)
            {
                if (!CanReplay(levelId)) throw new InvalidOperationException("Only completed shipped levels may be replayed.");
                return;
            }
            if (!string.Equals(levelId, GetContinueLevelId(), StringComparison.Ordinal))
                throw new InvalidOperationException("The won level is not the active campaign selection.");

            bool newlyCompleted = Progress.MarkCompleted(levelId);
            foreach (LevelCatalogMetadata level in _levels)
            {
                if (!Progress.IsCompleted(level.LevelId))
                {
                    Progress.SetResumeLevel(level.LevelId);
                    return;
                }
            }
            // The first campaign finish enters the range at its beginning. Only subsequent loop
            // wins advance its cursor. Changing this selection rule does not change save schema.
            int index = newlyCompleted ? -1 : _loopIds.IndexOf(levelId);
            Progress.SetResumeLevel(_loopIds[(index + 1) % _loopIds.Count]);
        }
    }
}
