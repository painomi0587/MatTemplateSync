# Process Feedback

開発プロセスの振り返りと改善を記録する。

## 記録方法

タスク完了時や問題発生時に、以下のいずれかのセクションに追記する。

## オーナーフィードバック

## 問題の記録

### 2026-07-07: リモート実行環境から MatTemplateSync への push が 403
- git relay（`http://local_proxy@127.0.0.1:<port>/git/...`）・GitHub API（MCP）とも書き込みが 403
  （`Resource not accessible by integration` — セッションの GitHub 統合に write 権限がない）
- fetch/read は通るため、リポジトリの読み取り専用スコープが原因
- **対処**: オーナーが GitHub の Claude アプリ設定で painomi0587/MatTemplateSync への
  write 権限を付与する。付与後にエージェントが再 push する

### 2026-07-07: この環境では Unity コンパイル・動作検証が不可
- コンテナに Unity Editor が無く、C# のコンパイル確認すらできない
- **対処**: コードレビューエージェントで Unity API の存在・シグネチャを重点確認し、
  実機検証は Plan 0001 としてオーナーの Unity プロジェクトで実施する運用とする

## 改善アクション

## 完了済み
