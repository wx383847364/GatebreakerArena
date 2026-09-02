using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.BrickDuel;

namespace App.HotUpdate.GatebreakerArena.Phase
{
    /// <summary>
    /// Keeps an acknowledged match settlement alive independently from result-screen navigation.
    /// Persistence and reward authority remain in <see cref="PhaseProfileService"/>.
    /// </summary>
    public sealed class PhaseSettlementCoordinator : IDisposable
    {
        private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
        private readonly object _sync = new object();
        private readonly Dictionary<string, SettlementOperation> _operations =
            new Dictionary<string, SettlementOperation>(StringComparer.Ordinal);
        private readonly PhaseProfileService _profileService;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly TimeSpan _retryDelay;

        public PhaseSettlementCoordinator(
            PhaseProfileService profileService,
            TimeSpan? retryDelay = null)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _retryDelay = retryDelay.HasValue && retryDelay.Value >= TimeSpan.Zero
                ? retryDelay.Value
                : DefaultRetryDelay;
        }

        public Task<PhaseSettlementResult> EnsureSettlementAsync(
            string matchId,
            BrickDuelResult localResult,
            bool completedNormally)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return Task.FromResult(new PhaseSettlementResult(
                    PhaseSettlementStatus.NotEligible,
                    0));
            }

            lock (_sync)
            {
                if (_operations.TryGetValue(matchId, out SettlementOperation existing))
                {
                    // The first caller must receive the first persistence outcome promptly so
                    // result UI can expose retry state. Later callers await the same background
                    // operation until it reaches an idempotent final result.
                    return existing.FirstAttempt.IsCompleted
                        ? existing.FinalResult
                        : existing.FirstAttempt;
                }

                var firstAttempt = new TaskCompletionSource<PhaseSettlementResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Task<PhaseSettlementResult> finalResult = RunSettlementAsync(
                    matchId,
                    localResult,
                    completedNormally,
                    _lifetime.Token,
                    firstAttempt);
                var operation = new SettlementOperation(firstAttempt.Task, finalResult);
                _operations.Add(matchId, operation);
                ObserveCompletion(matchId, finalResult);
                return firstAttempt.Task;
            }
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            lock (_sync)
            {
                _operations.Clear();
            }
        }

        private async Task<PhaseSettlementResult> RunSettlementAsync(
            string matchId,
            BrickDuelResult localResult,
            bool completedNormally,
            CancellationToken cancellationToken,
            TaskCompletionSource<PhaseSettlementResult> firstAttempt)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PhaseSettlementResult settlement = await _profileService.SettleWithResultAsync(
                        matchId,
                        localResult,
                        completedNormally);
                    firstAttempt.TrySetResult(settlement);
                    if (settlement.IsFinal)
                    {
                        return settlement;
                    }

                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                firstAttempt.TrySetException(exception);
                throw;
            }
        }

        private async void ObserveCompletion(
            string matchId,
            Task<PhaseSettlementResult> operation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
                // Runner lifetime ended; cancellation is the expected shutdown path.
            }
            finally
            {
                lock (_sync)
                {
                    if (_operations.TryGetValue(matchId, out SettlementOperation current) &&
                        ReferenceEquals(current.FinalResult, operation))
                    {
                        _operations.Remove(matchId);
                    }
                }
            }
        }

        private sealed class SettlementOperation
        {
            public SettlementOperation(
                Task<PhaseSettlementResult> firstAttempt,
                Task<PhaseSettlementResult> finalResult)
            {
                FirstAttempt = firstAttempt;
                FinalResult = finalResult;
            }

            public Task<PhaseSettlementResult> FirstAttempt { get; }
            public Task<PhaseSettlementResult> FinalResult { get; }
        }
    }
}
