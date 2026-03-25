using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ContractConfigurator;
using Contracts;
using UnityEngine;

namespace OtherAgencies
{
    internal enum RivalSpaceRaceStatus
    {
        Pending,
        Offered,
        Declined,
        Active,
        Won,
        Lost
    }

    internal sealed class RivalSpaceRaceSnapshot
    {
        public string RaceId { get; set; } = string.Empty;
        public string RaceName { get; set; } = string.Empty;
        public string RivalAgencyName { get; set; } = string.Empty;
        public RivalSpaceRaceStatus Status { get; set; }
        public string CurrentStageTitle { get; set; } = string.Empty;
        public int CurrentStageNumber { get; set; }
        public int TotalStages { get; set; }
        public double FundsBalance { get; set; }
        public double ScienceBalance { get; set; }
        public int StolenContracts { get; set; }
        public double ContractProgress { get; set; }
        public int UnlockedResearchCount { get; set; }
        public int TotalResearchSteps { get; set; }
        public string NextResearchDisplayName { get; set; } = string.Empty;
        public string TargetBodyName { get; set; } = string.Empty;
    }

    internal sealed class RivalSpaceRacePersistentState
    {
        public string RaceId { get; set; } = string.Empty;
        public RivalSpaceRaceStatus Status { get; set; } = RivalSpaceRaceStatus.Pending;
        public int CurrentStageIndex { get; set; }
        public double ScienceBalance { get; set; }
        public double FundsBalance { get; set; }
        public int StolenContracts { get; set; }
        public double ContractProgress { get; set; }
        public List<string> UnlockedResearchIds { get; set; } = new List<string>();
        public double LastSimulationTime { get; set; }
        public double NextSimulationTime { get; set; }
        public double NextPlayerCheckTime { get; set; }
        public double StartedAtUniversalTime { get; set; }
        public double FinishedAtUniversalTime { get; set; }
    }

    [KSPScenario(
        ScenarioCreationOptions.AddToNewCareerGames | ScenarioCreationOptions.AddToExistingCareerGames,
        new[] { GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION })]
    public sealed class RivalSpaceRaceScenario : ScenarioModule
    {
        private const string LogPrefix = "[OtherAgencies]";
        private const string StateNodeName = "SPACE_RACE_STATE";

        private readonly Dictionary<string, RivalSpaceRacePersistentState> raceStates =
            new Dictionary<string, RivalSpaceRacePersistentState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SpaceRaceDefinition> raceDefinitions =
            new Dictionary<string, SpaceRaceDefinition>(StringComparer.OrdinalIgnoreCase);

        private OtherAgenciesConfig config;

        public static RivalSpaceRaceScenario Instance { get; private set; }

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            ReloadConfig();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ReloadConfig();
            LoadSavedStates(node);
            EnsureRaceStateEntries();
            UpdatePendingRaceOutcomes();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (node == null)
            {
                return;
            }

