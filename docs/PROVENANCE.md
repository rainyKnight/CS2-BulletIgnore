# 技术来源

前期原生实现来自 BulletIgnoreTestMM 压缩包，其元信息标注作者 seeseecs2、许可 MIT。项目随后完成 Linux/ModSharp 移植与 ZM 模式集成，再整理为独立插件。

原有 MIT 声明位于 `LICENSES/MIT-BulletIgnoreTestMM.txt`，当前独立项目采用 AGPL-3.0-only。来源文件和前期压缩包的 SHA256 见 `extraction-provenance.json`。

## 保留的行为

- Linux 44 字节签名、r15 上下文、原生字段偏移及数量上限。
- 命中链稳定压缩与起始表面修正。
- 先解析通用实体句柄，再检查 Pawn 类名和 CT 队伍。
- 不比较射手与目标的队伍，不增加存活状态判定。
- Hook 生命周期、统计计数与托管异常隔离。

## 独立化调整

- 独立的模块入口、配置及命名空间，不依赖原模式项目的 Contracts、Domain、数据库或资源包。
- 将数组处理抽为 NativeHitChainCompactor，缓存实体判定委托，避免每次回调创建委托。
- 默认关闭；启用需确认 CT-only 行为。
- Linux x64 架构检查及签名唯一性检查。
- 保留判定失败的命中项，确保托管判定异常不会中断压缩计数的提交。
- 合成内存边界测试、哨兵检查与固定种子随机对照测试。
