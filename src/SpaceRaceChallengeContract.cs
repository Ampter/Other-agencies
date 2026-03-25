using System;
using System.Linq;
using Contracts;

namespace OtherAgencies
{
    public sealed class SpaceRaceGoalParameter : ContractParameter
    {
        private string raceId = string.Empty;

        public SpaceRaceGoalParameter()
        {
        }

        public SpaceRaceGoalParameter(string raceId)
        {
            this.raceId = raceId ?? string.Empty;
        }

        protected override string GetTitle()
        {
            if (!TryGetDefinition(out SpaceRaceDefinition definition))
            {
                return "Beat the rival agency to its target milestone.";
            }

            return $"Put a Kerbal in orbit around {definition.TargetBodyName} before {definition.RivalAgencyName}.";
        }

        protected override string GetNotes()
        {
            RivalSpaceRaceSnapshot snapshot = RivalSpaceRaceScenario.Instance?.GetSnapshot(raceId);
            if (snapshot == null || string.IsNullOrEmpty(snapshot.RaceId))
            {
                return string.Empty;
            }

            string nextResearch = string.IsNullOrEmpty(snapshot.NextResearchDisplayName)
                ? "complete"
                : snapshot.NextResearchDisplayName;
            return $"Rival stage: {snapshot.CurrentStageTitle} | Funds: {snapshot.FundsBalance:0} | Science: {snapshot.ScienceBalance:0.0} | Contracts: {snapshot.StolenContracts} | Next tech: {nextResearch}";
        }

        protected override string GetMessageComplete()
        {
            return TryGetDefinition(out SpaceRaceDefinition definition)
                ? $"{definition.RivalAgencyName} failed to beat your crew to orbit."
                : "You won the space race.";
        }

        protected override string GetMessageFailed()
        {
            return TryGetDefinition(out SpaceRaceDefinition definition)
                ? $"{definition.RivalAgencyName} reached orbit first."
                : "The rival reached the milestone first.";
        }

        protected override string GetMessageIncomplete()
        {
            return "The rival program is still progressing.";
        }

        protected override void OnUpdate()
        {
            RivalSpaceRaceScenario scenario = RivalSpaceRaceScenario.Instance;
            if (scenario == null)
            {
                return;
            }

            RivalSpaceRaceStatus status = scenario.GetRaceStatus(raceId);
            if (status == RivalSpaceRaceStatus.Won)
            {
                SetComplete();
            }
            else if (status == RivalSpaceRaceStatus.Lost)
            {
                SetFailed();
            }
            else
            {
                SetIncomplete();
            }
        }

        protected override void OnSave(ConfigNode node)
        {
            node.AddValue("raceId", raceId);
        }

        protected override void OnLoad(ConfigNode node)
        {
            raceId = node.GetValue("raceId") ?? string.Empty;
        }

        private bool TryGetDefinition(out SpaceRaceDefinition definition)
        {
            definition = null;
            return RivalSpaceRaceScenario.Instance != null
                && RivalSpaceRaceScenario.Instance.TryGetRaceDefinition(raceId, out definition)
                && definition != null;
        }
    }

    public sealed class SpaceRaceChallengeContract : Contract
    {
        private string raceId = string.Empty;

        protected override bool Generate()
        {
            RivalSpaceRaceScenario scenario = RivalSpaceRaceScenario.Instance;
            if (scenario == null || HasExistingChallengeContract())
            {
                return false;
            }

            SpaceRaceDefinition definition = scenario.GetNextOfferableRace();
            if (definition == null)
            {
                return false;
            }

            raceId = definition.Id;
            FundsAdvance = definition.SupportFunds;
            FundsCompletion = definition.CompletionFunds;
            ScienceCompletion = definition.CompletionScience;
            ReputationCompletion = definition.CompletionReputation;
            ReputationFailure = definition.FailureReputation;

            AddParameter(new SpaceRaceGoalParameter(raceId), "goal");
            scenario.MarkRaceOffered(raceId);
            return true;
        }

        public override bool MeetRequirements()
        {
            RivalSpaceRaceScenario scenario = RivalSpaceRaceScenario.Instance;
            return scenario != null
                && !HasExistingChallengeContract()
                && scenario.GetNextOfferableRace() != null;
        }

        public override bool CanBeCancelled()
        {
            return false;
        }

