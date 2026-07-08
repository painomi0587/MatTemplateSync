# Plan 0005: PropertyTable 型付け・コピー計画の純関数化・テスト骨格

## 実装状況: 未着手

> 出典: 2026-07-08 マルチエージェントリファクタリング検討（リファクタリング分析エージェントの候補3・4・5）

## 関連 Plan

- [Plan 0002: v0.2 ユーザビリティ改善](0002-v02-usability.md) — 項目1（テクスチャオプトイン UI）の前提構造を本 Plan で整える。着手タイミングは Plan 0002 の直前が最適
- [Plan 0003: v0.3 Before/After プレビュー](0003-v03-preview.md) — 項目3（Window 状態分離）は Plan 0003 フェーズ1 と同時実施でもよい
- [Plan 0004: Undo トランザクション統合とテクスチャ null 潰し防止](0004-undo-transaction-hardening.md) — 先行推奨（本 Plan は 0004 の構造前提を崩さない）

## 目的

Plan 0002/0003 が差分小で入る構造を先に作り、あわせて「Unity 実機不可環境で回帰検出できない」
という本プロジェクト最大の弱点をテスト骨格の導入で緩和する。

## 現状の挙動

1. `LilToonPropertyTable.cs:68-182` はカテゴリ→ `string[]` のフラット構造で、テクスチャ名と
   値プロパティ名の区別がない。Plan 0002 の「カテゴリごとの □ テクスチャも含める」を表現できない。
   除外リスト（`_SrcBlend` 等）は「テーブルに書かない」という消極的表現でしか守られていない
2. `TemplateApplier.ApplyToMaterial`（130-171行）は「コピー対象の決定」と「Material への書き込み」が
   1ループに融合しており、DESIGN §6 の「決定を純データ変換として切り出せば Material 不要の
   NUnit テストが書ける」が未実現。`LilToonDetector.IsSupported`（33-53行）も `Material` を受けるため
   名前トークン照合ロジックのテストに実機が要る
3. `MatTemplateSyncWindow.cs` は対象収集・追跡（230-339行）、レンダラー差し替え（481-503行）、
   適用オーケストレーション（433-479行）がすべて EditorWindow に同居。Plan 0003 のプレビューペインと
   「テンプレート・対象選択・マスク」を共有する手段が private フィールド直参照しかない

## 変更内容（項目）

### 項目1: PropertyTable エントリの型付け

- **対象**: `Editor/LilToonPropertyTable.cs`
- エントリを `readonly struct PropertyEntry { string Name; PropertyKind Kind; }`（Kind = Value / Texture）
  へ拡張するか、カテゴリごとに `ValueProperties[]` / `TextureProperties[]` の2配列に分離する
- 静的な禁止名集合（`_SrcBlend`, `_DstBlend`, `_ZWrite`, `_Cutoff`, `_StencilRef` 系,
  `_MainTex_ScrollRotate`, `_egc*` 等）を定義し、テーブルとの非交差を EditMode テストまたは
  静的コンストラクタの `Debug.Assert` で機械検証する
- 名前和集合 + 両側存在チェックというバージョン差吸収の中核方式は変えない
- 現行の名前列をそのまま移送すること（`_GlitterAtras` は lilToon 本体由来の綴りで正しい）

### 項目2: 「コピー計画」段階の分離と Detector の純関数化

- **対象**: `Editor/TemplateApplier.cs` / `Editor/LilToonDetector.cs` / 新規 `Tests/`
- `BuildCopyPlan(Shader template, Material target, IReadOnlyList<string> names)
  → List<(string name, ShaderPropertyType type)>` を分離し、`ApplyToMaterial` は計画の実行のみにする
- Detector に `IsSupportedShaderName(string shaderName, out string reason)` を切り出し、
  `IsSupported(Material)` はその薄いラッパーにする
- `Tests/` + テスト用 asmdef の骨格を用意し、ダミーシェーダー2枚の EditMode テスト
  （DESIGN §6）で計画段階を検証できるようにする
- 過剰抽象化はしない — delegate 注入まではやらず、Shader/Material を受ける2段分割に留める

### 項目3: Window の状態モデル分離（Plan 0003 とセットで可）

- **対象**: `Editor/MatTemplateSyncWindow.cs` / 新規 `Editor/SyncSession.cs`
- `[Serializable] class SyncSession`（`_template` / `_targets` / `_sourceObject` /
  `_trackedMaterials` / mask と、`AddTargets`・`SyncFromSourceObject`・`ReplaceOnSourceObject`
  相当のメソッド）を非 UI クラスとして抽出する
- Window は `[SerializeField] SyncSession _session` を持ち、描画とイベント処理に専念する
- `[SerializeField]` ネストクラスのドメインリロード復元挙動を実機確認項目に加える

## 影響範囲

- `Editor/` 全4ファイル + 新規 `Editor/SyncSession.cs` / `Tests/`
- UPM パッケージ構成に `Tests/` ディレクトリと asmdef が増える（`.meta` 同梱を忘れないこと）

## テスト方針

- 項目2 で導入するテスト骨格自体が本 Plan の成果物。`CollectProperties`・禁止名非交差・
  シェーダー名トークン照合を純 NUnit で網羅する
- addf-code-review-agent で Unity API シグネチャ（asmdef の testables 設定含む）を重点確認する

## 破壊的変更の許容範囲

`LilToonPropertyTable` の内部データ構造変更は許容（利用者は Window/Applier のみ）。

## 要オーナー確認

なし（着手タイミングのみ: Plan 0001 実機検証前に大きく動かすと検証対象がずれるため、
Plan 0001 完了後・Plan 0002 直前を推奨）

## 完了条件

- [ ] PropertyTable がテクスチャ/値を型で区別している
- [ ] 禁止名集合との非交差が機械検証されている
- [ ] `BuildCopyPlan` が分離され、純 NUnit テストが `Tests/` に存在する
- [ ] Window の状態が `SyncSession` に分離されている（Plan 0003 と同時実施の場合はそちらで消化可）
- [ ] addf-code-review-agent のレビューで Critical/High 指摘なし

## AI 実装時間見積もり

1〜2セッション
