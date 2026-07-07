# CLAUDE.repo.md

MatTemplateSync — Unity 2022 + lilToon 向けマテリアルテンプレート一括適用ツール（UPM パッケージ）。
lilToon マテリアルを1つテンプレートとして選び、カテゴリ選択したプロパティ（色調・数値系のみ、
テクスチャは個別維持）を複数マテリアルへ一括適用するエディタ拡張。

# プロジェクト種別

このリポジトリは **ADDF 利用プロジェクト** です。

# プロジェクト構成

- リポジトリルート = UPM パッケージルート（`package.json` / `Editor/`）。git URL でインストール可能
- `Editor/` — エディタ拡張本体。asmdef（`MatTemplateSync.Editor`）は Editor プラットフォーム限定
  - `MatTemplateSyncWindow.cs` — EditorWindow（IMGUI）。D&D・カテゴリUI・リスト描画
  - `LilToonPropertyTable.cs` — カテゴリ → プロパティ名集合の静的テーブル（データのみ）
  - `TemplateApplier.cs` — 適用ロジック。Undo グループ化・ロールバック
  - `LilToonDetector.cs` — lilToon 判定・バリアント分類
- `Documentation~/DESIGN.md` — 設計ドキュメント（マルチエージェント検討の統合結果・落とし穴一覧）
- **Unity にインポートされるファイル（ルート直下の .md、Editor/ 配下等）には `.meta` を必ず同梱する**
  （dot 始まりのファイル・ディレクトリと `~` 終わりのディレクトリは Unity が無視するため不要）

## 設計上の不変条件

- 適用ロジックは「(template, target, mask) を受ける純関数」に保つ（プレビューと本適用を同一コードパスにするため）
- テクスチャ・レンダーステート系（`_SrcBlend`/`_ZWrite`/`_Cutoff`/Stencil/RenderQueue）はコピーしない
- シェーダー自体（レンダリングモード・アウトライン有無）は変更しない
- `renderer.material`（非 shared）へのアクセス禁止（マテリアルインスタンスのリーク源）
- プロパティコピーは template/target 両側の存在チェック必須（バリアント・バージョン差の吸収）

## ビルド・テスト

- このリポジトリ単体ではビルド・テスト不可（Unity Editor 2022.3 + lilToon 1.7〜1.8 実機が必要）
- CLI でのコンパイル検証は未整備のため、C# 変更時は Unity API の存在・シグネチャを
  コードレビュー（addf-code-review-agent）で重点確認する
- 将来: ダミーシェーダーによる EditMode テスト（`Documentation~/DESIGN.md` §6 参照）

## コミットログ規約

日本語で書く。形式:

```
[領域] 変更内容の要約

詳細説明（必要な場合）
```

領域例: `editor` / `docs` / `addf` / `release`
