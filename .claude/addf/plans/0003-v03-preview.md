# Plan 0003: v0.3 Before/After プレビュー

## 実装状況: 未着手

> 出典: 初期構想（ツール概要のプレビュー要件）+ 2026-07-07 マルチエージェント設計検討

## 関連 Plan

- [Plan 0001: v0.1 実機検証と修正](0001-v01-verification.md) — 前提
- [Plan 0002: v0.2 ユーザビリティ改善](0002-v02-usability.md) — 前提（バリアント警告 UI を流用）

## 目的

適用前に Before/After を視覚比較できるプレビューを追加し、「仮適用 → 確認 → 確定/破棄」の
安心なワークフローを実現する。

## 現状の挙動

適用は即時実行のみ。取り消しは Undo 頼み（機能はするが、適用前に結果を確認できない）。

## 変更内容（フェーズ）

### フェーズ1: プレビューペイン

- `MaterialPreviewPane.cs` を新設（`PreviewRenderUtility` のラッパー。Rect を受けて描画する部品）
- 球メッシュデフォルト + 任意メッシュ指定 ObjectField（アバターの顔メッシュで確認する実需に対応）
- Before/After は**トグル切替（1画面）**。`GUI.RepeatButton` で「押下中だけ Before」
- After 表示は `new Material(target) { hideFlags = HideFlags.HideAndDontSave }` に
  **本適用と同一の TemplateApplier を適用**して描画（コードパス分岐を作らない）

実装上の必須事項（`Documentation~/DESIGN.md` §5 の落とし穴に対応）:
- `PreviewRenderUtility` は遅延生成し `OnDisable` で必ず `Cleanup()`
- `farClipPlane = 20` 程度に設定（デフォルトでは何も映らない）
- `BeginPreview〜EndAndDrawPreview` は `EventType.Repaint` 時のみ実行
- プレビュー用複製マテリアルは選択変更・クローズ時に `DestroyImmediate`

### フェーズ2: 仮適用トランザクション（任意・フェーズ1の効果を見て判断）

- シーンビュー全体で After を確認したい場合のみ実装する
- 方式: 本体直接適用 + `HideAndDontSave` バックアップからの復元
  （restore は shader → `CopyPropertiesFromMaterial` → renderQueue → shaderKeywords の4点セット）
- `AssemblyReloadEvents.beforeAssemblyReload` で仮適用中の自動キャンセル必須
- 確定時は「巻き戻して正式再適用」方式で Undo 履歴を1操作に保つ

## テスト方針

実機確認。特にプレビューリーク（ウィンドウ開閉・ドメインリロード繰り返しでカメラ/RT が増えないこと）を
`Resources.FindObjectsOfTypeAll` で確認する。

## 完了条件

- フェーズ1 が実装され、プレビューと実適用結果が一致することを実機確認済み
- リークなしを確認済み
