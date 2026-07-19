using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Contracts.Abstractions;

/// <summary>
/// Detects TIA Portal version and calculates available capabilities.
/// </summary>
public interface ICapabilityProvider
{
    Task<CapabilityDto> GetCapabilitiesAsync(CancellationToken cancellationToken);
    Task<bool> IsSupportedAsync(string capability, CancellationToken cancellationToken);
}
