# Gatebreaker Arena LAN 联机测试方案 v0.1

## 完成情况

- 当前状态：进行中
- 进度说明：已形成可执行测试分层、同机限制说明、真机验收流程和日志取证标准；待真实多端执行后补充结果。
- 最近更新：2026-05-31，落地 LAN 联机测试方案与当前实现限制。

## 一、目标

本方案用于验证 Gatebreaker Arena 当前 LAN 房间与 Lockstep 联机链路是否可用，并区分“UI 显示成功”和“真实网络/同步成功”。

重点确认：

- 同一局域网下，两台设备或两个客户端能创建房间、发现房间、加入房间。
- 同一台机器上是否能用不同客户端近似验证联机流程。
- 房间创建、发现、加入、准备、开始、同步对局、退出/断线流程是否正常。
- 当前代码不支持可靠同机多 IP 模拟时，明确原因并给出最接近真实 LAN 的测试方式。

## 二、当前实现事实

当前联网方向是 LAN 房间 + Lockstep。

已落地职责：

- `App.AOT.Networking.Lan.LanTransport`：AOT socket transport，只处理 UDP/TCP 字节传输和 transport event，不理解房间或玩法规则。
- `LanRoomService`：HotUpdate 房间状态机，处理发现、加入、准备、loading ack、playing、lockstep 消息。
- `LanRoomTransportBridge`：把 `ILanTransport` 的 discovery/TCP event 桥接给 `LanRoomService`，并把 room service 的发送请求转成 transport 调用。
- `GatebreakerNetworkMatchController`：Playing 后消费 confirmed lockstep frame，驱动 `GatebreakerMatchRuntime` 固定帧，并每 30 帧提交 checksum report。

网络事实：

- UDP discovery 默认端口是 `47680`。
- discovery receiver 当前绑定 `0.0.0.0:47680`。
- discovery 默认广播目标是 `255.255.255.255:47680`。
- TCP host 使用 `IPAddress.Any` 监听，端口可自动分配。
- TCP listener 本地 endpoint 可能显示为 `0.0.0.0:port`；客户端真正连接的 host 地址来自 UDP discovery 的 remote endpoint，再替换 advertise 中携带的 TCP port。

当前风险：

- `GatebreakerPrototypeRunner.StartLanDiscovery()` 没有检查 `StartDiscovery()` 返回值。
- `LanTransportEventType.Error` 当前不会被 `LanRoomTransportBridge` 转成 `LanRoomService` 的 room error。
- discovery 端口冲突可能只出现在 Unity log / transport event 中，UI 仍可能进入发现或房间界面。
- `LanRoomTransportBridge` 收到 `Disconnected` 当前只移除连接映射，不通知 `LanRoomService` 触发业务 leave/abort；强关 client 不应作为现有业务 abort 的通过标准。

## 三、测试分层

### 1. 本机协议与状态机测试

目的：在不依赖真实网络的情况下确认协议、房间状态机和 lockstep runtime 基线正确。

执行：

- 在 Tuanjie/Unity Test Runner 中运行 EditMode tests。
- 重点用例：
  - `GatebreakerLanApiReadinessTests`
  - `GatebreakerLanTestTransport`
  - `GatebreakerLockstepRuntimeTests`

观察点：

- codec roundtrip、payload hash mismatch reject、protocol mismatch reject。
- advertise 携带 TCP port，并能从 discovery endpoint 推导 reliable endpoint。
- fake transport 能驱动 join、ready、start ack、missing input timeout、checksum desync abort。
- deterministic lockstep runtime 在相同配置和输入下 checksum 一致。

通过标准：

- 除已明确 `Assert.Ignore` 的 readiness guard 外，LAN/lockstep 相关测试通过。
- 测试失败时先判断是协议/状态机问题，还是真实网络问题；不要用 UI 冒烟替代这些测试。

### 2. 同机多客户端测试

目的：在一台 macOS 上用 `Editor + Standalone Build` 或两个 Standalone 近似验证创建、发现、加入流程。

当前限制：

- 两个真实 transport 不能可靠同时绑定 `0.0.0.0:47680`。
- 因为 discovery bind 是 `IPAddress.Any`，给 macOS 增加 `ifconfig alias` 不能解决固定端口冲突。
- `SO_REUSEADDR/SO_REUSEPORT` 可以作为实验方向，但不作为验收标准。

推荐客户端组合：

- 首选：Editor + Standalone Build。
- 次选：两个 Standalone Build。
- 不建议：同一个工程开两个 Editor。
- 不建议：Docker/NAT/pf 端口转发作为正式结论。

测试 A：Host 先创建，Client 后发现

步骤：

1. 启动 Host 客户端，进入 Online，创建 LAN 房间。
2. 记录 Host UI 上的 room code、本机 LAN IP、日志中的 TCP listen port。
3. 启动 Client 客户端，进入 Online，点击发现。
4. 输入或选择 room code，尝试加入。

