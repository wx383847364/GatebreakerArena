using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Match;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Network
{
    public static class GatebreakerLockstepInputConverter
    {
        public const ushort ServeButton = 1 << 0;

        public static LockstepInputFrame FromPlayerInputFrame(
            PlayerInputFrame frame,
            int slotIndex,
            int frameIndex,
            uint inputSeq)
        {
            ushort buttons = frame.ServePressed ? ServeButton : (ushort)0;
            return new LockstepInputFrame(
                slotIndex,
                frame.PlayerId,
                frameIndex,
                inputSeq,
                QuantizeSignedUnit(frame.MoveAxis),
                QuantizeSignedUnit(frame.AimDirection.x),
                QuantizeSignedUnit(frame.AimDirection.y),
                buttons);
        }

        public static PlayerInputFrame ToPlayerInputFrame(LockstepInputFrame frame)
        {
            return new PlayerInputFrame(
                frame.PlayerId,
                DequantizeSignedUnit(frame.MoveAxisQ),
                (frame.Buttons & ServeButton) != 0,
                new Vector2(DequantizeSignedUnit(frame.AimXQ), DequantizeSignedUnit(frame.AimYQ)));
        }

        public static GatebreakerFrameInput ToGatebreakerFrameInput(LockstepInputFrame frame)
        {
            return new GatebreakerFrameInput(
                frame.PlayerId,
                DequantizeSignedUnit(frame.MoveAxisQ),
                (frame.Buttons & ServeButton) != 0,
                new Vector2(DequantizeSignedUnit(frame.AimXQ), DequantizeSignedUnit(frame.AimYQ)));
        }

        public static GatebreakerFrameInput[] ToGatebreakerFrameInputs(LockstepFrameBundle bundle)
        {
            if (bundle?.Inputs == null)
            {
                return new GatebreakerFrameInput[0];
            }

            var inputs = new GatebreakerFrameInput[bundle.Inputs.Length];
            for (int i = 0; i < bundle.Inputs.Length; i++)
            {
                inputs[i] = ToGatebreakerFrameInput(bundle.Inputs[i]);
            }

            return inputs;
        }

        public static short QuantizeSignedUnit(float value)
        {
            float clamped = Mathf.Clamp(value, -1f, 1f);
            return (short)Mathf.RoundToInt(clamped * short.MaxValue);
        }

        public static float DequantizeSignedUnit(short value)
        {
            return Mathf.Clamp(value / (float)short.MaxValue, -1f, 1f);
        }
    }
}
