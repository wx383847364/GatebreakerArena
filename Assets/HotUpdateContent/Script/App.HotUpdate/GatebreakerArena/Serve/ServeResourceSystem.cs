using System;
using App.HotUpdate.GatebreakerArena.Core;

namespace App.HotUpdate.GatebreakerArena.Serve
{
    public sealed class ServeResourceSystem
    {
        public ServeResourceState CreateState(
            int initialAmmo,
            int maxAmmo,
            int maxOwnedBallsInField,
            float baseServeCooldown)
        {
            return new ServeResourceState
            {
                CurrentServeAmmo = Math.Max(0, Math.Min(initialAmmo, maxAmmo)),
                MaxServeAmmo = Math.Max(0, maxAmmo),
                OwnedBallsInField = 0,
                MaxOwnedBallsInField = Math.Max(0, maxOwnedBallsInField),
                ServeCooldownRemaining = 0f,
                BaseServeCooldown = Math.Max(0f, baseServeCooldown),
                LastBlockReason = ServeBlockReason.None,
            };
        }

        public ServeBlockReason EvaluateCanServe(
            ServeResourceState state,
            int ballsInMatch,
            int maxBallsInMatch,
            bool playerDisabled)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            ServeBlockReason reason;
            if (playerDisabled)
            {
                reason = ServeBlockReason.PlayerDisabled;
            }
            else if (state.ServeCooldownRemaining > 0f)
            {
                reason = ServeBlockReason.CoolingDown;
            }
            else if (state.CurrentServeAmmo <= 0)
            {
                reason = ServeBlockReason.NoAmmo;
            }
            else if (state.OwnedBallsInField >= state.MaxOwnedBallsInField)
            {
                reason = ServeBlockReason.OwnedBallLimit;
            }
            else if (ballsInMatch >= maxBallsInMatch)
            {
                reason = ServeBlockReason.MatchBallLimit;
            }
            else
            {
                reason = ServeBlockReason.None;
            }

            state.LastBlockReason = reason;
            return reason;
        }

        public bool TryServe(
            ServeResourceState state,
            int ballsInMatch,
            int maxBallsInMatch,
            bool playerDisabled,
            float cooldownScale,
            out ServeBlockReason blockReason)
        {
            blockReason = EvaluateCanServe(state, ballsInMatch, maxBallsInMatch, playerDisabled);
            if (blockReason != ServeBlockReason.None)
            {
                return false;
            }

            state.CurrentServeAmmo -= 1;
            state.OwnedBallsInField += 1;
            state.ServeCooldownRemaining = state.BaseServeCooldown * Math.Max(0f, cooldownScale);
            state.LastBlockReason = ServeBlockReason.CoolingDown;
            return true;
        }

        public void Tick(ServeResourceState state, float deltaTime)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.ServeCooldownRemaining <= 0f)
            {
                return;
            }

            state.ServeCooldownRemaining = Math.Max(0f, state.ServeCooldownRemaining - Math.Max(0f, deltaTime));
            if (state.ServeCooldownRemaining <= 0f && state.CurrentServeAmmo < state.MaxServeAmmo)
            {
                state.CurrentServeAmmo += 1;
            }
        }

        public void OnOwnedBallRemoved(ServeResourceState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.OwnedBallsInField = Math.Max(0, state.OwnedBallsInField - 1);
        }
    }
}
