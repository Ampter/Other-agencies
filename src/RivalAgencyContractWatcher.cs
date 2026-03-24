using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        private const double KerbinDaySeconds = 6d * 60d * 60d;
        private const double KerbinYearSeconds = 426d * KerbinDaySeconds;
        private const double LateGameStartSeconds = 3d * KerbinYearSeconds;
        private const double EndGameStartSeconds = 6d * KerbinYearSeconds;
        private const double CheckIntervalSeconds = 5d;
        private const double NearExpiryThresholdSeconds = 0.5d * KerbinDaySeconds;
        private const double OfferAgeThresholdSeconds = 3d * KerbinDaySeconds;
        private const string LogPrefix = "[OtherAgencies]";
        private const string ConfigFileName = "agencies.cfg";
        private const string ConfigRootNodeName = "OTHER_AGENCIES";
        private const string AgencyNodeName = "AGENCY";

        private readonly List<Agency> agencies = new List<Agency>();
        private readonly Dictionary<string, Func<Contract, bool>> preferenceMap =
            new Dictionary<string, Func<Contract, bool>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Guid> attemptedContracts = new HashSet<Guid>();
        private double nextCheckTime;

        private void Start()
        {
            agencies.Clear();
            RegisterBuiltInPreferences();
            LoadAgenciesConfigOrDefault();

            nextCheckTime = Planetarium.GetUniversalTime() + CheckIntervalSeconds;
            Debug.Log($"{LogPrefix} initialized with {agencies.Count} agencies.");
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

        private void LoadAgenciesConfigOrDefault()
        {
            agencies.Clear();

            string configPath = ResolveAgencyConfigPath();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                Debug.LogWarning($"{LogPrefix} agencies.cfg not found, using built-in defaults.");
                LoadDefaultAgencies();
                return;
            }

            ConfigNode root = ConfigNode.Load(configPath);
            if (root == null)
            {
                Debug.LogWarning($"{LogPrefix} could not parse agencies.cfg at '{configPath}', using built-in defaults.");
                LoadDefaultAgencies();
                return;
            }

            ConfigNode config = root.HasNode(ConfigRootNodeName) ? root.GetNode(ConfigRootNodeName) : root;
            ConfigNode[] agencyNodes = config.GetNodes(AgencyNodeName);
            if (agencyNodes == null || agencyNodes.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} no AGENCY nodes in agencies.cfg, using built-in defaults.");
                LoadDefaultAgencies();
                return;
            }

            foreach (ConfigNode agencyNode in agencyNodes)
            {
                TryAddAgencyFromNode(agencyNode);
            }

            if (agencies.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} agencies.cfg loaded zero valid agencies, using built-in defaults.");
                LoadDefaultAgencies();
                return;
            }

            Debug.Log($"{LogPrefix} loaded {agencies.Count} agencies from '{configPath}'.");
        }

        private static string ResolveAgencyConfigPath()
        {
            string root = KSPUtil.ApplicationRootPath;
            if (string.IsNullOrEmpty(root))
            {
                return string.Empty;
            }

            string hyphenPath = Path.Combine(root, "GameData", "Other-Agencies", ConfigFileName);
            if (File.Exists(hyphenPath))
            {
                return hyphenPath;
            }

            string fallbackPath = Path.Combine(root, "GameData", "OtherAgencies", ConfigFileName);
            if (File.Exists(fallbackPath))
            {
                return fallbackPath;
            }

            return hyphenPath;
        }

        private void TryAddAgencyFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return;
            }

            string name = (node.GetValue("name") ?? string.Empty).Trim();
            string preferenceId = (node.GetValue("preference") ?? string.Empty).Trim();
            string aggressionRaw = (node.GetValue("aggression") ?? string.Empty).Trim();
            string completionFlavor = node.GetValue("completionFlavor") ?? string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"{LogPrefix} skipping AGENCY with missing 'name'.");
                return;
            }

            if (!preferenceMap.TryGetValue(preferenceId, out Func<Contract, bool> preference))
            {
                Debug.LogWarning($"{LogPrefix} skipping '{name}': unknown preference '{preferenceId}'.");
                return;
            }

            if (!float.TryParse(aggressionRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float aggression))
            {
                aggression = 0.35f;
                Debug.LogWarning($"{LogPrefix} agency '{name}' has invalid aggression '{aggressionRaw}', using {aggression:0.00}.");
            }

            agencies.Add(new Agency(name, preference, aggression, completionFlavor));
        }

        private void LoadDefaultAgencies()
        {
            AddAgency("KerbalX Industries", "launch_orbit", 0.40f, "KerbalX has successfully placed a satellite before you.");
            AddAgency("OrbitCorp", "satellite_comms", 0.48f, "OrbitCorp optimized this network deployment before you.");
            AddAgency("Munar Exploration Group", "mun_minmus", 0.38f, "Munar Exploration Group planted their flag first.");
            AddAgency("Duna Initiative", "duna_late_game", 0.50f, "Duna Initiative launched an elite interplanetary campaign ahead of you.");
            AddAgency("Kerbin Science Union", "science", 0.28f, "Kerbin Science Union published the experiment results before your team.");
            AddAgency("Industrial Assembly Co.", "part_test", 0.42f, "Industrial Assembly Co. validated the design before your engineers.");
            AddAgency("Deep Space Surveyors", "exploration", 0.36f, "Deep Space Surveyors logged that exploration milestone first.");
            AddAgency("Outer Planets Coalition", "outer_planets_end_game", 0.50f, "Outer Planets Coalition quietly secured this outer-system objective.");
            AddAgency("Kerbin Logistics Network", "rescue_transport", 0.34f, "Kerbin Logistics Network handled this crew operation before you.");
            AddAgency("SpeedRun Aerospace", "urgent", 0.46f, "SpeedRun Aerospace sniped the deadline before your launch window.");
            Debug.Log($"{LogPrefix} loaded built-in default agencies.");
        }

        private void AddAgency(string name, string preferenceId, float aggression, string completionFlavor)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!preferenceMap.TryGetValue(preferenceId, out Func<Contract, bool> preference))
            {
                Debug.LogWarning($"{LogPrefix} could not add agency '{name}' with unknown preference '{preferenceId}'.");
                return;
            }

            agencies.Add(new Agency(name, preference, aggression, completionFlavor));
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

            Debug.Log($"{LogPrefix} evaluation tick at UT={now:0.0}, offered={offeredContracts.Count}.");

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
                    Debug.Log($"{LogPrefix} skip '{contract.Title}': not near expiry and offer age below threshold.");
                    continue;
                }

                if (attemptedContracts.Contains(contract.ContractGuid))
                {
                    Debug.Log($"{LogPrefix} skip '{contract.Title}': takeover roll already attempted for this offer instance.");
                    continue;
                }

                Debug.Log($"{LogPrefix} evaluating '{contract.Title}': nearExpiry={nearExpiry}, exceededOfferAge={exceededOfferAge}.");
                attemptedContracts.Add(contract.ContractGuid);

                Agency winner = SelectWinningAgency(contract);
                if (winner == null)
                {
                    Debug.Log($"{LogPrefix} no agency won takeover roll for '{contract.Title}'.");
                    continue;
                }

                ExpireContract(contract, winner);
            }
        }

        private static bool IsNearExpiry(Contract contract, double now)
        {
            if (contract.DateExpire <= 0d)
            {
                return false;
            }

            double remaining = contract.DateExpire - now;
            return remaining > 0d && remaining <= NearExpiryThresholdSeconds;
        }

        private static bool HasExceededOfferAge(Contract contract, double now)
        {
            if (contract.TimeExpiry <= 0d || contract.DateExpire <= 0d)
            {
                return false;
            }

            double offeredAt = contract.DateExpire - contract.TimeExpiry;
            return (now - offeredAt) >= OfferAgeThresholdSeconds;
        }

        private Agency SelectWinningAgency(Contract contract)
        {
            List<Agency> matchingAgencies = agencies
                .Where(agency => agency != null && agency.Preference(contract))
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            if (matchingAgencies.Count == 0)
            {
                Debug.Log($"{LogPrefix} no matching agencies for '{contract.Title}'.");
                return null;
            }

            Debug.Log($"{LogPrefix} '{contract.Title}' matched {matchingAgencies.Count} agencies.");

            foreach (Agency agency in matchingAgencies)
            {
                float relevance = GetContractRelevance(contract, agency);
                float chance = Mathf.Clamp(agency.Aggression * relevance, 0.08f, 0.30f);
                float roll = UnityEngine.Random.value;
                Debug.Log($"{LogPrefix} roll '{contract.Title}' vs {agency.Name}: chance={chance:0.00}, roll={roll:0.00}.");

                if (roll <= chance)
                {
                    Debug.Log($"{LogPrefix} winner for '{contract.Title}' is {agency.Name}.");
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
            if (!TryRemoveOfferedContract(contract))
            {
                Debug.LogWarning($"{LogPrefix} failed to decline contract '{contract?.Title ?? "<null>"}'; skipping takeover.");
                return;
            }

            string contractMessage = $"{winner.Name} completed: {contract.Title}";
            ScreenMessages.PostScreenMessage(contractMessage, 6f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log($"{LogPrefix} {winner.Name} took '{contract.Title}'. State is now {contract.ContractState}.");

            if (!string.IsNullOrEmpty(winner.CompletionFlavor))
            {
                ScreenMessages.PostScreenMessage(winner.CompletionFlavor, 6f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private static bool TryRemoveOfferedContract(Contract contract)
        {
            if (contract == null || contract.ContractState != Contract.State.Offered)
            {
                return false;
            }

            bool declined = contract.Decline();
            bool removed = contract.ContractState != Contract.State.Offered;
            Debug.Log($"{LogPrefix} decline result for '{contract.Title}': declined={declined}, state={contract.ContractState}.");
            return removed || declined;
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
