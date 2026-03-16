# NetConduit.Grid - Design Plan

## Overview

NetConduit.Grid provides scalable cloud-to-device communication infrastructure designed for serverless/containerized environments like GCP Cloud Run. Uses a hierarchical architecture: Devices → Cloud Nodes → Router → Users.

**Design Principles:**
- Hierarchical topology (not mesh)
- Max 100 connections per node at each level
- Cloud nodes aggregate devices
- Router aggregates cloud nodes and handles user requests
- Any NetConduit transport

**Capacity:**
- 1 Router → up to 100 Cloud Nodes
- 1 Cloud Node → up to 100 Devices
- Total: ~10,000 devices per router
- Scale horizontally by adding more routers

---

## Architecture

```
                              Connection Hierarchy
═══════════════════════════════════════════════════════════════════════════════

                                    ┌───────────┐
                                    │   Users   │
                                    │ (Clients) │
                                    └─────┬─────┘
                                          │ User requests
                                          ▼
                              ┌───────────────────────┐
                              │      GridRouter       │
                              │                       │
                              │  Max 100 cloud node   │
                              │     connections       │
                              │                       │
                              │  ┌─────────────────┐  │
                              │  │ Device Registry │  │
                              │  │ DEV-001 → CN-1  │  │
                              │  │ DEV-002 → CN-1  │  │
                              │  │ DEV-003 → CN-2  │  │
                              │  └─────────────────┘  │
                              └───────────┬───────────┘
                                          │
              ┌───────────────────────────┼───────────────────────────┐
              │                           │                           │
              ▼                           ▼                           ▼
     ┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
     │  Cloud Node 1   │         │  Cloud Node 2   │         │  Cloud Node N   │
     │                 │         │                 │         │                 │
     │  Max 100 device │         │  Max 100 device │         │  Max 100 device │
     │   connections   │         │   connections   │         │   connections   │
     └────────┬────────┘         └────────┬────────┘         └─────────────────┘
              │                           │
      ┌───────┴───────┐           ┌───────┴───────┐
      │               │           │               │
      ▼               ▼           ▼               ▼
┌──────────┐   ┌──────────┐ ┌──────────┐   ┌──────────┐
│ DEV-001  │   │ DEV-002  │ │ DEV-003  │   │ DEV-004  │
└──────────┘   └──────────┘ └──────────┘   └──────────┘


Connection Summary:
═══════════════════════════════════════════════════════════════════════════════

  Layer          │ Connects To    │ Max Connections │ Protocol
  ───────────────┼────────────────┼─────────────────┼─────────────────────
  Device         │ Cloud Node     │ 1 (single)      │ NetConduit transport
  Cloud Node     │ Router         │ 1 (single)      │ NetConduit transport
  Cloud Node     │ Devices        │ 100 max         │ NetConduit transport
  Router         │ Cloud Nodes    │ 100 max         │ NetConduit transport
  User           │ Router         │ (external API)  │ HTTP/gRPC/WebSocket


User Request Flow (Send to Device):
═══════════════════════════════════════════════════════════════════════════════

  ┌──────────┐     1. SendToDevice(DEV-001, data)     ┌──────────────┐
  │   User   │ ─────────────────────────────────────► │  GridRouter  │
  └──────────┘                                        └──────┬───────┘
                                                             │
                                            2. Lookup: DEV-001 → Cloud Node 1
                                                             │
                                                             ▼
                                                      ┌──────────────┐
                                                      │ Cloud Node 1 │
                                                      └──────┬───────┘
                                                             │
                                            3. Deliver to local device
                                                             │
                                                             ▼
                                                      ┌──────────────┐
                                                      │   DEV-001    │
                                                      └──────────────┘


Device Connection Flow:
═══════════════════════════════════════════════════════════════════════════════

  ┌──────────┐                                    ┌──────────────┐
  │  Device  │ ──1. Connect (deviceId=DEV-001)──► │ Cloud Node N │
  └──────────┘                                    └──────┬───────┘
                                                         │
                                        2. Register device with router
                                                         │
                                                         ▼
                                                  ┌──────────────┐
                                                  │  GridRouter  │
                                                  │  (Registry)  │
                                                  └──────────────┘


Cloud Node Startup Flow:
═══════════════════════════════════════════════════════════════════════════════

  ┌──────────────┐                              ┌──────────────┐
  │ Cloud Node N │ ──1. Connect & Register────► │  GridRouter  │
  └──────────────┘                              └──────────────┘
        │
        │ 2. Now ready to accept device connections
        ▼
  ┌──────────────┐
  │   Devices    │
  └──────────────┘
```

