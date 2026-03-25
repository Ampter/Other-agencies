using System;
using System.Collections.Generic;
using System.Linq;
using Contracts;
using UnityEngine;

namespace OtherAgencies
{
    internal sealed class Agency
    {
        private readonly List<Func<Contract, bool>> preferences;

        public Agency(
            string name,
            IEnumerable<string> preferenceIds,
            IEnumerable<Func<Contract, bool>> preferenceChecks,
            float aggression,
            string completionFlavor)
        {
            Name = name ?? string.Empty;
            PreferenceIds = (preferenceIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            preferences = (preferenceChecks ?? Enumerable.Empty<Func<Contract, bool>>())
                .Where(check => check != null)
                .ToList();
            Aggression = Mathf.Clamp01(aggression);
            CompletionFlavor = completionFlavor ?? string.Empty;
        }

        public string Name { get; }
        public IReadOnlyList<string> PreferenceIds { get; }
        public float Aggression { get; }
        public string CompletionFlavor { get; }

        public bool MatchesContract(Contract contract)
        {
            return preferences.Any(preference => preference(contract));
        }

        public int GetMatchCount(Contract contract)
        {
            return preferences.Count(preference => preference(contract));
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public sealed class RivalAgencyContractWatcher : MonoBehaviour
    {
        private const string LogPrefix = "[OtherAgencies]";

        private readonly List<Agency> agencies = new List<Agency>();
        private readonly Dictionary<string, Func<Contract, bool>> preferenceMap =
            new Dictionary<string, Func<Contract, bool>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string[]> preferenceKeywords =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Guid> attemptedContracts = new HashSet<Guid>();

        private ContractWatcherSettings settings = new ContractWatcherSettings();
        private double nextCheckTime;

        private void Start()
        {
            RegisterBuiltInPreferences();
            LoadConfig();
            nextCheckTime = Planetarium.GetUniversalTime() + settings.CheckIntervalSeconds;
        }

        private void FixedUpdate()
        {
            double now = Planetarium.GetUniversalTime();
            if (now < nextCheckTime)
            {
                return;
            }

            nextCheckTime = now + settings.CheckIntervalSeconds;
            EvaluateOfferedContracts(now);
        }

        private void LoadConfig()
        {
            agencies.Clear();
            preferenceKeywords.Clear();

            OtherAgenciesConfig config = OtherAgenciesConfigLoader.Load();
            settings = config.WatcherSettings ?? new ContractWatcherSettings();

            foreach (KeyValuePair<string, string[]> pair in config.PreferenceKeywords)
            {
                preferenceKeywords[pair.Key] = pair.Value ?? Array.Empty<string>();
            }

            foreach (AgencyConfigDefinition agencyDefinition in config.Agencies)
            {
                TryAddAgency(agencyDefinition);
            }
        }

        private void RegisterBuiltInPreferences()
        {
            preferenceMap.Clear();
            preferenceMap["launch_orbit"] = IsLaunchAndOrbitContract;
            preferenceMap["satellite_comms"] = IsSatelliteAndCommsContract;
            preferenceMap["mun_minmus"] = IsMunOrMinmusContract;
            preferenceMap["duna_late_game"] = IsDunaLateGameContract;
            preferenceMap["science"] = IsScienceContract;
            preferenceMap["part_test"] = IsPartTestContract;
            preferenceMap["exploration"] = IsExplorationContract;
            preferenceMap["outer_planets_end_game"] = IsOuterPlanetsEndGameContract;
            preferenceMap["rescue_transport"] = IsRescueAndTransportContract;
            preferenceMap["urgent"] = IsUrgentContract;
        }

        private void TryAddAgency(AgencyConfigDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Name))
            {
                return;
            }

            List<string> resolvedIds = new List<string>();
            List<Func<Contract, bool>> checks = new List<Func<Contract, bool>>();
            foreach (string preferenceId in definition.PreferenceIds)
            {
                if (string.IsNullOrEmpty(preferenceId)
                    || !preferenceMap.TryGetValue(preferenceId, out Func<Contract, bool> check))
                {
                    continue;
                }

                resolvedIds.Add(preferenceId);
                checks.Add(check);
            }

            if (checks.Count == 0)
            {
                return;
            }

            agencies.Add(new Agency(
                definition.Name,
                resolvedIds,
                checks,
                definition.Aggression,
                definition.CompletionFlavor));
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
                attemptedContracts.Clear();
                return;
            }

            HashSet<Guid> offeredIds = new HashSet<Guid>(offeredContracts.Select(contract => contract.ContractGuid));
            attemptedContracts.RemoveWhere(id => !offeredIds.Contains(id));

            foreach (Contract contract in offeredContracts)
            {
                bool nearExpiry = IsNearExpiry(contract, now);
                bool exceededOfferAge = HasExceededOfferAge(contract, now);
                if (!nearExpiry && !exceededOfferAge)
                {
                    continue;
                }

                if (attemptedContracts.Contains(contract.ContractGuid))
                {
                    continue;
                }

                attemptedContracts.Add(contract.ContractGuid);
                Agency winner = SelectWinningAgency(contract);
                if (winner == null)
                {
                    continue;
                }

                if (!ExpireContract(contract, winner))
                {
                    continue;
                }

                RivalSpaceRaceScenario.Instance?.NotifyContractStolen(winner.Name, contract);
            }
        }

