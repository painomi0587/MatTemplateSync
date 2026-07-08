# Plan 0006: スタイルプリセットライブラリ（v0.4 の柱）

## 実装状況: 未着手

> 出典: 2026-07-08 マルチエージェントリファクタリング検討（新機能提案エージェントの提案1）

## 関連 Plan

- [Plan 0002: v0.2 ユーザビリティ改善](0002-v02-usability.md) — 前提（`_lilToonVersion` 齟齬警告・null 非コピー原則を流用）
- [Plan 0003: v0.3 Before/After プレビュー](0003-v03-preview.md) — 前提（プリセット適用も同一プレビューコードパスに乗せる）
- [Plan 0007: 同期リンクと再適用](0007-sync-links-reapply.md) — 本 Plan の `IPropertySource` 抽象化が共通基盤になる

## 目的

テンプレートマテリアルから「カテゴリマスク + プロパティ値スナップショット」を名前付きプリセットとして
保存し、マテリアル非依存で呼び出し・適用できるようにする。JSON エクスポートで別プロジェクト・
別クリエイターとのスタイル共有（VRChat 界隈の設定レシピ共有文化）に対応する。

## 現状の挙動

適用元は常に「生きている Material 参照」のみ。「肌はこの影設定」という定番スタイルを再利用するには
テンプレート元 .mat を保持し続ける必要があり、消すと再現できない。プロジェクト間共有の手段もない。

## 変更内容（項目）

### 項目1: IPropertySource 抽象化（先行リファクタ）

- **対象**: `Editor/TemplateApplier.cs`
- `ApplyToMaterial` のプロパティ供給源を「Material」から `IPropertySource`
  （Material 実装とスナップショット実装）に抽象化する
- 純関数構造と「プレビューと本適用の単一コードパス」不変条件を保つ。
  Plan 0007（同期リンク）・将来のマルチテンプレート合成の共通基盤となるため、
  **v0.4 の最初のリファクタとして先行実施する**

### 項目2: プリセットの保存・管理

- **対象**: 新規 `Editor/SyncPreset.cs` / `Editor/PresetStore.cs`
- `SyncPreset`（ScriptableObject）: 名前 / カテゴリマスク / `(name, type, value)` リスト。
  テクスチャは GUID 参照。`_lilToonVersion` を記録し適用時に齟齬警告（Plan 0002 項目5 と接続）
- スナップショット採取は PropertyTable 経由に限定する（除外リスト＝レンダーステート系が
  構造的に混入しない）
- `PresetStore`: JSON 入出力（エクスポート/インポート）

### 項目3: UI

- **対象**: `Editor/MatTemplateSyncWindow.cs`
- 「プリセット」フォールドアウトを追加: 現テンプレート+マスクから保存 / 一覧から選択して適用 /
  JSON エクスポート・インポート

## 影響範囲

- `Editor/TemplateApplier.cs`（シグネチャ抽象化）/ `Editor/MatTemplateSyncWindow.cs` / 新規2ファイル
- プレビュー（Plan 0003）実装済みの場合はプレビュー経路も `IPropertySource` を通す

## テスト方針

- Plan 0005 のテスト骨格があれば、スナップショット採取（除外混入なし）と
  `IPropertySource` 経由のコピー計画を EditMode テストで検証する
- 実機: プリセット保存→テンプレート .mat 削除→プリセット適用、の生存確認

## 破壊的変更の許容範囲

`TemplateApplier` の内部シグネチャ変更は許容。保存済みプリセットの JSON スキーマは
本 Plan 以降は後方互換を保つ（`version` フィールドを最初から入れる）。

## 要オーナー確認

なし

## 完了条件

- [ ] Material なしでプリセットのみから適用できる
- [ ] テクスチャ GUID 欠損時に対象側を null で潰さない（Plan 0004 の原則を踏襲）
- [ ] スナップショットにレンダーステート系が混入しない（テストで検証）
- [ ] JSON エクスポート→別プロジェクトでインポート→適用が通る <!-- human-judgment -->
- [ ] addf-code-review-agent のレビューで Critical/High 指摘なし

## AI 実装時間見積もり

2セッション程度（項目1 を独立コミットに分けること）