---

## Components

### GridRouter

Central routing component. Aggregates cloud nodes and handles user requests.

**Responsibilities:**
- Accept cloud node connections (max 100)
- Maintain device registry (device ID → cloud node)
- Handle user requests and route to correct cloud node
- Relay messages to devices via cloud nodes
- Handle cloud node health and failover

**API:**
```csharp
public class GridRouter : IAsyncDisposable
{
    // Configuration
    public GridRouter(GridRouterOptions options);
    
    // Lifecycle
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    
    // User-facing: Message routing
    Task SendToDeviceAsync(string deviceId, ReadOnlyMemory<byte> data, CancellationToken ct);
    Task<Stream> OpenChannelToDeviceAsync(string deviceId, CancellationToken ct);
    Task BroadcastToAllDevicesAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    
    // User-facing: Device queries
    bool IsDeviceConnected(string deviceId);
    IReadOnlyList<string> GetConnectedDeviceIds();
    IReadOnlyList<string> GetConnectedCloudNodes();
    
    // Events
    event Action<string> DeviceConnected;       // deviceId
    event Action<string> DeviceDisconnected;    // deviceId
    event Action<string> CloudNodeConnected;    // nodeId
    event Action<string> CloudNodeDisconnected; // nodeId
}
```

### GridCloudNode

Device aggregator. Connects to router, accepts device connections.

**Responsibilities:**
- Connect to router on startup
- Accept device connections (max 100)
- Register/unregister devices with router
- Relay messages between router and devices
- Handle device messages and channels

**API:**
```csharp
public class GridCloudNode : IAsyncDisposable
{
    // Configuration
    public GridCloudNode(GridCloudOptions options);
    
    // Lifecycle
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    
    // Properties
    bool IsConnectedToRouter { get; }
    int ConnectedDeviceCount { get; }
    
    // Events
    event Action<string> DeviceConnected;      // deviceId
    event Action<string> DeviceDisconnected;   // deviceId
    event Action<string, ReadOnlyMemory<byte>> DeviceMessageReceived;  // deviceId, data
}
```

### GridDeviceNode

Client-side component running on each device.

**Responsibilities:**
- Connect to cloud node endpoint
- Register device ID on connection
- Reconnect with same device ID on disconnect
- Send/receive messages to/from cloud

**API:**
```csharp
public class GridDeviceNode : IAsyncDisposable
{
    // Configuration
    public GridDeviceNode(GridDeviceOptions options);
    
    // Lifecycle
    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    
    // Communication
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    Task<Stream> OpenChannelAsync(CancellationToken ct);
    
    // Properties
    bool IsConnected { get; }
    string DeviceId { get; }
    
    // Events
    event Action Connected;
    event Action Disconnected;
    event Action<ReadOnlyMemory<byte>> MessageReceived;
}
```

---

## Internal Protocols

All internal communication uses NetConduit multiplexer channels.

### Cloud Node → Router Protocol

Cloud node connects to router and maintains persistent connection:

```
1. CloudNode connects to Router via NetConduit transport
2. CloudNode sends NodeRegistration message
3. Router acknowledges and assigns node ID
4. CloudNode reports device events via the connection
5. Router sends commands/messages via the connection
```

**Message Types (Cloud Node → Router):**

| Type | Description |
|------|-------------|
| `NodeRegister` | Register this cloud node with router |
| `DeviceConnected` | Report device connected to this node |
| `DeviceDisconnected` | Report device disconnected |
| `DeviceMessage` | Forward message from device to router |

**Message Types (Router → Cloud Node):**

| Type | Description |
|------|-------------|
| `NodeRegisterAck` | Acknowledge node registration |
| `SendToDevice` | Send data to specific device |
| `OpenChannel` | Open channel to specific device |
| `Ping` | Health check |

### Device → Cloud Node Protocol

Device connects to cloud node:

```
1. Device connects via NetConduit transport
2. Device sends DeviceRegister with deviceId
3. CloudNode acknowledges
4. CloudNode reports to Router
5. Bidirectional messaging enabled
```

**Message Types (Device ↔ Cloud Node):**

