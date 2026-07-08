# Plan 0004: Undo トランザクション統合とテクスチャ null 潰し防止

## 実装状況: 未着手

> 出典: 2026-07-08 マルチエージェントリファクタリング検討（リファクタリング分析エージェントの候補1・2・6）

## 関連 Plan

- [Plan 0001: v0.1 実機検証と修正](0001-v01-verification.md) — 本 Plan の検証項目（途中失敗巻き戻し・MatCap 未使用テンプレート適用）を実機確認リストに追加する
- [Plan 0002: v0.2 ユーザビリティ改善](0002-v02-usability.md) — 項目1（テクスチャオプトイン）の前提となる null 非コピー原則を先行適用する

## 目的

「途中失敗」「null テンプレート値」という実機で普通に起きる条件でユーザーデータが壊れる穴を塞ぐ。
v0.2 着手前に必須の堅牢化。

## 現状の挙動

1. **CopyAndApply の例外時ロールバック欠落**: `MatTemplateSyncWindow.CopyAndApply()`（452-479行）が
   Undo グループを開くが、catch 節（474-478行）は `Undo.RevertAllDownToGroup(undoGroup)` を呼ばない。
   `TemplateApplier.CopyAndApplyToMaterials` のループ途中で例外が出ると、作成済みの `_synced.mat` が
   ディスクに残り、Undo グループも collapse されず開きっぱなしになる。`ApplyToMaterials`
   （`TemplateApplier.cs:60-65` で全ロールバック）と挙動が非対称。
   さらに Undo トランザクションの所有権が分裂している: グループ管理は Window 側、
   `RegisterCreatedObjectUndo`（`TemplateApplier.cs:113`）と `SaveAssets/Refresh`（121-122行）は Applier 側。
2. **テクスチャの null 潰し**: `TemplateApplier.cs:162-164` の `Texture` ケースが
   `target.SetTexture(name, template.GetTexture(name))` を無条件実行。v0.1 のテーブルに既に
   `_MatCapTex` / `_MatCap2ndTex` / `_MatCapBumpMap` / `_MatCap2ndBumpMap` が載っているため
   （`LilToonPropertyTable.cs:154,159,166`）、テンプレートが MatCap 2nd 未使用なら対象の設定済み
   テクスチャが null で消える。DESIGN §2 の「非 null のみコピー」規則の現行違反。
   `GetTextureScale/Offset` の同時コピーも未実装。
3. **小粒の非対称・非効率**（候補6）:
   - `TemplateApplier.ApplyToMaterials`（36-44行）に `template` の null ガードがない
   - null ターゲットが `SkippedMaterials` に計上されず黙って抜ける（51行。`CopyAndApplyToMaterials` 88行と非対称）
   - `MatTemplateSyncWindow.DrawDropArea` の `candidates` が遅延 LINQ のまま二重列挙され
     `GetComponentsInChildren` が2回走る（284-303行）
   - `OnInspectorUpdate`（57-66行）が毎回 `HashSet` を新規構築

## 変更内容（項目）

### 項目1: Undo トランザクションの Applier への集約

- **対象**: `Editor/TemplateApplier.cs` / `Editor/MatTemplateSyncWindow.cs`
- `CopyAndApplyToMaterials` にグループ開始〜collapse〜例外時 revert を内包させる
  （`ApplyToMaterials` と同一パターン）
- レンダラー差し替え（`ReplaceOnSourceObject`）は戻り値のマッピングを使った同一グループ内呼び出し、
  または `Action<Dictionary<Material,Material>>` コールバックとして設計を1箇所に集約する
- Undo グループの括りという不変条件の担い手を `TemplateApplier` 1ファイルにする

### 項目2: テクスチャコピーの null ガードと Scale/Offset 同時コピー

- **対象**: `Editor/TemplateApplier.cs`
- `Texture` ケースを「template 側が非 null のときのみ `SetTexture` + `SetTextureScale/Offset`、
  null ならスキップ計上」に変更する
- **対象**: `CLAUDE.repo.md` — 不変条件「テクスチャ…はコピーしない」を
  「テーブルに明示的にオプトインされたテクスチャ（MatCap 等）を除き」と追記修正する
  （テーブルの意図的な MatCap 搭載と字面上矛盾しており、将来のエージェントが誤修正する温床のため）

### 項目3: ガード節・レポート計上・微細効率の統一

- **対象**: `Editor/TemplateApplier.cs` / `Editor/MatTemplateSyncWindow.cs`
- `ApplyToMaterials` に `ArgumentNullException` ガードを追加
- null ターゲットのスキップ計上を `CopyAndApplyToMaterials` と統一
- `DrawDropArea` の `candidates` を `.ToList()` で具現化
- `OnInspectorUpdate` のポーリングを間引き（または `EditorApplication.hierarchyChanged` 駆動を検討）

## 影響範囲

- `Editor/TemplateApplier.cs` / `Editor/MatTemplateSyncWindow.cs` / `CLAUDE.repo.md`
- 公開 API（`CopyAndApplyToMaterials` のシグネチャ）が変わる可能性がある（現状呼び出し元は Window のみ）

## テスト方針

- この環境では Unity 実機不可のため、addf-code-review-agent で Unity API シグネチャを重点確認する
- Plan 0001 の実機確認リストに以下を追加する:
  - コピー適用の途中失敗時に `_synced.mat` の孤児が残らないこと
  - MatCap 未使用テンプレートからの適用で対象の設定済み MatCap が消えないこと
  - `RegisterCreatedObjectUndo` の Undo がディスク上のアセットファイル自体を消すか（コメントの主張の裏取り）

## 破壊的変更の許容範囲

`TemplateApplier.CopyAndApplyToMaterials` のシグネチャ変更は許容（外部利用者なし）。

## 要オーナー確認

なし

## 完了条件

- [ ] `CopyAndApplyToMaterials` 途中失敗時に Undo revert が実行される構造になっている
- [ ] テクスチャコピーが非 null 限定 + Scale/Offset 同時コピーになっている
- [ ] addf-code-review-agent のレビューで Critical/High 指摘なし
- [ ] Plan 0001 に実機確認項目が追記されている <!-- human-judgment -->

## AI 実装時間見積もり

1セッション以内
