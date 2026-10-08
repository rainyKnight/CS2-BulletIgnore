# CS2-BulletIgnore

[English](README.md) | **简体中文**

基于 **ModSharp** 的 CS2 人类子弹穿透插件，避免子弹经过前方人类玩家时产生额外穿透损耗，导致后方僵尸受到的伤害下降。

## 功能

- 忽略子弹命中链中的 CT 玩家 Pawn。
- 修正后续命中段的起始表面，避免继续使用人类身体材质计算穿透。
- 保留墙体、道具和其他目标的正常穿透处理。
- 不通过伤害倍率补偿，不全局提高武器伤害。
- 提供启用开关及运行统计，无需数据库或额外资源包。

## 实现原理

插件在原生 `FireBullet` 生成命中链之后、计算穿透和伤害之前介入。移除需要忽略的玩家命中项并修正剩余命中段，使引擎继续处理后方目标，避免前方人类造成额外损耗。

## 适用范围

当前版本针对**人类固定为 CT、僵尸固定为 T**的模式。

实现固定过滤 CT 玩家 Pawn，未比较射手与目标的队伍，因此不是通用的“双向队友穿透”逻辑。

## 环境要求

| 项目 | 要求 |
|---|---|
| 服务器系统 | Linux x64 |
| 框架 | ModSharp 2.1.169 |
| 运行环境 | .NET 10 |

插件依赖版本相关的原生签名和结构布局，CS2 更新后需要检查兼容性。签名没有唯一匹配时，插件拒绝安装 Hook。实现细节见 [原生兼容性说明](docs/NATIVE-COMPATIBILITY.md)。

## 安装

将插件包中的 `sharp/` 合并到服务器的 `game/sharp/`，保留已有配置：

```text
game/sharp/
├── modules/
│   └── BulletIgnore/
│       ├── BulletIgnore.dll
│       └── BulletIgnore.deps.json
└── configs/
    └── bullet-ignore.json
```

按 ModSharp 的标准模块加载流程加载插件。

不要与占用相同原生 Hook 位置的插件同时启用，包括仍启用该功能的原 ZM 集成版本。

## 配置

配置文件：`game/sharp/configs/bullet-ignore.json`。

默认关闭：

```json
{
  "enabled": false,
  "ct_only_policy_acknowledged": false
}
```

| 配置项 | 说明 |
|---|---|
| `enabled` | 是否启用功能 |
| `ct_only_policy_acknowledged` | 确认理解当前版本固定过滤 CT 玩家 |

启用示例：

```json
{
  "enabled": true,
  "ct_only_policy_acknowledged": true
}
```

配置在模块加载时读取，修改后需要重新加载模块。配置缺失时默认关闭；配置错误或未确认 CT-only 行为时拒绝启用。

## 构建

需要 .NET SDK **10.0.301**：

```sh
dotnet build CS2-BulletIgnore.slnx -c Release --disable-build-servers --maxcpucount:1
dotnet test CS2-BulletIgnore.slnx -c Release --disable-build-servers --maxcpucount:1
python scripts/package.py
```

输出到 `artifacts/`：

- `CS2-BulletIgnore-0.1.0-source.zip`：源码包。
- `CS2-BulletIgnore-0.1.0-plugin.zip`：插件包，示例配置默认关闭。
- `SHA256SUMS`：文件校验值。

## 赞助支持

如果这个项目对你有帮助，欢迎自愿赞助，支持项目的开发与维护。项目免费开源，是否赞助不影响使用。

请使用对应的支付应用扫码，点击图片可查看原图。

<table>
  <tr>
    <th align="center">微信支付 · 1</th>
    <th align="center">微信支付 · 2</th>
  </tr>
  <tr>
    <td align="center"><a href="assets/sponsorship/wechat-1.jpg"><img src="assets/sponsorship/wechat-1.jpg" width="260" alt="微信支付 1 赞助收款码"></a></td>
    <td align="center"><a href="assets/sponsorship/wechat-2.jpg"><img src="assets/sponsorship/wechat-2.jpg" width="260" alt="微信支付 2 赞助收款码"></a></td>
  </tr>
  <tr>
    <th align="center">支付宝 · 1</th>
    <th align="center">支付宝 · 2</th>
  </tr>
  <tr>
    <td align="center"><a href="assets/sponsorship/alipay-1.jpg"><img src="assets/sponsorship/alipay-1.jpg" width="260" alt="支付宝 1 赞助收款码"></a></td>
    <td align="center"><a href="assets/sponsorship/alipay-2.jpg"><img src="assets/sponsorship/alipay-2.jpg" width="260" alt="支付宝 2 赞助收款码"></a></td>
  </tr>
</table>

## 许可证

本项目采用 **GNU Affero General Public License v3.0（AGPL-3.0-only）**，详见 [LICENSE](LICENSE)。

原有 MIT 代码声明及外部依赖说明见 [LICENSE-NOTICES.md](LICENSE-NOTICES.md) 和 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