| Type | Direction | Description |
|------|-----------|-------------|
| `DeviceRegister` | D→CN | Register device with ID |
| `DeviceRegisterAck` | CN→D | Acknowledge registration |
| `Data` | Both | Application data |
| `Ping` | Both | Keep-alive |

---

## Device Protocol

### Handshake

On connection, device sends registration:

```
[1 byte: MessageType.Register]
[4 bytes: deviceId length]
[N bytes: deviceId UTF-8]
```

Cloud responds:

```
[1 byte: MessageType.RegisterAck]
[1 byte: success (0/1)]
```

### Data Messages

```
[1 byte: MessageType.Data]
[4 bytes: payload length]
[N bytes: payload]
```

### Channel Open

Uses NetConduit multiplexer channel opening.

---

## Configuration

### GridRouterOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| NodeListenEndpoint | EndPoint | - | Endpoint for cloud node connections |
| Transport | ITransport | - | Transport for cloud node connections |
| MaxCloudNodes | int | 100 | Maximum cloud node connections |
| HeartbeatInterval | TimeSpan | 30s | Interval for node heartbeat |
| NodeTimeout | TimeSpan | 90s | Mark node dead after no heartbeat |

### GridCloudOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| NodeId | string | auto | Unique identifier for this node |
| RouterEndpoint | string | - | Router endpoint to connect to |
| RouterTransport | ITransport | - | Transport to connect to router |
| DeviceListenEndpoint | EndPoint | - | Endpoint for device connections |
| DeviceTransport | ITransport | - | Transport for device connections |
| MaxDevices | int | 100 | Maximum device connections |
| HeartbeatInterval | TimeSpan | 30s | Interval to send heartbeat to router |
| ReconnectDelay | TimeSpan | 5s | Delay before reconnecting to router |

### GridDeviceOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| DeviceId | string | - | Unique device identifier |
| CloudNodeEndpoint | string | - | Cloud node endpoint to connect to |
| Transport | ITransport | - | Transport to use |
| ReconnectDelay | TimeSpan | 5s | Delay between reconnect attempts |
| HeartbeatInterval | TimeSpan | 30s | Keep-alive interval |

---

## Deployment Modes

### Mode 1: All-in-One (Development/Small Scale)

Single process runs router + cloud node (max 100 devices):

```csharp
// Single process: Router + CloudNode
var router = new GridRouter(new GridRouterOptions
{
    NodeListenEndpoint = new IPEndPoint(IPAddress.Loopback, 5001),
    Transport = new TcpTransport()
});

var cloudNode = new GridCloudNode(new GridCloudOptions
{
    RouterEndpoint = "127.0.0.1:5001",
    DeviceListenEndpoint = new IPEndPoint(IPAddress.Any, 8080),
    DeviceTransport = new WebSocketTransport(),
    MaxDevices = 100
});

await router.StartAsync(ct);
await cloudNode.StartAsync(ct);
```

### Mode 2: Distributed (Production)

Separate services, each with connection limits:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           GCP Cloud Run                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│                        ┌────────────────────┐                            │
│                        │    GridRouter      │                            │
│         Users ────────►│  (1 instance)      │                            │
│                        │  Max 100 CN conns  │                            │
│                        └─────────┬──────────┘                            │
│                                  │                                       │
│            ┌─────────────────────┼─────────────────────┐                 │
│            │                     │                     │                 │
│            ▼                     ▼                     ▼                 │
│   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐       │
│   │  Cloud Node 1   │   │  Cloud Node 2   │   │  Cloud Node N   │       │
│   │ Max 100 devices │   │ Max 100 devices │   │ Max 100 devices │       │
│   └────────┬────────┘   └────────┬────────┘   └─────────────────┘       │
│            │                     │                                       │
│            ▼                     ▼                                       │
│      ┌──────────┐          ┌──────────┐                                 │
│      │ Devices  │          │ Devices  │                                 │
│      │  ≤100    │          │  ≤100    │                                 │
│      └──────────┘          └──────────┘                                 │
│                                                                          │
│   Total capacity: 100 nodes × 100 devices = 10,000 devices              │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Mode 3: Multi-Router (Large Scale)

