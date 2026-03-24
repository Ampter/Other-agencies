#if KSP_STUBS
using System;
using System.Collections.Generic;

namespace Contracts
{
    public class Contract
    {
        public enum State
        {
            Generated,
            Offered,
            OfferExpired,
            Declined,
            Cancelled,
            Active,
            Completed,
            DeadlineExpired,
            Failed,
            Withdrawn
        }

        public State ContractState { get; private set; } = State.Offered;
        public Guid ContractGuid { get; } = Guid.NewGuid();
        public double DateExpire { get; set; }
        public double DateAccepted { get; set; }
        public double TimeExpiry { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public bool Decline()
        {
            ContractState = State.Declined;
            return true;
        }

        public void Withdraw()
        {
            ContractState = State.Withdrawn;
        }

        public void Kill()
        {
            ContractState = State.Withdrawn;
        }
    }

    public sealed class ContractSystem
    {
        private static readonly ContractSystem instance = new ContractSystem();

        public static ContractSystem Instance => instance;

        public List<Contract> Contracts { get; } = new List<Contract>();
    }
}

namespace UnityEngine
{
    public class MonoBehaviour
    {
    }

    public static class Mathf
    {
        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }

    public static class Random
    {
        private static readonly System.Random rng = new System.Random();

        public static float value => (float)rng.NextDouble();
    }

    public static class Debug
    {
        public static void Log(string message)
        {
        }

        public static void LogWarning(string message)
        {
        }
    }

    public enum ScreenMessageStyle
    {
        UPPER_CENTER
    }

    public static class ScreenMessages
    {
        public static void PostScreenMessage(string message, float duration, ScreenMessageStyle style)
        {
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class KSPAddon : Attribute
{
    public enum Startup
    {
        SpaceCentre
    }

    public KSPAddon(Startup startup, bool once)
    {
    }
}

public static class Planetarium
{
    public static double GetUniversalTime()
    {
        return 0d;
    }
}
#endif
