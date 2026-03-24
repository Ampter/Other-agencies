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

        public Agency(string name, Func<Contract, bool> preference, float aggression)
        {
            Name = name;
            Preference = preference;
            Aggression = Mathf.Clamp01(aggression);
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public sealed class RivalAgencyContractWatcher : MonoBehaviour
    {
        private const double CheckIntervalSeconds = 5d;
        private const double NearExpiryThresholdSeconds = 2d * 24d * 60d * 60d;
        private const double OfferAgeThresholdSeconds = 4d * 24d * 60d * 60d;

        private readonly List<Agency> agencies = new List<Agency>();
        private double nextCheckTime;

        private void Start()
        {
            agencies.Clear();
            agencies.Add(new Agency("OrbitCorp", IsOrbitContract, 0.50f));
            agencies.Add(new Agency("DeepSky Ventures", IsScienceContract, 0.35f));
            agencies.Add(new Agency("KerboFreight", IsTransportContract, 0.30f));
            agencies.Add(new Agency("Pioneer Systems", _ => true, 0.20f));

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
            if (agency.Preference(contract))
            {
                return 1f;
            }

            return 0.7f;
        }

        private static void ExpireContract(Contract contract, Agency winner)
        {
            contract.SetState(Contract.State.Expired);
            ScreenMessages.PostScreenMessage(
                $"{winner.Name} completed: {contract.Title}",
                6f,
                ScreenMessageStyle.UPPER_CENTER);
        }

        private static bool IsOrbitContract(Contract contract)
        {
            return ContainsText(contract, "orbit") || ContainsText(contract, "satellite");
        }

        private static bool IsScienceContract(Contract contract)
        {
            return ContainsText(contract, "science") || ContainsText(contract, "experiment");
        }

        private static bool IsTransportContract(Contract contract)
        {
            return ContainsText(contract, "station") || ContainsText(contract, "crew") || ContainsText(contract, "rescue");
        }

        private static bool ContainsText(Contract contract, string value)
        {
            if (contract == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            string title = contract.Title ?? string.Empty;
            string description = contract.Description ?? string.Empty;

            return title.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || description.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}