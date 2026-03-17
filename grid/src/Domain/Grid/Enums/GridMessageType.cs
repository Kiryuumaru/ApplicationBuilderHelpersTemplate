namespace Domain.Grid.Enums;

/// <summary>
/// Message types for Grid protocol communication.
/// </summary>
public enum GridMessageType : byte
{
    // Node ↔ Router messages (0x00-0x1F)
    /// <summary>Cloud node registers with router.</summary>
    NodeRegister = 0x01,
    /// <summary>Router acknowledges node registration.</summary>
    NodeRegisterAck = 0x02,
    /// <summary>Cloud node reports device connected.</summary>
    DeviceConnected = 0x03,
    /// <summary>Cloud node reports device disconnected.</summary>
    DeviceDisconnected = 0x04,
    /// <summary>Forward message from device to router.</summary>
    DeviceMessage = 0x05,
    /// <summary>Router sends data to specific device via node.</summary>
    SendToDevice = 0x06,
    /// <summary>Router requests channel to device.</summary>
    OpenChannel = 0x07,

    // Device ↔ Node messages (0x20-0x3F)
    /// <summary>Device registers with cloud node.</summary>
    DeviceRegister = 0x20,
    /// <summary>Cloud node acknowledges device registration.</summary>
    DeviceRegisterAck = 0x21,
    /// <summary>Application data message.</summary>
    Data = 0x22,

    // Common messages (0xF0-0xFF)
    /// <summary>Keep-alive ping.</summary>
    Ping = 0xF0,
    /// <summary>Keep-alive pong.</summary>
    Pong = 0xF1,
    /// <summary>Error notification.</summary>
    Error = 0xFE,
}
