using System;
using System.Collections.Generic;
using System.Linq;
using Contracts;
using UnityEngine;

namespace OtherAgencies
{
    internal sealed class Agency
    {
        public string Name { get; }
        public Func<Contract, bool> Preference { get; }
        public float Aggression { get; }
        public string CompletionFlavor { get; }

        public Agency(string name, Func<Contract, bool> preference, float aggression, string completionFlavor)
        {
            Name = name;
            Preference = preference;
            Aggression = Mathf.Clamp01(aggression);
            CompletionFlavor = completionFlavor;
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public sealed class RivalAgencyContractWatcher : MonoBehaviour
    {
        private const double CheckIntervalSeconds = 5d;
        private const double NearExpiryThresholdSeconds = 2d * 24d * 60d * 60d;
        private const double OfferAgeThresholdSeconds = 4d * 24d * 60d * 60d;

        private const double KerbinDaySeconds = 6d * 60d * 60d;
        private const double KerbinYearSeconds = 426d * KerbinDaySeconds;
        private const double LateGameStartSeconds = 3d * KerbinYearSeconds;
        private const double EndGameStartSeconds = 6d * KerbinYearSeconds;

        private readonly List<Agency> agencies = new List<Agency>();
        private double nextCheckTime;

        private void Start()
        {
            agencies.Clear();

            agencies.Add(new Agency(
                "KerbalX Industries",
                IsLaunchAndOrbitContract,
                0.40f,
                "KerbalX has successfully placed a satellite before you."));

            agencies.Add(new Agency(
                "OrbitCorp",
                IsSatelliteAndCommsContract,
                0.48f,
                "OrbitCorp optimized this network deployment before you."));

            agencies.Add(new Agency(
                "Munar Exploration Group",
                IsMunOrMinmusContract,
                0.38f,
                "Munar Exploration Group planted their flag first."));

            agencies.Add(new Agency(
                "Duna Initiative",
                IsDunaLateGameContract,
                0.50f,
                "Duna Initiative launched an elite interplanetary campaign ahead of you."));

            agencies.Add(new Agency(
                "Kerbin Science Union",
                IsScienceContract,
                0.28f,
                "Kerbin Science Union published the experiment results before your team."));

            agencies.Add(new Agency(
                "Industrial Assembly Co.",
                IsPartTestContract,
                0.42f,
                "Industrial Assembly Co. validated the design before your engineers."));

            agencies.Add(new Agency(
                "Deep Space Surveyors",
                IsExplorationContract,
                0.36f,
                "Deep Space Surveyors logged that exploration milestone first."));

            agencies.Add(new Agency(
                "Outer Planets Coalition",
                IsOuterPlanetsEndGameContract,
                0.50f,
                "Outer Planets Coalition quietly secured this outer-system objective."));

            agencies.Add(new Agency(
                "Kerbin Logistics Network",
                IsRescueAndTransportContract,
                0.34f,
                "Kerbin Logistics Network handled this crew operation before you."));

            agencies.Add(new Agency(
                "SpeedRun Aerospace",
                IsUrgentContract,
                0.46f,
                "SpeedRun Aerospace sniped the deadline before your launch window."));

            nextCheckTime = Planetarium.GetUniversalTime() + CheckIntervalSeconds;
        }

        private void FixedUpdate()
        {
            double now = Planetarium.GetUniversalTime();
            if (now < nextCheckTime)
            {
                return;
            }

            nextCheckTime = now + CheckIntervalSeconds;
            EvaluateOfferedContracts(now);
        }

        private void EvaluateOfferedContracts(double now)
        {
            if (ContractSystem.Instance == null || ContractSystem.Instance.Contracts == null)
            {
                return;
            }

            List<Contract> offeredContracts = ContractSystem.Instance.Contracts
                .Where(contract => contract != null && contract.ContractState == Contract.State.Offered)
                .ToList();

            if (offeredContracts.Count == 0)
            {
                return;
            }

            foreach (Contract contract in offeredContracts)
            {
                if (!ShouldEvaluateContract(contract, now))
                {
                    continue;
                }

                Agency winner = SelectWinningAgency(contract);
                if (winner == null)
                {
                    continue;
                }

                ExpireContract(contract, winner);
            }
        }

        private static bool ShouldEvaluateContract(Contract contract, double now)
        {
            if (contract == null)
            {
                return false;
            }

            return IsNearExpiry(contract, now) || HasExceededOfferAge(contract, now);
        }

        private static bool IsNearExpiry(Contract contract, double now)
        {
            return (contract.DateExpire - now) <= NearExpiryThresholdSeconds;
        }

        private static bool HasExceededOfferAge(Contract contract, double now)
        {
            return contract.DateAccepted > 0 && (now - contract.DateAccepted) >= OfferAgeThresholdSeconds;
        }

        private Agency SelectWinningAgency(Contract contract)
        {
            List<Agency> matchingAgencies = agencies
                .Where(agency => agency != null && agency.Preference(contract))
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            if (matchingAgencies.Count == 0)
            {
                return null;
            }

            foreach (Agency agency in matchingAgencies)
            {
                float relevance = GetContractRelevance(contract, agency);
                float chance = Mathf.Clamp(agency.Aggression * relevance, 0.20f, 0.50f);

                if (UnityEngine.Random.value <= chance)
                {
                    return agency;
                }
            }

            return null;
        }

        private static float GetContractRelevance(Contract contract, Agency agency)
        {
            return agency.Preference(contract) ? 1f : 0.7f;
        }

        private static void ExpireContract(Contract contract, Agency winner)
        {
            contract.SetState(Contract.State.Expired);

            string contractMessage = $"{winner.Name} completed: {contract.Title}";
            ScreenMessages.PostScreenMessage(contractMessage, 6f, ScreenMessageStyle.UPPER_CENTER);

            if (!string.IsNullOrEmpty(winner.CompletionFlavor))
            {
                ScreenMessages.PostScreenMessage(winner.CompletionFlavor, 6f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private static bool IsLaunchAndOrbitContract(Contract contract)
        {
            return ContainsAnyText(contract, "launch", "orbit", "sub-orbital", "satellite", "first launch");
        }

        private static bool IsSatelliteAndCommsContract(Contract contract)
        {
            return ContainsAnyText(contract, "satellite", "relay", "antenna", "comms", "commnet");
        }

        private static bool IsMunOrMinmusContract(Contract contract)
        {
            return ContainsAnyText(contract, "mun", "minmus");
        }

        private static bool IsDunaLateGameContract(Contract contract)
        {
            return Planetarium.GetUniversalTime() >= LateGameStartSeconds
                && ContainsAnyText(contract, "duna", "interplanetary", "transfer window");
        }

        private static bool IsScienceContract(Contract contract)
        {
            return ContainsAnyText(contract, "science", "experiment", "temperature", "crew report", "goo", "materials bay");
        }

        private static bool IsPartTestContract(Contract contract)
        {
            return ContainsAnyText(contract, "test", "engine", "part", "altitude", "activate");
        }

        private static bool IsExplorationContract(Contract contract)
        {
            return ContainsAnyText(contract, "explore", "flyby", "first", "discover", "reach");
        }

        private static bool IsOuterPlanetsEndGameContract(Contract contract)
        {
            return Planetarium.GetUniversalTime() >= EndGameStartSeconds
                && ContainsAnyText(contract, "jool", "eeloo", "outer", "tylo", "vall", "bop", "pol");
        }

        private static bool IsRescueAndTransportContract(Contract contract)
        {
            return ContainsAnyText(contract, "rescue", "passenger", "tourist", "crew", "transport");
        }

        private static bool IsUrgentContract(Contract contract)
        {
            return IsNearExpiry(contract, Planetarium.GetUniversalTime());
        }

        private static bool ContainsAnyText(Contract contract, params string[] values)
        {
            if (contract == null || values == null || values.Length == 0)
            {
                return false;
            }

            string title = contract.Title ?? string.Empty;
            string description = contract.Description ?? string.Empty;

            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (title.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                    || description.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}