Multiple independent grids for >10,000 devices:

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Grid A        │     │   Grid B        │     │   Grid C        │
│   Router A      │     │   Router B      │     │   Router C      │
│   └─ 100 nodes  │     │   └─ 100 nodes  │     │   └─ 100 nodes  │
│      └─ 10K dev │     │      └─ 10K dev │     │      └─ 10K dev │
└─────────────────┘     └─────────────────┘     └─────────────────┘

Total: 30,000 devices across 3 grids
Device assignment to grid via consistent hashing or external coordinator
```

---

## Project Structure

```
src_grid/
├── GRID_PLAN.md
└── NetConduit.Grid/
    ├── NetConduit.Grid.csproj
    │
    ├── GridRouter.cs
    ├── GridRouterOptions.cs
    ├── GridCloudNode.cs
    ├── GridCloudOptions.cs
    ├── GridDeviceNode.cs
    ├── GridDeviceOptions.cs
    │
    ├── Protocol/
    │   ├── MessageType.cs
    │   ├── GridMessage.cs
    │   ├── ProtocolReader.cs
    │   └── ProtocolWriter.cs
    │
    └── Internal/
        ├── DeviceConnection.cs       # Device → CloudNode connection
        ├── CloudNodeConnection.cs    # CloudNode → Router connection
        ├── RouterDeviceRegistry.cs   # In-memory device→node mapping
        └── ConnectionLimiter.cs      # Enforce max 100 connections
```

---

## Implementation Phases

### Phase 1: Core Protocol
- [ ] Project setup and dependencies
- [ ] Message types enum
- [ ] Protocol serialization (read/write)
- [ ] ConnectionLimiter (max 100)

### Phase 2: GridDeviceNode
- [ ] Connect to cloud node endpoint
- [ ] Device registration handshake
- [ ] Send/receive data messages
- [ ] Reconnection with backoff

### Phase 3: GridCloudNode
- [ ] Accept device connections (max 100)
- [ ] Device registration handling
- [ ] Connect to router
- [ ] Report device events to router
- [ ] Relay messages router ↔ devices

### Phase 4: GridRouter
- [ ] Accept cloud node connections (max 100)
- [ ] Device registry (deviceId → nodeId)
- [ ] Route messages to correct node
- [ ] User API: SendToDevice, IsConnected, etc.

### Phase 5: Reliability
- [ ] Heartbeat/ping on all connections
- [ ] Dead connection detection
- [ ] Graceful shutdown (drain devices)
- [ ] Reconnection handling

### Phase 6: Advanced Features
- [ ] Device groups/topics
- [ ] Broadcast to all devices
- [ ] Channel (bidirectional stream) support
- [ ] Connection metrics/events

---

## Dependencies

- NetConduit (core multiplexer)
- Any NetConduit transport

---

## Cloud Run Compatibility

| Requirement | Solution |
|-------------|----------|
| HTTP/WebSocket only | Use NetConduit.WebSocket transport |
| Stateless containers | All state in router, nodes reconnect |
| Max connections | Enforced 100 limit matches Cloud Run limits |
| No fixed IPs | Nodes connect TO router (outbound) |
| Keep-alive | WebSocket with heartbeat |

### Cloud Run Deployment Example

```yaml
# Router service (single instance, stateful)
apiVersion: serving.knative.dev/v1
kind: Service
metadata:
  name: grid-router
spec:
  template:
    metadata:
      annotations:
        autoscaling.knative.dev/minScale: "1"
        autoscaling.knative.dev/maxScale: "1"
    spec:
      containers:
        - image: gcr.io/project/grid-router
          ports:
            - containerPort: 8080

# Cloud Node service (auto-scaled, up to 100 instances)
apiVersion: serving.knative.dev/v1
kind: Service
metadata:
  name: grid-node
spec:
  template:
    metadata:
      annotations:
        autoscaling.knative.dev/minScale: "1"
        autoscaling.knative.dev/maxScale: "100"
    spec:
      containers:
        - image: gcr.io/project/grid-node
          env:
            - name: ROUTER_ENDPOINT
              value: "wss://grid-router.run.app"
          ports:
            - containerPort: 8080
```

---

## Open Questions

1. **Device ID collision**: Reject duplicate or disconnect old device?
2. **Cloud node failure**: How to redistribute devices? (Let them reconnect to different node?)
3. **Router failover**: Single point of failure - need HA solution?
4. **Authentication**: Add auth hooks for device/node registration?
5. **Message ordering**: Guarantee order per device?
