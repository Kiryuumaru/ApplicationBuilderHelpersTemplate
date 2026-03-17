using Domain.Grid.Enums;

namespace Domain.Grid.Models;

/// <summary>
/// Base message for Grid protocol communication.
/// </summary>
public sealed class GridMessage
{
    /// <summary>
    /// Message type identifier.
    /// </summary>
    public GridMessageType Type { get; init; }

    /// <summary>
    /// Optional source identifier (device ID or node ID).
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>
    /// Optional target identifier (device ID or node ID).
    /// </summary>
    public string? TargetId { get; init; }

    /// <summary>
    /// Optional payload data.
    /// </summary>
    public byte[]? Payload { get; init; }

    /// <summary>
    /// Optional error code for error messages.
    /// </summary>
    public GridErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Creates a node registration message.
    /// </summary>
    public static GridMessage CreateNodeRegister(string nodeId)
    {
        return new GridMessage
        {
            Type = GridMessageType.NodeRegister,
            SourceId = nodeId ?? throw new ArgumentNullException(nameof(nodeId))
        };
    }

    /// <summary>
    /// Creates a node registration acknowledgment.
    /// </summary>
    public static GridMessage CreateNodeRegisterAck(string nodeId, bool success)
    {
        return new GridMessage
        {
            Type = GridMessageType.NodeRegisterAck,
            TargetId = nodeId,
            ErrorCode = success ? GridErrorCode.None : GridErrorCode.RegistrationFailed
        };
    }

    /// <summary>
    /// Creates a device registration message.
    /// </summary>
    public static GridMessage CreateDeviceRegister(string deviceId)
    {
        return new GridMessage
        {
            Type = GridMessageType.DeviceRegister,
            SourceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId))
        };
    }

    /// <summary>
    /// Creates a device registration acknowledgment.
    /// </summary>
    public static GridMessage CreateDeviceRegisterAck(string deviceId, bool success, GridErrorCode errorCode = GridErrorCode.None)
    {
        return new GridMessage
        {
            Type = GridMessageType.DeviceRegisterAck,
            TargetId = deviceId,
            ErrorCode = success ? GridErrorCode.None : errorCode
        };
    }

    /// <summary>
    /// Creates a device connected notification (node → router).
    /// </summary>
    public static GridMessage CreateDeviceConnected(string deviceId)
    {
        return new GridMessage
        {
            Type = GridMessageType.DeviceConnected,
            SourceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId))
        };
    }

    /// <summary>
    /// Creates a device disconnected notification (node → router).
    /// </summary>
    public static GridMessage CreateDeviceDisconnected(string deviceId)
    {
        return new GridMessage
        {
            Type = GridMessageType.DeviceDisconnected,
            SourceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId))
        };
    }

    /// <summary>
    /// Creates a data message.
    /// </summary>
    public static GridMessage CreateData(string? sourceId, string? targetId, byte[] payload)
    {
        return new GridMessage
        {
            Type = GridMessageType.Data,
            SourceId = sourceId,
            TargetId = targetId,
            Payload = payload ?? throw new ArgumentNullException(nameof(payload))
        };
    }

    /// <summary>
    /// Creates a send-to-device command (router → node).
    /// </summary>
    public static GridMessage CreateSendToDevice(string deviceId, byte[] payload)
    {
        return new GridMessage
        {
            Type = GridMessageType.SendToDevice,
            TargetId = deviceId ?? throw new ArgumentNullException(nameof(deviceId)),
            Payload = payload ?? throw new ArgumentNullException(nameof(payload))
        };
    }

    /// <summary>
    /// Creates a device message relay (node → router).
    /// </summary>
    public static GridMessage CreateDeviceMessage(string deviceId, byte[] payload)
    {
        return new GridMessage
        {
            Type = GridMessageType.DeviceMessage,
            SourceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId)),
            Payload = payload ?? throw new ArgumentNullException(nameof(payload))
        };
    }

    /// <summary>
    /// Creates a ping message.
    /// </summary>
    public static GridMessage CreatePing()
    {
        return new GridMessage { Type = GridMessageType.Ping };
    }

    /// <summary>
    /// Creates a pong message.
    /// </summary>
    public static GridMessage CreatePong()
    {
        return new GridMessage { Type = GridMessageType.Pong };
    }

    /// <summary>
    /// Creates an error message.
    /// </summary>
    public static GridMessage CreateError(GridErrorCode errorCode, string? targetId = null)
    {
        return new GridMessage
        {
            Type = GridMessageType.Error,
            TargetId = targetId,
            ErrorCode = errorCode
        };
    }
}