        private bool IsNearExpiry(Contract contract, double now)
        {
            if (contract.DateExpire <= 0d)
            {
                return false;
            }

            double remaining = contract.DateExpire - now;
            return remaining > 0d && remaining <= settings.NearExpiryThresholdSeconds;
        }

        private bool HasExceededOfferAge(Contract contract, double now)
        {
            if (contract.TimeExpiry <= 0d || contract.DateExpire <= 0d)
            {
                return false;
            }

            double offeredAt = contract.DateExpire - contract.TimeExpiry;
            return (now - offeredAt) >= settings.OfferAgeThresholdSeconds;
        }

        private Agency SelectWinningAgency(Contract contract)
        {
            List<Agency> matchingAgencies = agencies
                .Where(agency => agency != null && agency.MatchesContract(contract))
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            if (matchingAgencies.Count == 0)
            {
                return null;
            }

            foreach (Agency agency in matchingAgencies)
            {
                float relevance = GetContractRelevance(contract, agency);
                float chance = Mathf.Clamp(
                    agency.Aggression * relevance,
                    settings.MinTakeoverChance,
                    settings.MaxTakeoverChance);

                if (UnityEngine.Random.value <= chance)
                {
                    return agency;
                }
            }

            return null;
        }

        private static float GetContractRelevance(Contract contract, Agency agency)
        {
            int matches = agency.GetMatchCount(contract);
            if (matches <= 0)
            {
                return 0.7f;
            }

            return Mathf.Clamp(1f + ((matches - 1) * 0.12f), 1f, 1.3f);
        }

        private static bool ExpireContract(Contract contract, Agency winner)
        {
            if (!TryRemoveOfferedContract(contract))
            {
                return false;
            }

            string contractMessage = $"{winner.Name} completed: {contract.Title}";
            ScreenMessages.PostScreenMessage(contractMessage, 6f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log($"{LogPrefix} {winner.Name} took '{contract.Title}'. State is now {contract.ContractState}.");

            if (!string.IsNullOrEmpty(winner.CompletionFlavor))
            {
                ScreenMessages.PostScreenMessage(winner.CompletionFlavor, 6f, ScreenMessageStyle.UPPER_CENTER);
            }

            return true;
        }

        private static bool TryRemoveOfferedContract(Contract contract)
        {
            if (contract == null || contract.ContractState != Contract.State.Offered)
            {
                return false;
            }

            bool declined = contract.Decline();
            bool removed = contract.ContractState != Contract.State.Offered;
            return removed || declined;
        }

        private bool IsLaunchAndOrbitContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "launch_orbit");
        }

        private bool IsSatelliteAndCommsContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "satellite_comms");
        }

        private bool IsMunOrMinmusContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "mun_minmus");
        }

        private bool IsDunaLateGameContract(Contract contract)
        {
            return Planetarium.GetUniversalTime() >= settings.LateGameStartSeconds
                && MatchesPreferenceKeywords(contract, "duna_late_game");
        }

        private bool IsScienceContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "science");
        }

        private bool IsPartTestContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "part_test");
        }

        private bool IsExplorationContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "exploration");
        }

        private bool IsOuterPlanetsEndGameContract(Contract contract)
        {
            return Planetarium.GetUniversalTime() >= settings.EndGameStartSeconds
                && MatchesPreferenceKeywords(contract, "outer_planets_end_game");
        }

        private bool IsRescueAndTransportContract(Contract contract)
        {
            return MatchesPreferenceKeywords(contract, "rescue_transport");
        }

        private bool IsUrgentContract(Contract contract)
        {
            return IsNearExpiry(contract, Planetarium.GetUniversalTime());
        }

        private bool MatchesPreferenceKeywords(Contract contract, string preferenceId)
        {
            return preferenceKeywords.TryGetValue(preferenceId, out string[] keywords)
                && ContainsAnyText(contract, keywords);
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
