using System;
using System.Collections.Generic;
using System.Linq;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public sealed class LockstepSession
    {
        public const int SimulationFps = 30;
        public const int InputDelay = 3;
        private const int SyncWaitingMissingFrames = 6;
        private const float AbortMissingInputSeconds = 5f;

        private readonly Dictionary<int, Dictionary<int, LockstepInputFrame>> _inputsByFrame =
            new Dictionary<int, Dictionary<int, LockstepInputFrame>>();
        private readonly SortedDictionary<int, LockstepFrameBundle> _pendingBundles =
            new SortedDictionary<int, LockstepFrameBundle>();
        private readonly List<ChecksumReport> _desyncReports = new List<ChecksumReport>();
        private readonly List<LockstepFrameBundle> _startupBundles = new List<LockstepFrameBundle>();
        private readonly List<int> _waitingSlots = new List<int>();
        private readonly Dictionary<int, int> _slotToPlayerId = new Dictionary<int, int>();
        private readonly Dictionary<int, uint> _authorityInputSeqBySlot = new Dictionary<int, uint>();
        private readonly HashSet<int> _activeSlots = new HashSet<int>();
        private uint _localInputSeq;
        private uint _bundleSeq;
        private float _waitingSeconds;
        private int _nextBundleFrame;
        private int _nextLocalInputFrame;
        private int _localSlotIndex = -1;
        private int _localPlayerId;
        private bool _isHost;
        private string _error = string.Empty;

        public event Action<LockstepInputFrame> LocalInputReady;
        public event Action<LockstepFrameBundle> FrameBundleReady;
        public event Action<ChecksumReport> ChecksumReportReady;
        public event Action<MatchAbortReason, string> Aborted;

        public LockstepSyncState State { get; private set; } = LockstepSyncState.Idle;
        public int LatestConfirmedFrame { get; private set; } = -1;
        public MatchAbortReason AbortReason { get; private set; } = MatchAbortReason.None;
        public bool IsHost => _isHost;
        public int LocalTargetFrame => _nextLocalInputFrame;
        public int HostNextBundleFrame => _nextBundleFrame;
        public int NextRequiredFrame => LatestConfirmedFrame + 1;

        public void StartHost(IEnumerable<RoomPlayerSnapshot> activePlayers, int localSlotIndex)
        {
            StartInternal(true, activePlayers, localSlotIndex);
        }

        public void StartClient(IEnumerable<RoomPlayerSnapshot> activePlayers, int localSlotIndex)
        {
            StartInternal(false, activePlayers, localSlotIndex);
        }

        public void Reset()
        {
            _inputsByFrame.Clear();
            _pendingBundles.Clear();
            _desyncReports.Clear();
            _startupBundles.Clear();
            _waitingSlots.Clear();
            _slotToPlayerId.Clear();
            _authorityInputSeqBySlot.Clear();
            _activeSlots.Clear();
            _localInputSeq = 0;
            _bundleSeq = 0;
            _waitingSeconds = 0f;
            _nextBundleFrame = 0;
            _nextLocalInputFrame = 0;
            _localSlotIndex = -1;
            _localPlayerId = 0;
            _isHost = false;
            _error = string.Empty;
            State = LockstepSyncState.Idle;
            LatestConfirmedFrame = -1;
            AbortReason = MatchAbortReason.None;
        }

        public void Tick(float deltaTime)
        {
            if (State == LockstepSyncState.Idle ||
                State == LockstepSyncState.Aborted ||
                State == LockstepSyncState.Desync)
            {
                return;
            }

            if (_isHost)
            {
                BuildAvailableHostBundles();
            }

            RefreshWaiting(deltaTime);
        }

        public LockstepInputFrame SubmitLocalInput(short moveAxisQ, short aimXQ, short aimYQ, ushort buttons)
        {
            if (State == LockstepSyncState.Idle || State == LockstepSyncState.Aborted)
            {
                return new LockstepInputFrame();
            }

            var input = new LockstepInputFrame(
                _localSlotIndex,
                _localPlayerId,
                _nextLocalInputFrame++,
                ++_localInputSeq,
                moveAxisQ,
                aimXQ,
                aimYQ,
                buttons);
            SubmitInput(input);
            LocalInputReady?.Invoke(input);
            return input;
        }

        public LockstepInputFrame SubmitHostInputForSlot(
            int slotIndex,
            int frameIndex,
            short moveAxisQ,
            short aimXQ,
            short aimYQ,
            ushort buttons)
        {
            if (!_isHost ||
                State == LockstepSyncState.Idle ||
                State == LockstepSyncState.Aborted ||
                State == LockstepSyncState.Desync ||
                !_activeSlots.Contains(slotIndex) ||
                frameIndex < _nextBundleFrame)
            {
                return new LockstepInputFrame();
            }

            if (_inputsByFrame.TryGetValue(frameIndex, out Dictionary<int, LockstepInputFrame> frameInputs) &&
                frameInputs.TryGetValue(slotIndex, out LockstepInputFrame existing))
            {
                return existing;
            }

            int playerId = ResolvePlayerId(slotIndex);
            if (playerId <= 0)
            {
                return new LockstepInputFrame();
            }

            uint inputSeq = NextAuthorityInputSeq(slotIndex);
            var input = new LockstepInputFrame(
                slotIndex,
                playerId,
                frameIndex,
                inputSeq,
                moveAxisQ,
                aimXQ,
                aimYQ,
                buttons);
            SubmitInput(input);
            return input;
        }

        public void SubmitInput(LockstepInputFrame input)
        {
            if (!_activeSlots.Contains(input.SlotIndex) ||
                input.PlayerId != ResolvePlayerId(input.SlotIndex) ||
                input.FrameIndex < 0)
            {
                return;
            }

            Dictionary<int, LockstepInputFrame> frameInputs = GetOrCreateFrameInputs(input.FrameIndex);
            frameInputs[input.SlotIndex] = input;
            if (_isHost)
            {
                BuildAvailableHostBundles();
            }
        }

        public void ReceiveFrameBundle(LockstepFrameBundle bundle)
        {
            if (bundle == null ||
                bundle.FrameIndex < NextRequiredFrame ||
                !BundleMatchesActiveSlots(bundle))
            {
                return;
            }

            _pendingBundles[bundle.FrameIndex] = bundle;
            ClearWaiting();
        }

        public LockstepFrameBundle[] ConsumeStartupBundles()
        {
            LockstepFrameBundle[] bundles = _startupBundles.ToArray();
            _startupBundles.Clear();
            return bundles;
        }

        public bool TryDequeueConfirmedFrame(out LockstepFrameBundle bundle)
        {
            int expectedFrame = NextRequiredFrame;
            if (_pendingBundles.TryGetValue(expectedFrame, out bundle))
            {
                _pendingBundles.Remove(expectedFrame);
                LatestConfirmedFrame = expectedFrame;
                ClearWaiting();
                return true;
            }

            bundle = null;
            return false;
        }

        public void SubmitChecksumReport(ChecksumReport report)
        {
            if (report == null)
            {
                return;
            }

            if (report.DesyncDetected)
            {
                _desyncReports.Add(report);
                State = LockstepSyncState.Desync;
                _error = "Checksum desync detected.";
            }

            ChecksumReportReady?.Invoke(report);
        }

        public void Abort(MatchAbortReason reason, string message)
        {
            if (State == LockstepSyncState.Aborted)
            {
                return;
            }

            AbortReason = reason;
            _error = message ?? string.Empty;
            State = LockstepSyncState.Aborted;
            Aborted?.Invoke(reason, _error);
        }

        public LockstepSnapshot CreateSnapshot()
        {
            return new LockstepSnapshot
            {
                State = State,
                SimulationFps = SimulationFps,
                InputDelay = InputDelay,
                LatestConfirmedFrame = LatestConfirmedFrame,
                NextRequiredFrame = NextRequiredFrame,
                LocalTargetFrame = LocalTargetFrame,
                WaitingSeconds = _waitingSeconds,
                IsWaitingForInput = State == LockstepSyncState.SyncWaiting,
                IsDesynced = State == LockstepSyncState.Desync,
                IsAborted = State == LockstepSyncState.Aborted,
                AbortReason = AbortReason,
                Error = _error,
                WaitingSlotIndexes = _waitingSlots.ToArray(),
                DesyncReports = _desyncReports.ToArray(),
            };
        }

        private void StartInternal(bool isHost, IEnumerable<RoomPlayerSnapshot> activePlayers, int localSlotIndex)
        {
            Reset();
            _isHost = isHost;
            _localSlotIndex = localSlotIndex;
            foreach (RoomPlayerSnapshot player in activePlayers ?? Array.Empty<RoomPlayerSnapshot>())
            {
                if (player == null || !player.IsActive)
                {
                    continue;
                }

                _activeSlots.Add(player.SlotIndex);
                _slotToPlayerId[player.SlotIndex] = player.PlayerId;
                if (player.SlotIndex == localSlotIndex)
                {
                    _localPlayerId = player.PlayerId;
                }
            }

            State = _activeSlots.Count > 0 ? LockstepSyncState.Running : LockstepSyncState.Idle;
            if (isHost && State == LockstepSyncState.Running)
            {
                SeedStartupBundles();
            }

            _nextLocalInputFrame = Math.Max(0, InputDelay - 1);
        }

        private void BuildAvailableHostBundles()
        {
            while (HasCompleteInputs(_nextBundleFrame))
            {
                Dictionary<int, LockstepInputFrame> frameInputs = _inputsByFrame[_nextBundleFrame];
                var inputs = _activeSlots
                    .OrderBy(slot => slot)
                    .Select(slot => frameInputs[slot])
                    .ToArray();
                var bundle = new LockstepFrameBundle
                {
                    FrameIndex = _nextBundleFrame,
                    BundleSeq = ++_bundleSeq,
                    Inputs = inputs,
                };
                ReceiveFrameBundle(bundle);
                FrameBundleReady?.Invoke(bundle);
                _inputsByFrame.Remove(_nextBundleFrame);
                _nextBundleFrame++;
            }
        }

        private bool HasCompleteInputs(int frameIndex)
        {
            if (!_inputsByFrame.TryGetValue(frameIndex, out Dictionary<int, LockstepInputFrame> frameInputs))
            {
                return false;
            }

            foreach (int slot in _activeSlots)
            {
                if (!frameInputs.ContainsKey(slot))
                {
                    return false;
                }
            }

            return true;
        }

        private void SeedStartupBundles()
        {
            int startupFrameCount = Math.Max(0, InputDelay - 1);
            for (int frameIndex = 0; frameIndex < startupFrameCount; frameIndex++)
            {
                LockstepInputFrame[] inputs = _activeSlots
                    .OrderBy(slot => slot)
                    .Select(slot => new LockstepInputFrame(
                        slot,
                        ResolvePlayerId(slot),
                        frameIndex,
                        0,
                        0,
                        0,
                        0,
                        0))
                    .ToArray();
                var bundle = new LockstepFrameBundle
                {
                    FrameIndex = frameIndex,
                    BundleSeq = ++_bundleSeq,
                    Inputs = inputs,
                };
                _pendingBundles[frameIndex] = bundle;
                _startupBundles.Add(bundle);
            }

            _nextBundleFrame = startupFrameCount;
        }

        private void RefreshWaiting(float deltaTime)
        {
            bool isWaiting = _isHost
                ? RefreshHostWaitingSlots()
                : !_pendingBundles.ContainsKey(NextRequiredFrame);
            if (!isWaiting)
            {
                ClearWaiting();
                return;
            }

            _waitingSeconds += Math.Max(0f, deltaTime);
            if (_waitingSeconds >= AbortMissingInputSeconds)
            {
                Abort(MatchAbortReason.MissingInputTimeout, "Lockstep input wait timed out.");
                return;
            }

            if (_waitingSeconds >= SyncWaitingMissingFrames / (float)SimulationFps)
            {
                State = LockstepSyncState.SyncWaiting;
            }
        }

        private bool RefreshHostWaitingSlots()
        {
            _waitingSlots.Clear();
            if (_pendingBundles.ContainsKey(NextRequiredFrame))
            {
                return false;
            }

            _inputsByFrame.TryGetValue(_nextBundleFrame, out Dictionary<int, LockstepInputFrame> frameInputs);
            foreach (int slot in _activeSlots.OrderBy(slot => slot))
            {
                if (frameInputs == null || !frameInputs.ContainsKey(slot))
                {
                    _waitingSlots.Add(slot);
                }
            }

            return _waitingSlots.Count > 0;
        }

        private bool BundleMatchesActiveSlots(LockstepFrameBundle bundle)
        {
            if (bundle.Inputs == null || bundle.Inputs.Length != _activeSlots.Count)
            {
                return false;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < bundle.Inputs.Length; i++)
            {
                LockstepInputFrame input = bundle.Inputs[i];
                if (input.FrameIndex != bundle.FrameIndex ||
                    !_activeSlots.Contains(input.SlotIndex) ||
                    input.PlayerId != ResolvePlayerId(input.SlotIndex) ||
                    !seen.Add(input.SlotIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private Dictionary<int, LockstepInputFrame> GetOrCreateFrameInputs(int frameIndex)
        {
            if (!_inputsByFrame.TryGetValue(frameIndex, out Dictionary<int, LockstepInputFrame> frameInputs))
            {
                frameInputs = new Dictionary<int, LockstepInputFrame>();
                _inputsByFrame.Add(frameIndex, frameInputs);
            }

            return frameInputs;
        }

        private int ResolvePlayerId(int slotIndex)
        {
            return _slotToPlayerId.TryGetValue(slotIndex, out int playerId) ? playerId : 0;
        }

        private uint NextAuthorityInputSeq(int slotIndex)
        {
            _authorityInputSeqBySlot.TryGetValue(slotIndex, out uint inputSeq);
            inputSeq++;
            _authorityInputSeqBySlot[slotIndex] = inputSeq;
            return inputSeq;
        }

        private void ClearWaiting()
        {
            _waitingSeconds = 0f;
            _waitingSlots.Clear();
            if (State == LockstepSyncState.SyncWaiting)
            {
                State = LockstepSyncState.Running;
            }
        }
    }
}