预期：

- Client 的 `StartDiscovery()` 可能因为 Host 已占用 `47680` 而失败。
- UI 仍可能进入发现界面；必须查看日志确认是否有 `DiscoveryPortInUse` 或 discovery start failed。

通过标准：

- 如果 Client discovery 端口冲突，记录为“当前同机限制”，不判定真实 LAN 失败。
- 如果仍能发现并加入，继续验证 ready/start/playing，但必须用事件链和日志证明真实成功。

测试 B：Client 先发现，Host 后创建

步骤：

1. 先启动 Client，进入 Online，点击发现，让 Client 占用 `0.0.0.0:47680`。
2. 再启动 Host，进入 Online，创建 LAN 房间。
3. 观察 Host 是否出现 discovery bind 失败日志。
4. 观察 Client 是否收到 Host 的 room advertise。
5. Client 输入或选择 room code，尝试加入。

为什么这条路径值得试：

- Host 创建房间会调用 `StartDiscovery()`，但 Host 发送 advertise 使用临时 UDP sender，不依赖 discovery receiver 成功绑定。
- 因此即使 Host discovery receiver 绑定失败，仍可能广播房间广告给已在监听的 Client。

通过标准：

- Client 收到 `DiscoveryReceived(RoomAdvertise)`。
- Client 建立 TCP 连接并发送 join request。
- Host/Client 都进入 Lobby，玩家数更新。
- 后续 ready/start/playing 满足“真实联机成功证据链”。

### 3. 同局域网真机测试

目的：正式验收 LAN 能力。

推荐组合：

- 两台真实设备。
- 一台 Mac Editor + 另一台 Standalone/真机。
- 两台电脑各运行一个 Standalone。

网络要求：

- 两端在同一 Wi-Fi/VLAN，能互相访问。
- 路由器未禁用 client isolation。
- 系统防火墙允许 UDP `47680` 和 Host 自动分配的 TCP 端口。
- 如开启 VPN，需要确认 VPN 不拦截局域网广播。

步骤：

1. Host 启动游戏，进入 Online，创建 LAN 房间。
2. 记录 Host 的 room code、LAN IP、TCP port、`game.log` 路径。
3. Client 启动游戏，进入 Online，点击发现。
4. Client 看到房间后加入；如果 UI 不显示房间，查看日志是否收到 advertise。
5. Client 切换 ready。
6. Host 确认 `CanStart` 后开始。
7. Client 进行 loading ack。
8. 双方进入 Playing。
9. 双方分别移动和发球，观察画面和状态同步。
10. Playing 后至少观察 60 秒或覆盖 30/60/90 帧 checksum report。

通过标准：

- Client 收到 discovery advertise。
- TCP `Connected` 事件出现。
- join request / join response / snapshot 通过 `DataReceived` 传输。
- 双方都进入 `RoomPlaying`。
- `LockstepFrameBundle` 持续到达并被 runtime 消费。
- 每 30 帧 checksum report 无 desync。
- 一端移动/发球后，双方画面持续一致推进。

## 四、流程专项测试

### 创建与发现

观察点：

- Host 日志出现 TCP host started，记录实际端口。
- Client 日志出现 `DiscoveryReceived(RoomAdvertise)`。
- room code、host player name、active players、max players 正确。

通过标准：

- Client 发现的 reliable endpoint 使用 discovery remote IP + advertise TCP port。
- 只看到 UI 房间列表不算通过，必须能从日志或 transport event 证明收到 advertise。

### 加入与大厅

观察点：

- Client 发出 `RoomJoinRequest`。
- Host 收到 join request 并返回 `RoomJoinResponse`。
- 双方 snapshot 中 active player 数一致。

通过标准：

- Host 和 Client 都进入 Lobby。
- Client 获得非负 `LocalSlotIndex`。
- 玩家名、slot、player id 在双方 snapshot 中一致。

### Ready、Start 与 Loading Ack

观察点：

- Client ready 后 Host snapshot 更新。
- Host `CanStart` 变为 true。
- Host start 后进入 Loading。
- Client ack 后双方进入 Playing。

通过标准：

- Host 只在所有 active players ready 后可 start。
- Loading ack 完成后，双方进入 `LanRoomState.Playing`。

### 对局同步

观察点：

- Host 收到 lockstep input。
- Client 收到 `LockstepFrameBundle`。
- `GatebreakerNetworkMatchController` 消费 confirmed frame。
- 30 帧间隔 checksum report 无 desync。

通过标准：

- 双方画面和比分持续一致。
- 无 `MatchAbortReason.Desync`。
- 无 missing input timeout，除非专项测试故意制造。

### 主动退出

观察点：

- 非 Playing 大厅中离开：Host 清 slot 并广播 snapshot。
- Playing 中离开：当前实现应通过 room leave/abort 处理，具体以日志和 state 为准。

