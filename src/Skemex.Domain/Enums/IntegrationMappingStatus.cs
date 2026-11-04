namespace Skemex.Domain.Enums;

/// <summary>Lifecycle of a local↔external entity mapping.</summary>
public enum IntegrationMappingStatus
{
    /// <summary>Mapping is current and used for sync.</summary>
    Active = 1,

    /// <summary>External entity changed or was removed; needs review/re-link.</summary>
    Outdated = 2,
}
