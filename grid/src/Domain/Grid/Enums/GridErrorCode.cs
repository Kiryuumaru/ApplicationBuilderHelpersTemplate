namespace Domain.Grid.Enums;

/// <summary>
/// Error codes for Grid protocol errors.
/// </summary>
public enum GridErrorCode : byte
{
    /// <summary>No error.</summary>
    None = 0x00,
    /// <summary>Device ID already registered.</summary>
    DeviceIdCollision = 0x01,
    /// <summary>Maximum connections exceeded.</summary>
    ConnectionLimitExceeded = 0x02,
    /// <summary>Device not found.</summary>
    DeviceNotFound = 0x03,
    /// <summary>Node not found.</summary>
    NodeNotFound = 0x04,
    /// <summary>Invalid message format.</summary>
    InvalidMessage = 0x05,
    /// <summary>Registration failed.</summary>
    RegistrationFailed = 0x06,
    /// <summary>Internal error.</summary>
    InternalError = 0xFF,
}