        protected override string GetHashString()
        {
            return $"OtherAgencies.SpaceRace.{raceId}";
        }

        protected override string GetTitle()
        {
            return GetDefinition()?.ContractTitle ?? "World First Challenge";
        }

        protected override string GetDescription()
        {
            return GetDefinition()?.ContractDescription
                ?? "A rival agency is challenging your program to reach a milestone first.";
        }

        protected override string GetSynopsys()
        {
            return GetDefinition()?.ContractSynopsis
                ?? "A rival agency has started a public milestone challenge.";
        }

        protected override string GetNotes()
        {
            return GetDefinition()?.ContractNotes ?? string.Empty;
        }

        protected override string MessageOffered()
        {
            return GetDefinition()?.OfferedMessage ?? base.MessageOffered();
        }

        protected override string MessageAccepted()
        {
            return GetDefinition()?.AcceptedMessage ?? base.MessageAccepted();
        }

        protected override string MessageCompleted()
        {
            return GetDefinition()?.CompletedMessage ?? base.MessageCompleted();
        }

        protected override string MessageFailed()
        {
            return GetDefinition()?.FailedMessage ?? base.MessageFailed();
        }

        protected override void OnAccepted()
        {
            RivalSpaceRaceScenario.Instance?.AcceptRace(raceId);
        }

        protected override void OnCompleted()
        {
            RivalSpaceRaceScenario.Instance?.MarkPlayerWon(raceId);
        }

        protected override void OnDeclined()
        {
            RivalSpaceRaceScenario.Instance?.DeclineRace(raceId);
        }

        protected override void OnOfferExpired()
        {
            RivalSpaceRaceScenario.Instance?.DeclineRace(raceId);
        }

        protected override void OnFailed()
        {
            RivalSpaceRaceScenario.Instance?.MarkRivalWon(raceId);

            SpaceRaceDefinition definition = GetDefinition();
            if (definition == null || definition.FailureSciencePenalty <= 0f || ResearchAndDevelopment.Instance == null)
            {
                return;
            }

            float currentScience = ResearchAndDevelopment.Instance.Science;
            float appliedPenalty = Math.Min(currentScience, definition.FailureSciencePenalty);
            if (appliedPenalty > 0f)
            {
                ResearchAndDevelopment.Instance.AddScience(-appliedPenalty, TransactionReasons.ContractPenalty);
            }
        }

        protected override void OnUpdate()
        {
            RivalSpaceRaceScenario scenario = RivalSpaceRaceScenario.Instance;
            if (scenario == null)
            {
                return;
            }

            RivalSpaceRaceStatus status = scenario.GetRaceStatus(raceId);
            if (ContractState == State.Active && status == RivalSpaceRaceStatus.Won)
            {
                Complete();
            }
            else if (ContractState == State.Active && status == RivalSpaceRaceStatus.Lost)
            {
                Fail();
            }
        }

        protected override void OnSave(ConfigNode node)
        {
            node.AddValue("raceId", raceId);
        }

        protected override void OnLoad(ConfigNode node)
        {
            raceId = node.GetValue("raceId") ?? string.Empty;
            RivalSpaceRaceScenario scenario = RivalSpaceRaceScenario.Instance;
            if (scenario == null || string.IsNullOrEmpty(raceId))
            {
                return;
            }

            if (ContractState == State.Offered)
            {
                scenario.MarkRaceOffered(raceId);
            }
            else if (ContractState == State.Active)
            {
                scenario.AcceptRace(raceId);
            }
        }

        private SpaceRaceDefinition GetDefinition()
        {
            return RivalSpaceRaceScenario.Instance != null
                && RivalSpaceRaceScenario.Instance.TryGetRaceDefinition(raceId, out SpaceRaceDefinition definition)
                ? definition
                : null;
        }

        private static bool HasExistingChallengeContract()
        {
            if (ContractSystem.Instance == null || ContractSystem.Instance.Contracts == null)
            {
                return false;
            }

            return ContractSystem.Instance.Contracts.Any(contract =>
                contract != null
                && contract is SpaceRaceChallengeContract
                && contract.ContractState != State.Completed
                && contract.ContractState != State.Cancelled
                && contract.ContractState != State.Declined
                && contract.ContractState != State.Withdrawn
                && contract.ContractState != State.Failed
                && contract.ContractState != State.DeadlineExpired);
        }
    }
}
