namespace Agent200.Host.Services;

/// <summary>
/// Represents the Tier 2 Agent responsible for Root Cause Analysis (RCA).
/// The Investigator is awakened by the Watchdog when an anomaly is detected.
/// </summary>
public interface IInvestigatorAgent
{
    /// <summary>
    /// Investigates a detected anomaly by correlating metrics and events.
    /// </summary>
    /// <param name="anomalyDescription">A description of the anomaly provided by the health evaluator.</param>
    /// <returns>A detailed markdown report containing findings and recommendations.</returns>
    Task<string> InvestigateAnomalyAsync(string anomalyDescription);
}
