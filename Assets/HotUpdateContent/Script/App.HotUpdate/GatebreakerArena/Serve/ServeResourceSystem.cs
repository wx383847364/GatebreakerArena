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
            int safeMaxAmmo = Math.Max(0, maxAmmo);
            int safeInitialAmmo = Math.Max(0, Math.Min(initialAmmo, safeMaxAmmo));
            float safeCooldown = Math.Max(0f, baseServeCooldown);
            return new ServeResourceState
            {
                CurrentServeAmmo = safeInitialAmmo,
                MaxServeAmmo = safeMaxAmmo,
                OwnedBallsInField = 0,
                MaxOwnedBallsInField = Math.Max(0, maxOwnedBallsInField),
                ServeCooldownRemaining = safeInitialAmmo < safeMaxAmmo ? safeCooldown : 0f,
                BaseServeCooldown = safeCooldown,
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
            StartRechargeIfNeeded(state, cooldownScale);
            state.LastBlockReason = ServeBlockReason.None;
            return true;
        }

        public void Tick(ServeResourceState state, float deltaTime)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.CurrentServeAmmo >= state.MaxServeAmmo)
            {
                state.ServeCooldownRemaining = 0f;
                return;
            }

            if (state.BaseServeCooldown <= 0f)
            {
                state.CurrentServeAmmo = state.MaxServeAmmo;
                state.ServeCooldownRemaining = 0f;
                return;
            }

            if (state.ServeCooldownRemaining <= 0f)
            {
                state.ServeCooldownRemaining = state.BaseServeCooldown;
            }

            float remainingDelta = Math.Max(0f, deltaTime);
            while (remainingDelta > 0f && state.CurrentServeAmmo < state.MaxServeAmmo)
            {
                if (remainingDelta < state.ServeCooldownRemaining)
                {
                    state.ServeCooldownRemaining -= remainingDelta;
                    return;
                }

                remainingDelta -= state.ServeCooldownRemaining;
                state.CurrentServeAmmo += 1;
                state.ServeCooldownRemaining = state.CurrentServeAmmo < state.MaxServeAmmo
                    ? state.BaseServeCooldown
                    : 0f;
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

        private static void StartRechargeIfNeeded(ServeResourceState state, float cooldownScale)
        {
            if (state.CurrentServeAmmo >= state.MaxServeAmmo || state.ServeCooldownRemaining > 0f)
            {
                return;
            }

            float cooldown = state.BaseServeCooldown * Math.Max(0f, cooldownScale);
            if (cooldown <= 0f)
            {
                state.CurrentServeAmmo = state.MaxServeAmmo;
                state.ServeCooldownRemaining = 0f;
                return;
            }

            state.ServeCooldownRemaining = cooldown;
        }
    }
}
