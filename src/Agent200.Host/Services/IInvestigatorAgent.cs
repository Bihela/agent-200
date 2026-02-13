using Microsoft.Agents.AI;

namespace Agent200.Host.Services;

/// <summary>
/// Interface for the Tier 2 Investigator Agent.
/// Responsible for Root Cause Analysis (RCA).
/// </summary>
public interface IInvestigatorAgent
{
    /// <summary>
    /// Performs an investigation into a detected anomaly.
    /// </summary>
    Task<string> InvestigateAnomalyAsync(string anomalyDescription);

    /// <summary>
    /// Returns the underlying AIAgent instance for use in workflows.
    /// </summary>
    AIAgent AsAgent();
}
