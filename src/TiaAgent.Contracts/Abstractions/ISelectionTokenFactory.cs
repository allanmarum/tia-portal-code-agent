namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Creates selection tokens for testing and simulation.
/// Implemented by SimulatorTiaProjectService; real TIA implementations capture from the UI.
/// </summary>
public interface ISelectionTokenFactory
{
    void CreateSelectionToken(string token, string objectId, string objectName, string objectType, string path);
}
