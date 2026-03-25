using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OtherAgencies
{
    internal static class OtherAgenciesTime
    {
        public const double KerbinDaySeconds = 6d * 60d * 60d;
        public const double KerbinYearSeconds = 426d * KerbinDaySeconds;
    }

    internal sealed class AgencyConfigDefinition
    {
        public string Name { get; set; } = string.Empty;
        public List<string> PreferenceIds { get; set; } = new List<string>();
        public float Aggression { get; set; } = 0.35f;
        public string CompletionFlavor { get; set; } = string.Empty;
    }

    internal sealed class ContractWatcherSettings
    {
        public double CheckIntervalSeconds { get; set; } = 5d;
        public double NearExpiryThresholdSeconds { get; set; } = 0.5d * OtherAgenciesTime.KerbinDaySeconds;
        public double OfferAgeThresholdSeconds { get; set; } = 3d * OtherAgenciesTime.KerbinDaySeconds;
        public float MinTakeoverChance { get; set; } = 0.08f;
        public float MaxTakeoverChance { get; set; } = 0.30f;
        public double LateGameStartSeconds { get; set; } = 3d * OtherAgenciesTime.KerbinYearSeconds;
        public double EndGameStartSeconds { get; set; } = 6d * OtherAgenciesTime.KerbinYearSeconds;
    }

    internal sealed class SpaceRaceSettings
    {
        public double SimulationCheckIntervalSeconds { get; set; } = 45d;
        public double PlayerProgressCheckIntervalSeconds { get; set; } = 2d;
        public int MaxCatchUpTicksPerUpdate { get; set; } = 10;
        public bool AnnounceStageChanges { get; set; } = true;
        public bool AnnounceResearchUnlocks { get; set; } = true;
        public bool AnnounceContractRewards { get; set; } = false;
    }

    internal sealed class SpaceRaceResearchStepDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double ScienceCost { get; set; }
        public string UnlockMessage { get; set; } = string.Empty;
    }

    internal sealed class SpaceRaceStageDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> RequiredResearchIds { get; set; } = new List<string>();
        public double FundsCost { get; set; }
        public int RequiredStolenContracts { get; set; }
        public double RequiredContractProgress { get; set; }
        public float CompletionChance { get; set; } = 0.5f;
        public float PassiveScienceChance { get; set; } = 0.5f;
        public double PassiveScienceMin { get; set; } = 0.25d;
        public double PassiveScienceMax { get; set; } = 1.25d;
        public float PassiveFundsChance { get; set; } = 0.5f;
        public double PassiveFundsMin { get; set; } = 500d;
        public double PassiveFundsMax { get; set; } = 2500d;
        public float ContractScienceMultiplier { get; set; } = 1f;
        public float ContractFundsMultiplier { get; set; } = 1f;
        public float ContractProgressMultiplier { get; set; } = 1f;
        public string CompletionMessage { get; set; } = string.Empty;
        public string MarkerBody { get; set; } = string.Empty;
        public double MarkerAltitude { get; set; }
        public string MarkerLabel { get; set; } = string.Empty;
        public string CraftFileName { get; set; } = string.Empty;
    }

    internal sealed class SpaceRaceDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string RivalAgencyName { get; set; } = string.Empty;
        public string PlayerGoal { get; set; } = "crewed_orbit";
        public string TargetBodyName { get; set; } = "Kerbin";
        public double OfferAfterUniversalTime { get; set; }
        public int OfferAfterCompletedContracts { get; set; }
        public bool RequireFirstLaunchReached { get; set; } = true;
        public string ContractTitle { get; set; } = string.Empty;
        public string ContractSynopsis { get; set; } = string.Empty;
        public string ContractDescription { get; set; } = string.Empty;
        public string ContractNotes { get; set; } = string.Empty;
        public string OfferedMessage { get; set; } = string.Empty;
        public string AcceptedMessage { get; set; } = string.Empty;
        public string CompletedMessage { get; set; } = string.Empty;
        public string FailedMessage { get; set; } = string.Empty;
        public double SupportFunds { get; set; } = 0d;
        public double CompletionFunds { get; set; } = 0d;
        public float CompletionScience { get; set; } = 0f;
        public float CompletionReputation { get; set; } = 0f;
        public float FailureReputation { get; set; } = 0f;
        public float FailureSciencePenalty { get; set; } = 0f;
        public double ContractScienceBaseMin { get; set; } = 0.2d;
        public double ContractScienceBaseMax { get; set; } = 1.0d;
        public double ContractFundsBaseMin { get; set; } = 300d;
        public double ContractFundsBaseMax { get; set; } = 1500d;
        public double ContractProgressBase { get; set; } = 1d;
        public float ContractCompletionScienceMultiplier { get; set; } = 1f;
        public float ContractCompletionFundsMultiplier { get; set; } = 0.1f;
        public List<SpaceRaceResearchStepDefinition> ResearchSteps { get; set; } = new List<SpaceRaceResearchStepDefinition>();
        public List<SpaceRaceStageDefinition> Stages { get; set; } = new List<SpaceRaceStageDefinition>();
    }

    internal sealed class OtherAgenciesConfig
    {
        public ContractWatcherSettings WatcherSettings { get; } = new ContractWatcherSettings();
        public SpaceRaceSettings RaceSettings { get; } = new SpaceRaceSettings();
        public Dictionary<string, string[]> PreferenceKeywords { get; } =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public List<AgencyConfigDefinition> Agencies { get; } = new List<AgencyConfigDefinition>();
        public List<SpaceRaceDefinition> SpaceRaces { get; } = new List<SpaceRaceDefinition>();
    }

    internal static class OtherAgenciesConfigLoader
    {
        private const string ConfigFileName = "agencies.cfg";
        private const string ConfigRootNodeName = "OTHER_AGENCIES";
        private const string SettingsNodeName = "SETTINGS";
        private const string PreferenceNodeName = "PREFERENCE";
        private const string AgencyNodeName = "AGENCY";
        private const string SpaceRaceSettingsNodeName = "SPACE_RACE_SETTINGS";
        private const string SpaceRaceNodeName = "SPACE_RACE";
        private const string SpaceRaceResearchNodeName = "RESEARCH_STEP";
        private const string SpaceRaceStageNodeName = "STAGE";

        public static OtherAgenciesConfig Load()
        {
            OtherAgenciesConfig config = CreateDefaultConfig();
            string configPath = ResolveAgencyConfigPath();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return config;
            }

            ConfigNode root = ConfigNode.Load(configPath);
            if (root == null)
            {
                return config;
            }

            ConfigNode loadedConfig = root.HasNode(ConfigRootNodeName) ? root.GetNode(ConfigRootNodeName) : root;
            if (loadedConfig == null)
            {
                return config;
            }

            ApplyWatcherSettings(loadedConfig, config.WatcherSettings);
            ApplyPreferenceOverrides(loadedConfig, config.PreferenceKeywords);
            ApplySpaceRaceSettings(loadedConfig, config.RaceSettings);
            ApplyAgencyNodes(loadedConfig, config);
            ApplySpaceRaceNodes(loadedConfig, config);

            if (config.Agencies.Count == 0)
            {
                AddDefaultAgencies(config);
            }

            if (config.SpaceRaces.Count == 0)
            {
                config.SpaceRaces.Add(CreateDefaultCrewedOrbitRace());
            }

            return config;
        }

        public static string ResolveAgencyConfigPath()
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

        private static OtherAgenciesConfig CreateDefaultConfig()
        {
            OtherAgenciesConfig config = new OtherAgenciesConfig();
            SeedDefaultPreferences(config.PreferenceKeywords);
            AddDefaultAgencies(config);
            config.SpaceRaces.Add(CreateDefaultCrewedOrbitRace());
            return config;
        }

        private static void SeedDefaultPreferences(IDictionary<string, string[]> preferences)
        {
            preferences.Clear();
            preferences["launch_orbit"] = new[] { "launch", "orbit", "sub-orbital", "satellite", "first launch" };
            preferences["satellite_comms"] = new[] { "satellite", "relay", "antenna", "comms", "commnet" };
            preferences["mun_minmus"] = new[] { "mun", "minmus" };
            preferences["duna_late_game"] = new[] { "duna", "interplanetary", "transfer window" };
            preferences["science"] = new[] { "science", "experiment", "temperature", "crew report", "goo", "materials bay" };
            preferences["part_test"] = new[] { "test", "engine", "part", "altitude", "activate" };
            preferences["exploration"] = new[] { "explore", "flyby", "first", "discover", "reach" };
            preferences["outer_planets_end_game"] = new[] { "jool", "eeloo", "outer", "tylo", "vall", "bop", "pol" };
            preferences["rescue_transport"] = new[] { "rescue", "passenger", "tourist", "crew", "transport" };
            preferences["urgent"] = Array.Empty<string>();
        }

        private static void AddDefaultAgencies(OtherAgenciesConfig config)
        {
            config.Agencies.Clear();
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "KerbalX Industries",
                PreferenceIds = new List<string> { "launch_orbit", "science", "urgent" },
                Aggression = 0.42f,
                CompletionFlavor = "KerbalX Industries leveraged fresh R&D data before your team could react."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "OrbitCorp",
                PreferenceIds = new List<string> { "satellite_comms" },
                Aggression = 0.48f,
                CompletionFlavor = "OrbitCorp optimized this network deployment before you."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Munar Exploration Group",
                PreferenceIds = new List<string> { "mun_minmus" },
                Aggression = 0.38f,
                CompletionFlavor = "Munar Exploration Group planted their flag first."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Duna Initiative",
                PreferenceIds = new List<string> { "duna_late_game" },
                Aggression = 0.50f,
                CompletionFlavor = "Duna Initiative launched an elite interplanetary campaign ahead of you."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Kerbin Science Union",
                PreferenceIds = new List<string> { "science" },
                Aggression = 0.28f,
                CompletionFlavor = "Kerbin Science Union published the experiment results before your team."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Industrial Assembly Co.",
                PreferenceIds = new List<string> { "part_test" },
                Aggression = 0.42f,
                CompletionFlavor = "Industrial Assembly Co. validated the design before your engineers."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Deep Space Surveyors",
                PreferenceIds = new List<string> { "exploration" },
                Aggression = 0.36f,
                CompletionFlavor = "Deep Space Surveyors logged that exploration milestone first."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Outer Planets Coalition",
                PreferenceIds = new List<string> { "outer_planets_end_game" },
                Aggression = 0.50f,
                CompletionFlavor = "Outer Planets Coalition quietly secured this outer-system objective."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "Kerbin Logistics Network",
                PreferenceIds = new List<string> { "rescue_transport" },
                Aggression = 0.34f,
                CompletionFlavor = "Kerbin Logistics Network handled this crew operation before you."
            });
            config.Agencies.Add(new AgencyConfigDefinition
            {
                Name = "SpeedRun Aerospace",
                PreferenceIds = new List<string> { "urgent" },
                Aggression = 0.46f,
                CompletionFlavor = "SpeedRun Aerospace sniped the deadline before your launch window."
            });
        }

        private static SpaceRaceDefinition CreateDefaultCrewedOrbitRace()
        {
            SpaceRaceDefinition race = new SpaceRaceDefinition
            {
                Id = "first_crewed_orbit",
                Name = "First Crewed Orbit",
                Enabled = true,
                RivalAgencyName = "KerbalX Industries",
                PlayerGoal = "crewed_orbit",
                TargetBodyName = "Kerbin",
                OfferAfterUniversalTime = 0d,
                OfferAfterCompletedContracts = 1,
                RequireFirstLaunchReached = true,
                ContractTitle = "World First Challenge: Beat KerbalX to Orbit",
                ContractSynopsis = "KerbalX Industries has challenged your program to put a Kerbal into orbit first.",
                ContractDescription =
                    "KerbalX Industries is openly racing you toward crewed orbit. If you accept, they will build through a linear program of sounding rockets, sub-orbital tests, contract-funded expansion, and a final orbital push. Declining carries no penalty. Losing will cost your program a painful amount of reputation and science.",
                ContractNotes =
                    "Accepting grants support funds up front. Winning secures milestone rewards. Losing to the rival program costs science and reputation.",
                OfferedMessage = "KerbalX Industries has issued a public crewed-orbit challenge.",
                AcceptedMessage = "The KerbalX orbital challenge is on. Their program has started moving.",
                CompletedMessage = "Your program reached crewed orbit before KerbalX Industries.",
                FailedMessage = "KerbalX Industries reached crewed orbit first. The setback hit your reputation and science teams hard.",
                SupportFunds = 5000d,
                CompletionFunds = 18000d,
                CompletionScience = 12f,
                CompletionReputation = 10f,
                FailureReputation = 25f,
                FailureSciencePenalty = 10f,
                ContractScienceBaseMin = 0.6d,
                ContractScienceBaseMax = 2.0d,
                ContractFundsBaseMin = 800d,
                ContractFundsBaseMax = 3200d,
                ContractProgressBase = 1.25d,
                ContractCompletionScienceMultiplier = 1.2f,
                ContractCompletionFundsMultiplier = 0.08f
            };

            race.ResearchSteps.Add(new SpaceRaceResearchStepDefinition
            {
                Id = "sounding_payloads",
                DisplayName = "Sounding Payloads",
                ScienceCost = 5d,
                UnlockMessage = "KerbalX Industries has unlocked sounding-payload research."
            });
            race.ResearchSteps.Add(new SpaceRaceResearchStepDefinition
            {
                Id = "basic_capsules",
                DisplayName = "Basic Capsules",
                ScienceCost = 9d,
                UnlockMessage = "KerbalX Industries has developed a recoverable capsule line."
            });
            race.ResearchSteps.Add(new SpaceRaceResearchStepDefinition
            {
                Id = "general_rocketry",
                DisplayName = "General Rocketry",
                ScienceCost = 18d,
                UnlockMessage = "KerbalX Industries has expanded into larger launch vehicles."
            });
            race.ResearchSteps.Add(new SpaceRaceResearchStepDefinition
            {
                Id = "flight_control",
                DisplayName = "Flight Control",
                ScienceCost = 15d,
                UnlockMessage = "KerbalX Industries has hardened its guidance and flight-control stack."
            });
            race.ResearchSteps.Add(new SpaceRaceResearchStepDefinition
            {
                Id = "crewed_orbit_systems",
                DisplayName = "Crewed Orbit Systems",
                ScienceCost = 28d,
                UnlockMessage = "KerbalX Industries is now capable of crewed orbital hardware."
            });

            race.Stages.Add(new SpaceRaceStageDefinition
            {
                Id = "sounding",
                Title = "Sounding Rocket",
                Description = "The rival is building a simple high-altitude test stack.",
                RequiredResearchIds = new List<string> { "sounding_payloads" },
                FundsCost = 2500d,
                CompletionChance = 0.62f,
                PassiveScienceChance = 0.55f,
                PassiveScienceMin = 0.5d,
                PassiveScienceMax = 1.4d,
                PassiveFundsChance = 0.75f,
                PassiveFundsMin = 500d,
                PassiveFundsMax = 1400d,
                ContractScienceMultiplier = 0.75f,
                ContractFundsMultiplier = 0.85f,
                ContractProgressMultiplier = 0.5f,
                CompletionMessage = "KerbalX Industries has flown a successful sounding rocket.",
                MarkerBody = "Kerbin",
                MarkerAltitude = 25000d,
                MarkerLabel = "KerbalX sounding rocket",
                CraftFileName = "OA_KerbalX_Sounding.craft"
            });
            race.Stages.Add(new SpaceRaceStageDefinition
            {
                Id = "suborbital",
                Title = "Sub-Orbital Flight",
                Description = "The rival is stretching into recoverable sub-orbital missions.",
                RequiredResearchIds = new List<string> { "basic_capsules", "general_rocketry" },
                FundsCost = 6500d,
                CompletionChance = 0.50f,
                PassiveScienceChance = 0.45f,
                PassiveScienceMin = 0.75d,
                PassiveScienceMax = 1.8d,
                PassiveFundsChance = 0.60f,
                PassiveFundsMin = 900d,
                PassiveFundsMax = 2200d,
                ContractScienceMultiplier = 1f,
                ContractFundsMultiplier = 1f,
                ContractProgressMultiplier = 0.8f,
                CompletionMessage = "KerbalX Industries has completed a crew-capable sub-orbital flight.",
                MarkerBody = "Kerbin",
                MarkerAltitude = 70000d,
                MarkerLabel = "KerbalX sub-orbital arc",
                CraftFileName = "OA_KerbalX_Suborbital.craft"
            });
            race.Stages.Add(new SpaceRaceStageDefinition
            {
                Id = "contracts",
                Title = "Contract Expansion",
                Description = "The rival is stealing contracts to bankroll flight-control and orbital hardware.",
                RequiredResearchIds = new List<string> { "flight_control" },
                FundsCost = 0d,
                RequiredStolenContracts = 4,
                RequiredContractProgress = 5d,
                CompletionChance = 0.58f,
                PassiveScienceChance = 0.25f,
                PassiveScienceMin = 0.2d,
                PassiveScienceMax = 0.8d,
                PassiveFundsChance = 0.35f,
                PassiveFundsMin = 400d,
                PassiveFundsMax = 1000d,
                ContractScienceMultiplier = 1.25f,
                ContractFundsMultiplier = 1.2f,
                ContractProgressMultiplier = 1.35f,
                CompletionMessage = "KerbalX Industries has turned stolen contracts into a funded orbital program.",
                MarkerBody = "Kerbin",
                MarkerAltitude = 0d,
                MarkerLabel = "KerbalX contract campaign",
                CraftFileName = string.Empty
            });
            race.Stages.Add(new SpaceRaceStageDefinition
            {
                Id = "orbit",
                Title = "Crewed Orbit",
                Description = "The rival is assembling its final orbital launch.",
                RequiredResearchIds = new List<string> { "crewed_orbit_systems" },
                FundsCost = 18000d,
                RequiredStolenContracts = 4,
                RequiredContractProgress = 6d,
                CompletionChance = 0.38f,
                PassiveScienceChance = 0.30f,
                PassiveScienceMin = 0.4d,
                PassiveScienceMax = 1.2d,
                PassiveFundsChance = 0.45f,
                PassiveFundsMin = 1000d,
                PassiveFundsMax = 2600d,
                ContractScienceMultiplier = 1.1f,
                ContractFundsMultiplier = 1.3f,
                ContractProgressMultiplier = 1f,
                CompletionMessage = "KerbalX Industries has placed a Kerbal into orbit around Kerbin.",
                MarkerBody = "Kerbin",
                MarkerAltitude = 90000d,
                MarkerLabel = "KerbalX crewed orbit",
                CraftFileName = "OA_KerbalX_Orbiter.craft"
            });

            return race;
        }

        private static void ApplyWatcherSettings(ConfigNode configNode, ContractWatcherSettings settings)
        {
            if (configNode == null || settings == null || !configNode.HasNode(SettingsNodeName))
            {
                return;
            }

            ConfigNode node = configNode.GetNode(SettingsNodeName);
            if (node == null)
            {
                return;
            }

            settings.CheckIntervalSeconds = ReadDouble(node, "checkIntervalSeconds", settings.CheckIntervalSeconds, 0.2d, 300d);

            double nearExpiryThresholdKerbinDays = ReadDouble(
                node,
                "nearExpiryThresholdKerbinDays",
                settings.NearExpiryThresholdSeconds / OtherAgenciesTime.KerbinDaySeconds,
                0d,
                1000d);
            settings.NearExpiryThresholdSeconds = nearExpiryThresholdKerbinDays * OtherAgenciesTime.KerbinDaySeconds;

            double offerAgeThresholdKerbinDays = ReadDouble(
                node,
                "offerAgeThresholdKerbinDays",
                settings.OfferAgeThresholdSeconds / OtherAgenciesTime.KerbinDaySeconds,
                0d,
                1000d);
            settings.OfferAgeThresholdSeconds = offerAgeThresholdKerbinDays * OtherAgenciesTime.KerbinDaySeconds;

            settings.MinTakeoverChance = ReadFloat(node, "minTakeoverChance", settings.MinTakeoverChance, 0f, 1f);
            settings.MaxTakeoverChance = ReadFloat(node, "maxTakeoverChance", settings.MaxTakeoverChance, 0f, 1f);
            if (settings.MaxTakeoverChance < settings.MinTakeoverChance)
            {
                float swap = settings.MinTakeoverChance;
                settings.MinTakeoverChance = settings.MaxTakeoverChance;
                settings.MaxTakeoverChance = swap;
            }

            double lateGameStartKerbinYears = ReadDouble(
                node,
                "lateGameStartKerbinYears",
                settings.LateGameStartSeconds / OtherAgenciesTime.KerbinYearSeconds,
                0d,
                1000d);
            settings.LateGameStartSeconds = lateGameStartKerbinYears * OtherAgenciesTime.KerbinYearSeconds;

            double endGameStartKerbinYears = ReadDouble(
                node,
                "endGameStartKerbinYears",
                settings.EndGameStartSeconds / OtherAgenciesTime.KerbinYearSeconds,
                0d,
                1000d);
            settings.EndGameStartSeconds = endGameStartKerbinYears * OtherAgenciesTime.KerbinYearSeconds;
        }

        private static void ApplySpaceRaceSettings(ConfigNode configNode, SpaceRaceSettings settings)
        {
            if (configNode == null || settings == null || !configNode.HasNode(SpaceRaceSettingsNodeName))
            {
                return;
            }

            ConfigNode node = configNode.GetNode(SpaceRaceSettingsNodeName);
            if (node == null)
            {
                return;
            }

            settings.SimulationCheckIntervalSeconds = ReadDouble(
                node,
                "simulationCheckIntervalSeconds",
                settings.SimulationCheckIntervalSeconds,
                1d,
                3600d);
            settings.PlayerProgressCheckIntervalSeconds = ReadDouble(
                node,
                "playerProgressCheckIntervalSeconds",
                settings.PlayerProgressCheckIntervalSeconds,
                0.2d,
                60d);
            settings.MaxCatchUpTicksPerUpdate = ReadInt(node, "maxCatchUpTicksPerUpdate", settings.MaxCatchUpTicksPerUpdate, 1, 100);
            settings.AnnounceStageChanges = ReadBool(node, "announceStageChanges", settings.AnnounceStageChanges);
            settings.AnnounceResearchUnlocks = ReadBool(node, "announceResearchUnlocks", settings.AnnounceResearchUnlocks);
            settings.AnnounceContractRewards = ReadBool(node, "announceContractRewards", settings.AnnounceContractRewards);
        }

        private static void ApplyPreferenceOverrides(ConfigNode configNode, IDictionary<string, string[]> preferences)
        {
            if (configNode == null || preferences == null)
            {
                return;
            }

            ConfigNode[] nodes = configNode.GetNodes(PreferenceNodeName);
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
                string[] keywords = SplitList(node.GetValue("keywords"));
                if (string.IsNullOrEmpty(id) || keywords.Length == 0)
                {
                    continue;
                }

                preferences[id] = keywords;
            }
        }

        private static void ApplyAgencyNodes(ConfigNode configNode, OtherAgenciesConfig config)
        {
            ConfigNode[] nodes = configNode.GetNodes(AgencyNodeName);
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            config.Agencies.Clear();
            foreach (ConfigNode node in nodes)
            {
                AgencyConfigDefinition agency = CreateAgencyFromNode(node);
                if (agency != null)
                {
                    config.Agencies.Add(agency);
                }
            }
        }

        private static AgencyConfigDefinition CreateAgencyFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return null;
            }

            string name = (node.GetValue("name") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            List<string> preferenceIds = SplitList(node.GetValue("preferences")).ToList();
            string singlePreference = (node.GetValue("preference") ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(singlePreference) && !preferenceIds.Contains(singlePreference, StringComparer.OrdinalIgnoreCase))
            {
                preferenceIds.Add(singlePreference);
            }

            if (preferenceIds.Count == 0)
            {
                return null;
            }

            return new AgencyConfigDefinition
            {
                Name = name,
                PreferenceIds = preferenceIds,
                Aggression = ReadFloat(node, "aggression", 0.35f, 0f, 1f),
                CompletionFlavor = node.GetValue("completionFlavor") ?? string.Empty
            };
        }

        private static void ApplySpaceRaceNodes(ConfigNode configNode, OtherAgenciesConfig config)
        {
            ConfigNode[] nodes = configNode.GetNodes(SpaceRaceNodeName);
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            config.SpaceRaces.Clear();
            foreach (ConfigNode node in nodes)
            {
                SpaceRaceDefinition race = CreateSpaceRaceFromNode(node);
                if (race != null)
                {
                    config.SpaceRaces.Add(race);
                }
            }
        }

        private static SpaceRaceDefinition CreateSpaceRaceFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return null;
            }

            string id = (node.GetValue("id") ?? string.Empty).Trim();
            string rivalAgencyName = (node.GetValue("rivalAgency") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(rivalAgencyName))
            {
                return null;
            }

            SpaceRaceDefinition race = new SpaceRaceDefinition
            {
                Id = id,
                Name = ReadString(node, "name", id),
                Enabled = ReadBool(node, "enabled", true),
                RivalAgencyName = rivalAgencyName,
                PlayerGoal = ReadString(node, "playerGoal", "crewed_orbit"),
                TargetBodyName = ReadString(node, "targetBody", "Kerbin"),
                OfferAfterUniversalTime = ReadDouble(node, "offerAfterUniversalTime", 0d, 0d, double.MaxValue),
                OfferAfterCompletedContracts = ReadInt(node, "offerAfterCompletedContracts", 0, 0, int.MaxValue),
                RequireFirstLaunchReached = ReadBool(node, "requireFirstLaunchReached", true),
                ContractTitle = ReadString(node, "contractTitle", $"World First Challenge: {id}"),
                ContractSynopsis = ReadString(node, "contractSynopsis", "A rival agency is challenging your program."),
                ContractDescription = ReadString(node, "contractDescription", "A rival agency is racing your program toward a milestone."),
                ContractNotes = ReadString(node, "contractNotes", string.Empty),
                OfferedMessage = ReadString(node, "offeredMessage", string.Empty),
                AcceptedMessage = ReadString(node, "acceptedMessage", string.Empty),
                CompletedMessage = ReadString(node, "completedMessage", string.Empty),
                FailedMessage = ReadString(node, "failedMessage", string.Empty),
                SupportFunds = ReadDouble(node, "supportFunds", 0d, 0d, double.MaxValue),
                CompletionFunds = ReadDouble(node, "completionFunds", 0d, 0d, double.MaxValue),
                CompletionScience = ReadFloat(node, "completionScience", 0f, 0f, float.MaxValue),
                CompletionReputation = ReadFloat(node, "completionReputation", 0f, 0f, float.MaxValue),
                FailureReputation = ReadFloat(node, "failureReputation", 0f, 0f, float.MaxValue),
                FailureSciencePenalty = ReadFloat(node, "failureSciencePenalty", 0f, 0f, float.MaxValue),
                ContractScienceBaseMin = ReadDouble(node, "contractScienceBaseMin", 0.2d, 0d, double.MaxValue),
                ContractScienceBaseMax = ReadDouble(node, "contractScienceBaseMax", 1d, 0d, double.MaxValue),
                ContractFundsBaseMin = ReadDouble(node, "contractFundsBaseMin", 300d, 0d, double.MaxValue),
                ContractFundsBaseMax = ReadDouble(node, "contractFundsBaseMax", 1500d, 0d, double.MaxValue),
                ContractProgressBase = ReadDouble(node, "contractProgressBase", 1d, 0d, double.MaxValue),
                ContractCompletionScienceMultiplier = ReadFloat(node, "contractCompletionScienceMultiplier", 1f, 0f, float.MaxValue),
                ContractCompletionFundsMultiplier = ReadFloat(node, "contractCompletionFundsMultiplier", 0.1f, 0f, float.MaxValue)
            };

            if (race.ContractScienceBaseMax < race.ContractScienceBaseMin)
            {
                double swap = race.ContractScienceBaseMin;
                race.ContractScienceBaseMin = race.ContractScienceBaseMax;
                race.ContractScienceBaseMax = swap;
            }

            if (race.ContractFundsBaseMax < race.ContractFundsBaseMin)
            {
                double swap = race.ContractFundsBaseMin;
                race.ContractFundsBaseMin = race.ContractFundsBaseMax;
                race.ContractFundsBaseMax = swap;
            }

            foreach (ConfigNode researchNode in node.GetNodes(SpaceRaceResearchNodeName))
            {
                SpaceRaceResearchStepDefinition researchStep = CreateResearchStepFromNode(researchNode);
                if (researchStep != null)
                {
                    race.ResearchSteps.Add(researchStep);
                }
            }

            foreach (ConfigNode stageNode in node.GetNodes(SpaceRaceStageNodeName))
            {
                SpaceRaceStageDefinition stage = CreateStageFromNode(stageNode);
                if (stage != null)
                {
                    race.Stages.Add(stage);
                }
            }

            if (race.ResearchSteps.Count == 0 || race.Stages.Count == 0)
            {
                return null;
            }

            return race;
        }

        private static SpaceRaceResearchStepDefinition CreateResearchStepFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return null;
            }

            string id = (node.GetValue("id") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return new SpaceRaceResearchStepDefinition
            {
                Id = id,
                DisplayName = ReadString(node, "displayName", id),
                ScienceCost = ReadDouble(node, "scienceCost", 1d, 0d, double.MaxValue),
                UnlockMessage = ReadString(node, "unlockMessage", string.Empty)
            };
        }

        private static SpaceRaceStageDefinition CreateStageFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return null;
            }

            string id = (node.GetValue("id") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return new SpaceRaceStageDefinition
            {
                Id = id,
                Title = ReadString(node, "title", id),
                Description = ReadString(node, "description", string.Empty),
                RequiredResearchIds = SplitList(node.GetValue("requiredResearch")).ToList(),
                FundsCost = ReadDouble(node, "fundsCost", 0d, 0d, double.MaxValue),
                RequiredStolenContracts = ReadInt(node, "requiredStolenContracts", 0, 0, int.MaxValue),
                RequiredContractProgress = ReadDouble(node, "requiredContractProgress", 0d, 0d, double.MaxValue),
                CompletionChance = ReadFloat(node, "completionChance", 0.5f, 0f, 1f),
                PassiveScienceChance = ReadFloat(node, "passiveScienceChance", 0.5f, 0f, 1f),
                PassiveScienceMin = ReadDouble(node, "passiveScienceMin", 0.25d, 0d, double.MaxValue),
                PassiveScienceMax = ReadDouble(node, "passiveScienceMax", 1.25d, 0d, double.MaxValue),
                PassiveFundsChance = ReadFloat(node, "passiveFundsChance", 0.5f, 0f, 1f),
                PassiveFundsMin = ReadDouble(node, "passiveFundsMin", 500d, 0d, double.MaxValue),
                PassiveFundsMax = ReadDouble(node, "passiveFundsMax", 2500d, 0d, double.MaxValue),
                ContractScienceMultiplier = ReadFloat(node, "contractScienceMultiplier", 1f, 0f, float.MaxValue),
                ContractFundsMultiplier = ReadFloat(node, "contractFundsMultiplier", 1f, 0f, float.MaxValue),
                ContractProgressMultiplier = ReadFloat(node, "contractProgressMultiplier", 1f, 0f, float.MaxValue),
                CompletionMessage = ReadString(node, "completionMessage", string.Empty),
                MarkerBody = ReadString(node, "markerBody", string.Empty),
                MarkerAltitude = ReadDouble(node, "markerAltitude", 0d, 0d, double.MaxValue),
                MarkerLabel = ReadString(node, "markerLabel", string.Empty),
                CraftFileName = ReadString(node, "craftFileName", string.Empty)
            };
        }

        private static string ReadString(ConfigNode node, string key, string defaultValue)
        {
            string value = node?.GetValue(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static bool ReadBool(ConfigNode node, string key, bool defaultValue)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            string raw = node.GetValue(key);
            return bool.TryParse(raw, out bool value) ? value : defaultValue;
        }

        private static int ReadInt(ConfigNode node, string key, int defaultValue, int min, int max)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            string raw = node.GetValue(key);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return defaultValue;
            }

            return Math.Max(min, Math.Min(max, value));
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

            return UnityEngine.Mathf.Clamp(value, min, max);
        }

        private static string[] SplitList(string raw)
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
    }
}
