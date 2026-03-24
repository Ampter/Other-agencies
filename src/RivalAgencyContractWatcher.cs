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
        private const double DefaultCheckIntervalSeconds = 5d;
        private const double DefaultNearExpiryThresholdKerbinDays = 0.5d;
        private const double DefaultOfferAgeThresholdKerbinDays = 3d;
        private const float DefaultMinTakeoverChance = 0.08f;
        private const float DefaultMaxTakeoverChance = 0.30f;
        private const double DefaultLateGameStartKerbinYears = 3d;
        private const double DefaultEndGameStartKerbinYears = 6d;
        private const string LogPrefix = "[OtherAgencies]";
        private const string ConfigFileName = "agencies.cfg";
        private const string ConfigRootNodeName = "OTHER_AGENCIES";
        private const string AgencyNodeName = "AGENCY";
        private const string SettingsNodeName = "SETTINGS";
        private const string PreferenceNodeName = "PREFERENCE";

        private readonly List<Agency> agencies = new List<Agency>();
        private readonly Dictionary<string, Func<Contract, bool>> preferenceMap =
            new Dictionary<string, Func<Contract, bool>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string[]> preferenceKeywords =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Guid> attemptedContracts = new HashSet<Guid>();
        private double checkIntervalSeconds;
        private double nearExpiryThresholdSeconds;
        private double offerAgeThresholdSeconds;
        private float minTakeoverChance;
        private float maxTakeoverChance;
        private double lateGameStartSeconds;
        private double endGameStartSeconds;
        private double nextCheckTime;

        private void Start()
        {
            ResetConfigToDefaults();
            RegisterBuiltInPreferences();
            LoadAgenciesConfigOrDefault();

            nextCheckTime = Planetarium.GetUniversalTime() + checkIntervalSeconds;
        }

        private void ResetConfigToDefaults()
        {
            checkIntervalSeconds = DefaultCheckIntervalSeconds;
            nearExpiryThresholdSeconds = DefaultNearExpiryThresholdKerbinDays * KerbinDaySeconds;
            offerAgeThresholdSeconds = DefaultOfferAgeThresholdKerbinDays * KerbinDaySeconds;
            minTakeoverChance = DefaultMinTakeoverChance;
            maxTakeoverChance = DefaultMaxTakeoverChance;
            lateGameStartSeconds = DefaultLateGameStartKerbinYears * KerbinYearSeconds;
            endGameStartSeconds = DefaultEndGameStartKerbinYears * KerbinYearSeconds;

            preferenceKeywords.Clear();
            preferenceKeywords["launch_orbit"] = new[] { "launch", "orbit", "sub-orbital", "satellite", "first launch" };
            preferenceKeywords["satellite_comms"] = new[] { "satellite", "relay", "antenna", "comms", "commnet" };
            preferenceKeywords["mun_minmus"] = new[] { "mun", "minmus" };
            preferenceKeywords["duna_late_game"] = new[] { "duna", "interplanetary", "transfer window" };
            preferenceKeywords["science"] = new[] { "science", "experiment", "temperature", "crew report", "goo", "materials bay" };
            preferenceKeywords["part_test"] = new[] { "test", "engine", "part", "altitude", "activate" };
            preferenceKeywords["exploration"] = new[] { "explore", "flyby", "first", "discover", "reach" };
            preferenceKeywords["outer_planets_end_game"] = new[] { "jool", "eeloo", "outer", "tylo", "vall", "bop", "pol" };
            preferenceKeywords["rescue_transport"] = new[] { "rescue", "passenger", "tourist", "crew", "transport" };
            preferenceKeywords["urgent"] = Array.Empty<string>();
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
                LoadDefaultAgencies();
                return;
            }

            ConfigNode root = ConfigNode.Load(configPath);
            if (root == null)
            {
                LoadDefaultAgencies();
                return;
            }

            ConfigNode config = root.HasNode(ConfigRootNodeName) ? root.GetNode(ConfigRootNodeName) : root;
            ApplySettings(config);
            ApplyPreferenceOverrides(config);

            ConfigNode[] agencyNodes = config.GetNodes(AgencyNodeName);
            if (agencyNodes == null || agencyNodes.Length == 0)
            {
                LoadDefaultAgencies();
                return;
            }

            foreach (ConfigNode agencyNode in agencyNodes)
            {
                TryAddAgencyFromNode(agencyNode);
            }

            if (agencies.Count == 0)
            {
                LoadDefaultAgencies();
                return;
            }
        }

        private void ApplySettings(ConfigNode config)
        {
            if (config == null || !config.HasNode(SettingsNodeName))
            {
                return;
            }

            ConfigNode settings = config.GetNode(SettingsNodeName);
            if (settings == null)
            {
                return;
            }

            checkIntervalSeconds = ReadDouble(settings, "checkIntervalSeconds", checkIntervalSeconds, 0.2d, 300d);

            double nearExpiryThresholdKerbinDays = ReadDouble(
                settings,
                "nearExpiryThresholdKerbinDays",
                nearExpiryThresholdSeconds / KerbinDaySeconds,
                0d,
                1000d);
            nearExpiryThresholdSeconds = nearExpiryThresholdKerbinDays * KerbinDaySeconds;

            double offerAgeThresholdKerbinDays = ReadDouble(
                settings,
                "offerAgeThresholdKerbinDays",
                offerAgeThresholdSeconds / KerbinDaySeconds,
                0d,
                1000d);
            offerAgeThresholdSeconds = offerAgeThresholdKerbinDays * KerbinDaySeconds;

            minTakeoverChance = ReadFloat(settings, "minTakeoverChance", minTakeoverChance, 0f, 1f);
            maxTakeoverChance = ReadFloat(settings, "maxTakeoverChance", maxTakeoverChance, 0f, 1f);
            if (maxTakeoverChance < minTakeoverChance)
            {
                float swap = minTakeoverChance;
                minTakeoverChance = maxTakeoverChance;
                maxTakeoverChance = swap;
            }

            double lateGameStartKerbinYears = ReadDouble(
                settings,
                "lateGameStartKerbinYears",
                lateGameStartSeconds / KerbinYearSeconds,
                0d,
                1000d);
            lateGameStartSeconds = lateGameStartKerbinYears * KerbinYearSeconds;

            double endGameStartKerbinYears = ReadDouble(
                settings,
                "endGameStartKerbinYears",
                endGameStartSeconds / KerbinYearSeconds,
                0d,
                1000d);
            endGameStartSeconds = endGameStartKerbinYears * KerbinYearSeconds;
        }

        private void ApplyPreferenceOverrides(ConfigNode config)
        {
            if (config == null)
            {
                return;
            }

            ConfigNode[] nodes = config.GetNodes(PreferenceNodeName);
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            foreach (ConfigNode node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                string id = (node.GetValue("id") ?? string.Empty).Trim();
                string keywordsRaw = node.GetValue("keywords") ?? string.Empty;
                if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(keywordsRaw))
                {
                    continue;
                }

                string[] keywords = SplitKeywords(keywordsRaw);
                if (keywords.Length == 0)
                {
                    continue;
                }

                preferenceKeywords[id] = keywords;
            }
        }

        private static string[] SplitKeywords(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static double ReadDouble(ConfigNode node, string key, double defaultValue, double min, double max)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            string raw = node.GetValue(key);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return defaultValue;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static float ReadFloat(ConfigNode node, string key, float defaultValue, float min, float max)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            string raw = node.GetValue(key);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return defaultValue;
            }

            return Mathf.Clamp(value, min, max);
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
                return;
            }

            if (!preferenceMap.TryGetValue(preferenceId, out Func<Contract, bool> preference))
            {
                return;
            }

            if (!float.TryParse(aggressionRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float aggression))
            {
                aggression = 0.35f;
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
        }

        private void AddAgency(string name, string preferenceId, float aggression, string completionFlavor)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!preferenceMap.TryGetValue(preferenceId, out Func<Contract, bool> preference))
            {
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

            nextCheckTime = now + checkIntervalSeconds;
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

                ExpireContract(contract, winner);
            }
        }

        private bool IsNearExpiry(Contract contract, double now)
        {
            if (contract.DateExpire <= 0d)
            {
                return false;
            }

            double remaining = contract.DateExpire - now;
            return remaining > 0d && remaining <= nearExpiryThresholdSeconds;
        }

        private bool HasExceededOfferAge(Contract contract, double now)
        {
            if (contract.TimeExpiry <= 0d || contract.DateExpire <= 0d)
            {
                return false;
            }

            double offeredAt = contract.DateExpire - contract.TimeExpiry;
            return (now - offeredAt) >= offerAgeThresholdSeconds;
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
                float chance = Mathf.Clamp(agency.Aggression * relevance, minTakeoverChance, maxTakeoverChance);
                float roll = UnityEngine.Random.value;

                if (roll <= chance)
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
            if (!TryRemoveOfferedContract(contract))
            {
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
            return Planetarium.GetUniversalTime() >= lateGameStartSeconds
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
            return Planetarium.GetUniversalTime() >= endGameStartSeconds
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
            if (!preferenceKeywords.TryGetValue(preferenceId, out string[] keywords))
            {
                return false;
            }

            return ContainsAnyText(contract, keywords);
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