            foreach (RivalSpaceRacePersistentState state in raceStates.Values)
            {
                ConfigNode stateNode = new ConfigNode(StateNodeName);
                stateNode.AddValue("id", state.RaceId);
                stateNode.AddValue("status", state.Status.ToString());
                stateNode.AddValue("currentStageIndex", state.CurrentStageIndex.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("scienceBalance", state.ScienceBalance.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("fundsBalance", state.FundsBalance.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("stolenContracts", state.StolenContracts.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("contractProgress", state.ContractProgress.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("unlockedResearch", string.Join(",", state.UnlockedResearchIds));
                stateNode.AddValue("lastSimulationTime", state.LastSimulationTime.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("nextSimulationTime", state.NextSimulationTime.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("nextPlayerCheckTime", state.NextPlayerCheckTime.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("startedAtUniversalTime", state.StartedAtUniversalTime.ToString(CultureInfo.InvariantCulture));
                stateNode.AddValue("finishedAtUniversalTime", state.FinishedAtUniversalTime.ToString(CultureInfo.InvariantCulture));
                node.AddNode(stateNode);
            }
        }

        private void Update()
        {
            if (config == null)
            {
                ReloadConfig();
            }

            if (config == null)
            {
                return;
            }

            SyncContractConfiguratorContracts();

            double now = Planetarium.GetUniversalTime();
            foreach (KeyValuePair<string, RivalSpaceRacePersistentState> pair in raceStates)
            {
                if (!raceDefinitions.TryGetValue(pair.Key, out SpaceRaceDefinition definition) || definition == null)
                {
                    continue;
                }

                RivalSpaceRacePersistentState state = pair.Value;
                if (state == null)
                {
                    continue;
                }

                if (state.Status == RivalSpaceRaceStatus.Active)
                {
                    SimulateActiveRace(definition, state, now);
                    CheckPlayerGoal(definition, state, now);
                }
                else if (state.Status == RivalSpaceRaceStatus.Pending)
                {
                    if (HasPlayerCompletedGoal(definition))
                    {
                        state.Status = RivalSpaceRaceStatus.Won;
                        state.FinishedAtUniversalTime = now;
                    }
                }
            }
        }

        internal SpaceRaceDefinition GetNextOfferableRace()
        {
            if (config == null)
            {
                ReloadConfig();
            }

            if (config == null)
            {
                return null;
            }

            double now = Planetarium.GetUniversalTime();
            foreach (SpaceRaceDefinition definition in config.SpaceRaces)
            {
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                RivalSpaceRacePersistentState state = GetOrCreateState(definition.Id);
                if (state.Status == RivalSpaceRaceStatus.Declined)
                {
                    state.Status = RivalSpaceRaceStatus.Pending;
                }

                if (state.Status != RivalSpaceRaceStatus.Pending)
                {
                    continue;
                }

                if (definition.RequireFirstLaunchReached && !IsFirstLaunchReached())
                {
                    continue;
                }

                if (definition.OfferAfterUniversalTime > now)
                {
                    continue;
                }

                if (GetCompletedContractCount() < definition.OfferAfterCompletedContracts)
                {
                    continue;
                }

                if (HasPlayerCompletedGoal(definition))
                {
                    state.Status = RivalSpaceRaceStatus.Won;
                    state.FinishedAtUniversalTime = now;
                    continue;
                }

                return definition;
            }

            return null;
        }

        internal bool TryGetRaceDefinition(string raceId, out SpaceRaceDefinition definition)
        {
            if (config == null)
            {
                ReloadConfig();
            }

            return raceDefinitions.TryGetValue(raceId ?? string.Empty, out definition);
        }

        public void MarkRaceOffered(string raceId)
        {
            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            if (state.Status == RivalSpaceRaceStatus.Pending)
            {
                state.Status = RivalSpaceRaceStatus.Offered;
                if (TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) && definition != null)
                {
                    LogRaceEvent(definition, state, "Challenge contract offered in Mission Control.");
                }
            }
        }

        public void DeclineRace(string raceId)
        {
            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            if (state.Status != RivalSpaceRaceStatus.Offered)
            {
                return;
            }

            // "Decline = nothing" means the race should remain eligible for a later offer.
            state.Status = RivalSpaceRaceStatus.Pending;
            state.FinishedAtUniversalTime = 0d;
            if (TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) && definition != null)
            {
                LogRaceEvent(definition, state, "Challenge contract declined or expired; race returned to pending.");
            }
        }

        public void AcceptRace(string raceId)
        {
            if (!TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) || definition == null)
            {
                return;
            }

            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            if (state.Status == RivalSpaceRaceStatus.Active
                || state.Status == RivalSpaceRaceStatus.Won
                || state.Status == RivalSpaceRaceStatus.Lost)
            {
                return;
            }

            double now = Planetarium.GetUniversalTime();
            state.Status = RivalSpaceRaceStatus.Active;
            state.CurrentStageIndex = 0;
            state.ScienceBalance = 0d;
            state.FundsBalance = 0d;
            state.StolenContracts = 0;
            state.ContractProgress = 0d;
            state.UnlockedResearchIds.Clear();
            state.StartedAtUniversalTime = now;
            state.FinishedAtUniversalTime = 0d;
            state.LastSimulationTime = now;
            state.NextSimulationTime = now + config.RaceSettings.SimulationCheckIntervalSeconds;
            state.NextPlayerCheckTime = now + config.RaceSettings.PlayerProgressCheckIntervalSeconds;

            PostRaceMessage(definition.AcceptedMessage);
            LogRaceEvent(definition, state, "Challenge accepted. Rival simulation started.");
        }

        public void MarkPlayerWon(string raceId)
        {
            if (!TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) || definition == null)
            {
                return;
            }

            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            if (state.Status != RivalSpaceRaceStatus.Active)
            {
                return;
            }

            state.Status = RivalSpaceRaceStatus.Won;
            state.FinishedAtUniversalTime = Planetarium.GetUniversalTime();
            PostRaceMessage(definition.CompletedMessage);
            TryCompleteRaceContract(definition);
            LogRaceEvent(definition, state, "Player reached the goal first.");
        }

        public void MarkRivalWon(string raceId)
        {
            if (!TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) || definition == null)
            {
                return;
            }

            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            if (state.Status != RivalSpaceRaceStatus.Active)
            {
                return;
            }

            state.Status = RivalSpaceRaceStatus.Lost;
            state.FinishedAtUniversalTime = Planetarium.GetUniversalTime();
            ApplyFailureSciencePenalty(definition);
            PostRaceMessage(definition.FailedMessage);
            TryFailRaceContract(definition);
            LogRaceEvent(definition, state, "Rival reached the goal first.");
        }

        internal RivalSpaceRaceStatus GetRaceStatus(string raceId)
        {
            return GetOrCreateState(raceId).Status;
        }

        internal RivalSpaceRaceSnapshot GetSnapshot(string raceId)
        {
            RivalSpaceRaceSnapshot snapshot = new RivalSpaceRaceSnapshot();
            if (!TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition) || definition == null)
            {
                return snapshot;
            }

            RivalSpaceRacePersistentState state = GetOrCreateState(raceId);
            SpaceRaceStageDefinition stage = GetCurrentStage(definition, state);
            SpaceRaceResearchStepDefinition nextResearch = GetNextResearchStep(definition, state);

            snapshot.RaceId = definition.Id;
            snapshot.RaceName = definition.Name;
            snapshot.RivalAgencyName = definition.RivalAgencyName;
            snapshot.Status = state.Status;
            snapshot.CurrentStageTitle = stage?.Title ?? "Complete";
            snapshot.CurrentStageNumber = Math.Min(state.CurrentStageIndex + 1, definition.Stages.Count);
            snapshot.TotalStages = definition.Stages.Count;
            snapshot.FundsBalance = state.FundsBalance;
            snapshot.ScienceBalance = state.ScienceBalance;
            snapshot.StolenContracts = state.StolenContracts;
            snapshot.ContractProgress = state.ContractProgress;
            snapshot.UnlockedResearchCount = state.UnlockedResearchIds.Count;
            snapshot.TotalResearchSteps = definition.ResearchSteps.Count;
            snapshot.NextResearchDisplayName = nextResearch?.DisplayName ?? string.Empty;
            snapshot.TargetBodyName = definition.TargetBodyName;
            return snapshot;
        }

        public void NotifyContractStolen(string agencyName, Contract contract)
        {
            if (config == null || contract == null)
            {
                return;
            }

            foreach (SpaceRaceDefinition definition in config.SpaceRaces)
            {
                if (definition == null
                    || !string.Equals(definition.RivalAgencyName, agencyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RivalSpaceRacePersistentState state = GetOrCreateState(definition.Id);
                if (state.Status != RivalSpaceRaceStatus.Active)
                {
                    continue;
                }

                SpaceRaceStageDefinition currentStage = GetCurrentStage(definition, state);
                if (currentStage == null)
                {
                    continue;
                }

                double scienceReward = (RandomRange(definition.ContractScienceBaseMin, definition.ContractScienceBaseMax)
                    + Math.Max(0f, contract.ScienceCompletion) * definition.ContractCompletionScienceMultiplier)
                    * currentStage.ContractScienceMultiplier;
                double fundsReward = (RandomRange(definition.ContractFundsBaseMin, definition.ContractFundsBaseMax)
                    + Math.Max(0d, contract.FundsAdvance + contract.FundsCompletion) * definition.ContractCompletionFundsMultiplier)
                    * currentStage.ContractFundsMultiplier;
                double progressReward = definition.ContractProgressBase * currentStage.ContractProgressMultiplier;

                state.ScienceBalance += scienceReward;
                state.FundsBalance += fundsReward;
                state.StolenContracts += 1;
                state.ContractProgress += progressReward;
                LogRaceEvent(
                    definition,
                    state,
                    $"Stole contract '{contract.Title}' for +{scienceReward:0.0} science, +{fundsReward:0} funds, +{progressReward:0.0} progress.");

                if (config.RaceSettings.AnnounceContractRewards)
                {
                    string rewardMessage =
                        $"{definition.RivalAgencyName} converted '{contract.Title}' into +{scienceReward:0.0} science and +{fundsReward:0} funds.";
                    PostRaceMessage(rewardMessage);
                }

                TryUnlockResearch(definition, state);
            }
        }

        private void ReloadConfig()
        {
            config = OtherAgenciesConfigLoader.Load();
            raceDefinitions.Clear();
            foreach (SpaceRaceDefinition race in config.SpaceRaces)
            {
                if (race == null || string.IsNullOrEmpty(race.Id))
                {
                    continue;
                }

                raceDefinitions[race.Id] = race;
            }

            EnsureRaceStateEntries();
        }

        private void SyncContractConfiguratorContracts()
        {
            if (config == null || ContractSystem.Instance == null || ContractSystem.Instance.Contracts == null)
            {
                return;
            }

            foreach (SpaceRaceDefinition definition in config.SpaceRaces)
            {
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                RivalSpaceRacePersistentState state = GetOrCreateState(definition.Id);
                ConfiguredContract contract = FindRaceContract(definition);
                if (contract == null)
                {
                    if (state.Status == RivalSpaceRaceStatus.Offered)
                    {
                        DeclineRace(definition.Id);
                    }

                    continue;
                }

                if (contract.ContractState == Contract.State.Offered)
                {
                    MarkRaceOffered(definition.Id);
                }
                else if (contract.ContractState == Contract.State.Active)
                {
                    AcceptRace(definition.Id);
                }
                else if (contract.ContractState == Contract.State.Completed && state.Status == RivalSpaceRaceStatus.Active)
                {
                    state.Status = RivalSpaceRaceStatus.Won;
                    state.FinishedAtUniversalTime = Planetarium.GetUniversalTime();
                    PostRaceMessage(definition.CompletedMessage);
                    LogRaceEvent(definition, state, "Contract Configurator marked the race contract complete.");
                }
                else if (contract.ContractState == Contract.State.Failed && state.Status == RivalSpaceRaceStatus.Active)
                {
                    state.Status = RivalSpaceRaceStatus.Lost;
                    state.FinishedAtUniversalTime = Planetarium.GetUniversalTime();
                    ApplyFailureSciencePenalty(definition);
                    PostRaceMessage(definition.FailedMessage);
                    LogRaceEvent(definition, state, "Contract Configurator marked the race contract failed.");
                }
            }
        }

        private void EnsureRaceStateEntries()
        {
            foreach (string raceId in raceDefinitions.Keys)
            {
                GetOrCreateState(raceId);
            }

            List<string> missingRaceIds = raceStates.Keys
                .Where(raceId => !raceDefinitions.ContainsKey(raceId))
                .ToList();
            foreach (string raceId in missingRaceIds)
            {
                raceStates.Remove(raceId);
            }
        }

        private void LoadSavedStates(ConfigNode node)
        {
            raceStates.Clear();
            if (node == null)
            {
                return;
            }

            foreach (ConfigNode stateNode in node.GetNodes(StateNodeName))
            {
                if (stateNode == null)
                {
                    continue;
                }

                string raceId = (stateNode.GetValue("id") ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(raceId))
                {
                    continue;
                }

                RivalSpaceRacePersistentState state = new RivalSpaceRacePersistentState
                {
                    RaceId = raceId,
                    Status = ParseStatus(stateNode.GetValue("status")),
                    CurrentStageIndex = ReadInt(stateNode, "currentStageIndex", 0),
                    ScienceBalance = ReadDouble(stateNode, "scienceBalance", 0d),
                    FundsBalance = ReadDouble(stateNode, "fundsBalance", 0d),
                    StolenContracts = ReadInt(stateNode, "stolenContracts", 0),
                    ContractProgress = ReadDouble(stateNode, "contractProgress", 0d),
                    UnlockedResearchIds = SplitList(stateNode.GetValue("unlockedResearch")).ToList(),
                    LastSimulationTime = ReadDouble(stateNode, "lastSimulationTime", 0d),
                    NextSimulationTime = ReadDouble(stateNode, "nextSimulationTime", 0d),
                    NextPlayerCheckTime = ReadDouble(stateNode, "nextPlayerCheckTime", 0d),
                    StartedAtUniversalTime = ReadDouble(stateNode, "startedAtUniversalTime", 0d),
                    FinishedAtUniversalTime = ReadDouble(stateNode, "finishedAtUniversalTime", 0d)
                };

                raceStates[raceId] = state;
            }
        }

        private void UpdatePendingRaceOutcomes()
        {
            double now = Planetarium.GetUniversalTime();
            foreach (RivalSpaceRacePersistentState state in raceStates.Values)
            {
                if (state == null
                    || (state.Status != RivalSpaceRaceStatus.Pending && state.Status != RivalSpaceRaceStatus.Declined)
                    || !raceDefinitions.TryGetValue(state.RaceId, out SpaceRaceDefinition definition)
                    || definition == null)
                {
                    continue;
                }

                if (state.Status == RivalSpaceRaceStatus.Declined)
                {
                    state.Status = RivalSpaceRaceStatus.Pending;
                }

                if (HasPlayerCompletedGoal(definition))
                {
                    state.Status = RivalSpaceRaceStatus.Won;
                    state.FinishedAtUniversalTime = now;
                    LogRaceEvent(definition, state, "Player had already completed the goal before the race could start.");
                }
            }
        }

        private void SimulateActiveRace(SpaceRaceDefinition definition, RivalSpaceRacePersistentState state, double now)
        {
            int safetyCounter = 0;
            while (state.NextSimulationTime <= now && safetyCounter < config.RaceSettings.MaxCatchUpTicksPerUpdate)
            {
                safetyCounter++;
                state.LastSimulationTime = state.NextSimulationTime;
                state.NextSimulationTime += config.RaceSettings.SimulationCheckIntervalSeconds;

                SpaceRaceStageDefinition currentStage = GetCurrentStage(definition, state);
                if (currentStage == null)
                {
                    MarkRivalWon(definition.Id);
                    return;
                }

                if (Roll(currentStage.PassiveScienceChance))
                {
                    state.ScienceBalance += RandomRange(currentStage.PassiveScienceMin, currentStage.PassiveScienceMax);
                }

                if (Roll(currentStage.PassiveFundsChance))
                {
                    state.FundsBalance += RandomRange(currentStage.PassiveFundsMin, currentStage.PassiveFundsMax);
                }

                TryUnlockResearch(definition, state);
                TryAdvanceStage(definition, state);

                if (state.Status != RivalSpaceRaceStatus.Active)
                {
                    return;
                }
            }
        }

        private void CheckPlayerGoal(SpaceRaceDefinition definition, RivalSpaceRacePersistentState state, double now)
        {
            if (state.NextPlayerCheckTime > now)
            {
                return;
            }

            state.NextPlayerCheckTime = now + config.RaceSettings.PlayerProgressCheckIntervalSeconds;
            if (HasPlayerCompletedGoal(definition))
            {
                MarkPlayerWon(definition.Id);
            }
        }

        private void TryUnlockResearch(SpaceRaceDefinition definition, RivalSpaceRacePersistentState state)
        {
            while (true)
            {
                SpaceRaceResearchStepDefinition nextResearch = GetNextResearchStep(definition, state);
                if (nextResearch == null || state.ScienceBalance < nextResearch.ScienceCost)
                {
                    return;
                }

                state.ScienceBalance -= nextResearch.ScienceCost;
                state.UnlockedResearchIds.Add(nextResearch.Id);
                LogRaceEvent(
                    definition,
                    state,
                    $"Unlocked research '{nextResearch.DisplayName}' for {nextResearch.ScienceCost:0.0} science.");

                if (config.RaceSettings.AnnounceResearchUnlocks)
                {
                    string message = string.IsNullOrEmpty(nextResearch.UnlockMessage)
                        ? $"{definition.RivalAgencyName} has researched {nextResearch.DisplayName}."
                        : nextResearch.UnlockMessage;
                    PostRaceMessage(message);
                }
            }
        }

        private void TryAdvanceStage(SpaceRaceDefinition definition, RivalSpaceRacePersistentState state)
        {
            SpaceRaceStageDefinition currentStage = GetCurrentStage(definition, state);
            if (currentStage == null)
            {
                return;
            }

            if (!currentStage.RequiredResearchIds.All(id => state.UnlockedResearchIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
            {
                return;
            }

            if (state.StolenContracts < currentStage.RequiredStolenContracts)
            {
                return;
            }

            if (state.ContractProgress < currentStage.RequiredContractProgress)
            {
                return;
            }

            if (state.FundsBalance < currentStage.FundsCost)
            {
                return;
            }

            if (!Roll(currentStage.CompletionChance))
            {
                return;
            }

            state.FundsBalance = Math.Max(0d, state.FundsBalance - currentStage.FundsCost);
            state.CurrentStageIndex++;
            LogRaceEvent(
                definition,
                state,
                $"Completed stage '{currentStage.Title}' after spending {currentStage.FundsCost:0} funds.");

            if (config.RaceSettings.AnnounceStageChanges)
            {
                string message = string.IsNullOrEmpty(currentStage.CompletionMessage)
                    ? $"{definition.RivalAgencyName} reached stage: {currentStage.Title}."
                    : currentStage.CompletionMessage;
                PostRaceMessage(message);
            }

            if (state.CurrentStageIndex >= definition.Stages.Count)
            {
                MarkRivalWon(definition.Id);
            }
        }

        private bool HasPlayerCompletedGoal(SpaceRaceDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            string playerGoal = (definition.PlayerGoal ?? string.Empty).Trim();
            if (string.Equals(playerGoal, "crewed_orbit", StringComparison.OrdinalIgnoreCase))
            {
                return HasCrewedOrbit(definition.TargetBodyName);
            }

            return false;
        }

        private static bool HasCrewedOrbit(string bodyName)
        {
            if (FlightGlobals.Vessels == null || FlightGlobals.Vessels.Count == 0)
            {
                return false;
            }

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (vessel == null || vessel.GetCrewCount() <= 0)
                {
                    continue;
                }

                if (vessel.situation != Vessel.Situations.ORBITING)
                {
                    continue;
                }

                string vesselBodyName = vessel.mainBody != null ? vessel.mainBody.name : string.Empty;
                if (!string.Equals(vesselBodyName, bodyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsFirstLaunchReached()
        {
            return ProgressTracking.Instance != null
                && ProgressTracking.Instance.firstLaunch != null
                && ProgressTracking.Instance.firstLaunch.IsReached;
        }

        private static int GetCompletedContractCount()
        {
            if (ContractSystem.Instance == null || ContractSystem.Instance.ContractsFinished == null)
            {
                return 0;
            }

            return ContractSystem.Instance.ContractsFinished.Count(
                contract => contract != null && contract.ContractState == Contract.State.Completed);
        }

        private static void ApplyFailureSciencePenalty(SpaceRaceDefinition definition)
        {
            if (definition == null || definition.FailureSciencePenalty <= 0f || ResearchAndDevelopment.Instance == null)
            {
                return;
            }

            float currentScience = ResearchAndDevelopment.Instance.Science;
            float appliedPenalty = Math.Min(currentScience, definition.FailureSciencePenalty);
            if (appliedPenalty > 0f)
            {
                ResearchAndDevelopment.Instance.AddScience(-appliedPenalty, TransactionReasons.ContractPenalty);
                Debug.Log($"{LogPrefix} Applied space-race science penalty: -{appliedPenalty:0.0} science.");
            }
        }

        private ConfiguredContract FindRaceContract(SpaceRaceDefinition definition)
        {
            if (definition == null
                || string.IsNullOrEmpty(definition.ContractConfiguratorTypeName)
                || ContractSystem.Instance == null
                || ContractSystem.Instance.Contracts == null)
            {
                return null;
            }

            return ContractSystem.Instance.Contracts
                .OfType<ConfiguredContract>()
                .FirstOrDefault(contract =>
                    contract != null
                    && contract.contractType != null
                    && string.Equals(
                        contract.contractType.name,
                        definition.ContractConfiguratorTypeName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private void TryCompleteRaceContract(SpaceRaceDefinition definition)
        {
            ConfiguredContract contract = FindRaceContract(definition);
            if (contract != null && contract.ContractState == Contract.State.Active)
            {
                Debug.Log($"{LogPrefix} Completing Contract Configurator race contract '{definition.ContractConfiguratorTypeName}'.");
                contract.Complete();
            }
        }

        private void TryFailRaceContract(SpaceRaceDefinition definition)
        {
            ConfiguredContract contract = FindRaceContract(definition);
            if (contract != null && contract.ContractState == Contract.State.Active)
            {
                Debug.Log($"{LogPrefix} Failing Contract Configurator race contract '{definition.ContractConfiguratorTypeName}'.");
                contract.Fail();
            }
        }

        private void LogRaceEvent(
            SpaceRaceDefinition definition,
            RivalSpaceRacePersistentState state,
            string message)
        {
            if (definition == null || state == null)
            {
                return;
            }

            string currentStage = GetCurrentStage(definition, state)?.Title ?? "Complete";
            Debug.Log(
                $"{LogPrefix} [SpaceRace:{definition.Id}] {message} " +
                $"Status={state.Status}, Stage={currentStage}, Funds={state.FundsBalance:0}, " +
                $"Science={state.ScienceBalance:0.0}, Contracts={state.StolenContracts}, Progress={state.ContractProgress:0.0}");
        }

        private SpaceRaceStageDefinition GetCurrentStage(SpaceRaceDefinition definition, RivalSpaceRacePersistentState state)
        {
            if (definition == null || state == null)
            {
                return null;
            }

            if (state.CurrentStageIndex < 0 || state.CurrentStageIndex >= definition.Stages.Count)
            {
                return null;
            }

            return definition.Stages[state.CurrentStageIndex];
        }

        private static SpaceRaceResearchStepDefinition GetNextResearchStep(
            SpaceRaceDefinition definition,
            RivalSpaceRacePersistentState state)
        {
            if (definition == null || state == null)
            {
                return null;
            }

            foreach (SpaceRaceResearchStepDefinition research in definition.ResearchSteps)
            {
                if (research != null && !state.UnlockedResearchIds.Contains(research.Id, StringComparer.OrdinalIgnoreCase))
                {
                    return research;
                }
            }

            return null;
        }

        private RivalSpaceRacePersistentState GetOrCreateState(string raceId)
        {
            string key = raceId ?? string.Empty;
            if (!raceStates.TryGetValue(key, out RivalSpaceRacePersistentState state) || state == null)
            {
                state = new RivalSpaceRacePersistentState { RaceId = key };
                raceStates[key] = state;
            }

            return state;
        }

        private static RivalSpaceRaceStatus ParseStatus(string raw)
        {
            return Enum.TryParse(raw, true, out RivalSpaceRaceStatus status)
                ? status
                : RivalSpaceRaceStatus.Pending;
        }

        private static int ReadInt(ConfigNode node, string key, int defaultValue)
        {
            string raw = node.GetValue(key);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : defaultValue;
        }

        private static double ReadDouble(ConfigNode node, string key, double defaultValue)
        {
            string raw = node.GetValue(key);
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : defaultValue;
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

        private static void PostRaceMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ScreenMessages.PostScreenMessage(message, 6f, ScreenMessageStyle.UPPER_CENTER);
        }

        private static bool Roll(float chance)
        {
            return chance > 0f && UnityEngine.Random.value <= chance;
        }

        private static double RandomRange(double min, double max)
        {
            if (max <= min)
            {
                return min;
            }

            return min + ((max - min) * UnityEngine.Random.value);
        }
    }
}