通过标准：

- 主动点击离开能产生 `RoomLeave` 相关消息或状态变化。
- 另一端不会继续显示一个可正常开始/继续的旧房间状态。

### 强关与断线

观察点：

- TCP transport 可能产生 `Disconnected`。
- 当前 `LanRoomTransportBridge` 只移除 connection mapping。
- 当前不保证 Host 自动触发业务 abort。

通过标准：

- 本项现阶段不作为“必须自动 abort”的通过项。
- 如强关 Client 后 Host 没有业务 abort，应记录为当前待补能力，而不是把测试判为网络链路失败。

## 五、真实联机成功证据链

一次 LAN 联机不能只用 UI 判断。至少记录以下证据：

- `DiscoveryReceived(RoomAdvertise)`
- `Connected`
- `DataReceived(RoomJoinRequest)`
- `DataReceived(RoomJoinResponse)`
- `DataReceived(RoomSnapshot)`
- 双方 snapshot state 都是 `RoomPlaying`
- `LockstepFrameBundle` 持续到达
- 每 30 帧 checksum report 无 desync
- transport stats 中 bytes/packets sent/received 增长
- active connections 与玩家数匹配

如果 UI 显示进入房间，但没有 discovery/TCP/data/lockstep 证据，判定为“UI 可能假成功，需继续排查”。

## 六、日志与取证

必须保留：

- `Application.persistentDataPath/logs/game.log`
- `Application.persistentDataPath/logs/game.previous.log`
- Unity Editor log 或 Player log
- Host/Client 截图：创建房间、发现房间、加入后玩家数、ready、playing、退出/断线状态
- room code
- Host LAN IP
- Host TCP port
- Client discovered endpoint
- transport stats：bytes/packets sent/received、active connections、errors
- room state transition
- lockstep frame index
- checksum report frame 与 checksum value

Android 取证路径示例：

```bash
adb pull /sdcard/Android/data/<package>/files/logs/game.log
adb pull /sdcard/Android/data/<package>/files/logs/game.previous.log
```

iOS 取证：

- 通过 Xcode Devices 下载 app container。
- 查看 app sandbox 中的 `Documents/logs/game.log` 或 Unity 对应 `persistentDataPath/logs/game.log`。

## 七、macOS 模拟不同 IP 判断

可行但有限：

- `ifconfig en0 alias 192.168.50.x`：只能增加本机 IP，当前 transport 绑定 `IPAddress.Any`，不能解决多个进程争抢 `0.0.0.0:47680`。
- `lo0` alias：适合未来 manual IP:port/TCP 直连测试，不适合验证真实 LAN broadcast。
- 虚拟机 bridged network：比 alias 更接近真实 LAN；NAT 模式不可靠。

不建议作为验收：

- Docker Desktop：macOS Docker 在 VM/NAT 后面，UDP broadcast 与真实 LAN 差异大。
- pf/NAT 端口转发：不能证明两个真实 transport 能同时 discovery。
- `SO_REUSEADDR/SO_REUSEPORT`：可做实验，不作为产品验收标准。

正式验收标准仍是两台同局域网真实设备或两台电脑。

## 八、最小后续改动建议

如要让 LAN 测试更自动化、更可靠，建议按以下边界补能力：

- AOT：给 `LanTransport` 增加 discovery bind IP/port、broadcast endpoint、TCP preferred port 配置；保留默认 `47680` 和 `0.0.0.0` 行为。
- Shared：只在确有跨层需要时增加稳定配置 DTO 或接口，不放玩法规则。
- HotUpdate：增加 manual IP:port join、启动参数读取、房间/测试入口编排、transport error 到 room error 的展示。
- HotUpdate：让 `Disconnected` 能按当前房间状态转成明确业务处理，例如大厅清 slot、Playing abort。
- 日志：补 LAN 专用结构化日志，记录 state transition、endpoint、message type、frame index、checksum。

这些改动不是当前测试方案的前置条件；当前方案先用于验证现有能力和暴露限制。

## 九、执行 Checklist

- [ ] 确认 `game.log` 文件日志可用。
- [ ] 确认两端在同一网段。
- [ ] 确认防火墙允许 UDP `47680` 和 Host TCP port。
- [ ] Host 创建房间，记录 room code、IP、TCP port。
- [ ] Client 发现房间，记录 discovered endpoint。
- [ ] Client 加入，双方玩家数一致。
- [ ] Client ready，Host `CanStart`。
- [ ] Host start，Client ack。
- [ ] 双方进入 `RoomPlaying`。
- [ ] 移动/发球，观察双方画面一致。
- [ ] 记录 lockstep frame bundle 和 checksum report。
- [ ] 测主动离开。
- [ ] 测强关/断线，并标记当前是否触发业务 abort。
- [ ] 汇总日志、截图、通过/失败原因。
