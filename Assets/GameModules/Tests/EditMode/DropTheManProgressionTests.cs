using System.Collections.Generic;
using NUnit.Framework;
using DropAwayPrototype.Runtime;
using PuzzleFramework.Content;
using PuzzleFramework.Progression;
using PuzzleFramework.CoreBoard;
using PuzzleFramework.RuntimeConstruction;
using PuzzleFramework.RuntimeFlow;
using UnityEngine;

namespace DropAwayPrototype.Tests
{
    public sealed class DropTheManProgressionTests
    {
        private static readonly LevelCatalogMetadata[] Levels =
        {
            new("a", 1), new("b", 2), new("c", 3)
        };

        [Test]
        public void FreshProfileBeginsAtFirstLevelAndUnlocksReplayOnlyOnWin()
        {
            var policy = Create();
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("a"));
            Assert.That(policy.CanReplay("a"), Is.False);
            policy.RecordWin("a", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("b"));
            Assert.That(policy.CanReplay("a"), Is.True);
            Assert.That(policy.CanReplay("b"), Is.False);
        }

        [Test]
        public void ReplayingCompletedLevelDoesNotAdvanceOrRewindCampaign()
        {
            var policy = Create(); policy.RecordWin("a", false);
            policy.RecordWin("a", true);
            Assert.That(policy.Progress.ResumeLevelId, Is.EqualTo("b"));
            Assert.That(policy.Progress.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsLockedReplayOrOutOfOrderWin()
        {
            Assert.Throws<System.InvalidOperationException>(() => Create().RecordWin("b", true));
            Assert.Throws<System.InvalidOperationException>(() => Create().RecordWin("b", false));
        }

        [Test]
        public void AllCompletedEntersConfiguredRangeAndWraps()
        {
            var policy = Create(first: 2, last: 3);
            CompleteCampaign(policy);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("b"));
            policy.RecordWin("b", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("c"));
            policy.RecordWin("c", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void SingleLevelLoopStaysOnThatLevel()
        {
            var policy = Create(first: 2, last: 2); CompleteCampaign(policy);
            policy.RecordWin("b", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [TestCase(3, 2)]
        [TestCase(5, 9)]
        [TestCase(0, 3)]
        public void InvalidRangesFailClearly(int first, int last) =>
            Assert.Throws<System.ArgumentException>(() => Create(first: first, last: last));

        [Test]
        public void RangeChangeRequiresNoSaveMigration()
        {
            var old = Create(); CompleteCampaign(old);
            Assert.That(old.GetContinueLevelId(), Is.EqualTo("a"));
            var changed = Create(old.Progress, 2, 3);
            Assert.That(changed.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void NewContentTakesPriorityOverLoopAndRemovedIdsRemainHistory()
        {
            var old = Create(); CompleteCampaign(old);
            var updated = new DropTheManLevelProgression(old.Progress,
                new[] { Levels[1], Levels[2], new LevelCatalogMetadata("new-id", 4) }, 1, 0);
            Assert.That(updated.GetContinueLevelId(), Is.EqualTo("new-id"));
            Assert.That(updated.Progress.IsCompleted("a"), Is.True);
            Assert.That(updated.CanReplay("a"), Is.False);
        }

        [Test]
        public void CatalogOrderAndGapsDoNotDependOnSavedIndexes()
        {
            var policy = new DropTheManLevelProgression(new PlayerProgressData(),
                new[] { new LevelCatalogMetadata("late", 8), new LevelCatalogMetadata("early", 3) }, 2, 9);
            policy.RecordWin("early", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("late"));
            policy.RecordWin("late", false);
            Assert.That(policy.GetContinueLevelId(), Is.EqualTo("early"));
        }

        [Test]
        public void WinSavesBeforeNextAndDuplicateNotificationDoesNotAdvanceLoopTwice()
        {
            var store = new MemoryStore(); var session = Load(store);
            foreach (var level in Levels)
            {
                session.BeginLevel(level.LevelId, false);
                session.RecordAcceptedWin();
                session.RecordAcceptedWin();
            }
            Assert.That(store.SaveCount, Is.EqualTo(3));
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("a"));
            session.BeginLevel("a", false);
            session.RecordAcceptedWin(); session.RecordAcceptedWin();
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void LossRestartOrMidLevelExitDoesNotSaveOrAdvance()
        {
            var store = new MemoryStore(); var session = Load(store);
            session.BeginLevel("a", false); session.BeginLevel("a", false);
            session.TrySave();
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("a"));
        }

        [Test]
        public void FailedSaveKeepsDirtyStateAndRetryPersistsIt()
        {
            var store = new MemoryStore { FailSave = true }; var session = Load(store);
            session.BeginLevel("a", false); session.RecordAcceptedWin();
            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.Levels.GetContinueLevelId(), Is.EqualTo("b"));
            Assert.That(session.Error, Is.Not.Empty);
            store.FailSave = false;
            Assert.That(session.TrySave(), Is.True);
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.Error, Is.Empty);
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void InvalidLoadNeverSavesOverOriginal()
        {
            var store = new MemoryStore { InvalidLoad = true };
            var session = new DropTheManProgressionSession(store, "profile");
            Assert.That(session.TryLoad(Levels, 1, 0), Is.False);
            session.RecordAcceptedWin(); session.TrySave();
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(session.Error, Is.Not.Empty);
        }

        [Test]
        public void ReplaySessionDoesNotSaveOrChangeColdResume()
        {
            var store = new MemoryStore(); var session = Load(store);
            session.BeginLevel("a", false); session.RecordAcceptedWin();
            session.BeginLevel("a", true); session.RecordAcceptedWin();
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void FailedOrUnstartedSessionCannotAdvanceAndLoadedSessionCannotDiscardDirtyProgress()
        {
            var store = new MemoryStore { FailSave = true };
            var session = Load(store);
            session.RecordAcceptedWin();
            Assert.That(store.SaveCount, Is.Zero);
            Assert.Throws<System.InvalidOperationException>(() => session.BeginLevel("c", false));
            session.BeginLevel("a", false);
            session.RecordAcceptedWin();
            Assert.That(session.TryLoad(Levels, 1, 0), Is.False);
            Assert.That(session.IsDirty, Is.True);
            Assert.That(session.Levels.GetContinueLevelId(), Is.EqualTo("b"));
        }

        [Test]
        public void ReplayNextChainsCompletedLevelsThenResumesUnfinishedCampaign()
        {
            var store = new MemoryStore(); var session = Load(store);
            Win(session, "a"); Win(session, "b");
            Win(session, "a", replay: true);
            AssertNext(session, "b", replay: true);
            Win(session, "b", replay: true);
            AssertNext(session, "c", replay: false);
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("c"));
            Win(session, "c");
            Assert.That(store.SaveCount, Is.EqualTo(3));
            AssertNext(session, "a", replay: false);
        }

        [Test]
        public void LastReplayResumesSavedLoopButEarlierReplayMatchingLoopCursorStaysReplay()
        {
            var store = new MemoryStore();
            var session = new DropTheManProgressionSession(store, "profile");
            Assert.That(session.TryLoad(Levels, 2, 3), Is.True);
            Win(session, "a"); Win(session, "b"); Win(session, "c");
            Win(session, "a", replay: true);
            AssertNext(session, "b", replay: true);
            Win(session, "b", replay: true);
            AssertNext(session, "c", replay: true);
            Win(session, "c", replay: true);
            AssertNext(session, "b", replay: false);
            Assert.That(session.Levels.Progress.ResumeLevelId, Is.EqualTo("b"));
            Assert.That(store.SaveCount, Is.EqualTo(3));
            Win(session, "b");
            AssertNext(session, "c", replay: false);
        }

        [Test]
        public void NextRequiresActiveWinAndRestartClearsItsEligibility()
        {
            var session = new DropTheManProgressionSession(new MemoryStore(), "profile");
            Assert.That(session.TryGetNextLevelAfterWin(out _, out _, out _), Is.False);
            Assert.That(session.TryLoad(Levels, 1, 0), Is.True);
            session.BeginLevel("a", false);
            Assert.That(session.TryGetNextLevelAfterWin(out _, out _, out _), Is.False);
            session.RecordAcceptedWin();
            AssertNext(session, "b", replay: false);
            session.BeginLevel("a", true);
            Assert.That(session.TryGetNextLevelAfterWin(out _, out _, out _), Is.False);
        }

        [Test]
        public void NextSelectionCanBeRetriedWithoutStartingOrSavingDestination()
        {
            var store = new MemoryStore(); var session = Load(store);
            Win(session, "a"); Win(session, "b");
            Win(session, "a", replay: true);
            AssertNext(session, "b", replay: true);
            AssertNext(session, "b", replay: true);
            Assert.That(session.ActiveLevelId, Is.EqualTo("a"));
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(Load(store).Levels.GetContinueLevelId(), Is.EqualTo("c"));
            // Resume Campaign is explicit and independent of the replay's post-win destination.
            session.BeginLevel(session.Levels.GetContinueLevelId(), false);
            Assert.That(session.ActiveLevelId, Is.EqualTo("c"));
            Assert.That(session.IsReplay, Is.False);
        }

        [Test]
        public void ReplaySuccessorUsesSortedShippedEntriesRatherThanContiguousNumbers()
        {
            var data = new PlayerProgressData();
            data.MarkCompleted("early"); data.MarkCompleted("late");
            var policy = new DropTheManLevelProgression(data,
                new[] { new LevelCatalogMetadata("late", 8), new LevelCatalogMetadata("early", 3) }, 1, 0);
            Assert.That(policy.GetLevelAfterReplay("early", out var replay), Is.EqualTo("late"));
            Assert.That(replay, Is.True);
        }

        [Test]
        public void ReplayCannotSkipEarlierUnfinishedContentWhenNewLevelsWereInserted()
        {
            var data = new PlayerProgressData(); data.MarkCompleted("b");
            var policy = new DropTheManLevelProgression(data, Levels, 1, 0);
            Assert.That(policy.GetLevelAfterReplay("b", out var replay), Is.EqualTo("a"));
            Assert.That(replay, Is.False);
            Assert.That(policy.Progress.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void SingleLevelReplayReturnsToCampaignWithoutRepeatedAdvancement()
        {
            var store = new MemoryStore();
            var session = new DropTheManProgressionSession(store, "profile");
            Assert.That(session.TryLoad(new[] { Levels[0] }, 1, 0), Is.True);
            Win(session, "a"); Win(session, "a", replay: true);
            AssertNext(session, "a", replay: false);
            AssertNext(session, "a", replay: false);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Win(session, "a");
            session.RecordAcceptedWin();
            Assert.That(store.SaveCount, Is.EqualTo(2));
        }

        private static void Win(DropTheManProgressionSession session, string id, bool replay = false)
        {
            session.BeginLevel(id, replay);
            session.RecordAcceptedWin();
        }

        private static void AssertNext(DropTheManProgressionSession session, string id, bool replay)
        {
            Assert.That(session.TryGetNextLevelAfterWin(out var next, out var isReplay, out var error), Is.True, error);
            Assert.That(next, Is.EqualTo(id));
            Assert.That(isReplay, Is.EqualTo(replay));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RuntimeCompletionSavesOnlyAfterHoleClosesAndNeverAfterLoss(bool loseBeforeCompletion)
        {
            var store = new MemoryStore();
            var session = Load(store);
            session.BeginLevel("a", false);
            var provider = new DropTheManDevLevelDefinitionProvider(DropTheManDevLevelData.CreateDefault());
            Assert.That(provider.TryGetLevelDefinition(out var definition, out var failure), Is.True, failure);
            var built = new LevelRuntimeBuilder(new LevelRuntimeConstructionValidator()).Build(definition);
            Assert.That(built.Success, Is.True, built.FailureReason);
            var model = new DropTheManRuntimeModelBuilder().Build(definition, built.Context);
            Assert.That(model.Success, Is.True, model.FailureReason);
            var registry = new DropTheManViewRegistry();
            var socket = new GameObject("ProgressionTestSocket");
            try
            {
                var hole = new DeferredHoleView(socket.transform);
                var cat = new DeferredCatView();
                Assert.That(registry.RegisterHoleView(hole).Success, Is.True);
                Assert.That(registry.RegisterStickmanView(cat).Success, Is.True);
                var runtime = new DropTheManRuntimeController(model.RuntimeModel,
                    new GridWorldLayout(Vector3.zero, Vector2.one, Vector3.right, Vector3.forward),
                    registry, new GameStateSystem());
                int notifications = 0;
                runtime.TerminalOutcomeAcceptedNow += state =>
                {
                    notifications++;
                    if (state == GameState.Won) session.RecordAcceptedWin();
                };
                Assert.That(runtime.StartGameplay().Success, Is.True);
                Assert.That(runtime.BeginDragByHoleId(hole.RuntimeId).Success, Is.True);
                var moved = runtime.UpdateDragWorldPosition(new Vector3(3f, 0f, 2f));
                Assert.That(moved.Success, Is.True, moved.FailureReason);
                Assert.That(cat.Complete, Is.Not.Null);
                Assert.That(store.SaveCount, Is.Zero, "Collection trigger is not a win.");
                cat.Complete();
                Assert.That(hole.Complete, Is.Not.Null);
                Assert.That(store.SaveCount, Is.Zero, "Cat arrival is not a win.");
                if (loseBeforeCompletion) runtime.HandleTimerExpired();
                hole.Complete();
                hole.Complete();
                runtime.HandleTimerExpired();
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(store.SaveCount, Is.EqualTo(loseBeforeCompletion ? 0 : 1));
                Assert.That(Load(store).Levels.GetContinueLevelId(),
                    Is.EqualTo(loseBeforeCompletion ? "a" : "b"));
            }
            finally { Object.DestroyImmediate(socket); }
        }

        [Test]
        public void EditorProfilePathIsSeparateFromPlayerProfile()
        {
            var obj = new GameObject("ProgressionPathTest");
            try
            {
                var bootstrapper = obj.AddComponent<DropTheManDevSceneBootstrapper>();
                Assert.That(bootstrapper.ProgressSavePath, Does.EndWith("progress.editor.json"));
                Assert.That(bootstrapper.ProgressionEnabled, Is.True);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        private sealed class DeferredHoleView : IDropTheManHoleView
        {
            private readonly Transform _socket;
            public System.Action Complete;
            public string RuntimeId => "hole_red_01";
            public Vector3 WorldPosition { get; private set; } = new(1f, 0f, 2f);
            public DeferredHoleView(Transform socket) => _socket = socket;
            public void ApplyWorldPosition(Vector3 position) => WorldPosition = position;
            public void SetSelectable(bool selectable) { }
            public bool TryClaimCollectionSocket(string id, Vector3 position,
                out Transform socket, out string failure)
            { socket = _socket; failure = string.Empty; return true; }
            public bool TryPlayCompletionPresentation(System.Action callback, out string failure)
            { Complete = callback; failure = string.Empty; return true; }
        }

        private sealed class DeferredCatView : IDropTheManStickmanView
        {
            public System.Action Complete;
            public string RuntimeId => "stickman_red_01";
            public bool TryPlayCollectionPresentation(Transform socket, System.Action callback, out string failure)
            { Complete = callback; failure = string.Empty; return true; }
        }

        private static DropTheManLevelProgression Create(PlayerProgressData data = null, int first = 1, int last = 0) =>
            new(data ?? new PlayerProgressData(), Levels, first, last);
        private static void CompleteCampaign(DropTheManLevelProgression policy)
        { foreach (var level in Levels) policy.RecordWin(level.LevelId, false); }
        private static DropTheManProgressionSession Load(MemoryStore store)
        {
            var session = new DropTheManProgressionSession(store, "profile");
            Assert.That(session.TryLoad(Levels, 1, 0), Is.True, session.Error);
            return session;
        }

        private sealed class MemoryStore : IProgressSaveLoadService
        {
            public bool FailSave, InvalidLoad;
            public int SaveCount;
            private PlayerProgressSnapshot _saved;
            public ProgressLoadResult Load(string path)
            {
                if (InvalidLoad) return new ProgressLoadResult(ProgressLoadStatus.InvalidData, null, "corrupt");
                if (_saved == null) return new ProgressLoadResult(ProgressLoadStatus.NotFound, null);
                PlayerProgressData.TryRestore(_saved, out var progress, out _);
                return new ProgressLoadResult(ProgressLoadStatus.Loaded, progress);
            }
            public ProgressSaveResult Save(PlayerProgressData progress, string path)
            {
                SaveCount++;
                if (FailSave) return new ProgressSaveResult(false, "disk failure");
                _saved = progress.CaptureSnapshot();
                return new ProgressSaveResult(true);
            }
        }
    }
}
