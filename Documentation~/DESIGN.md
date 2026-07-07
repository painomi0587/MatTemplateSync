# MatTemplateSync 設計ドキュメント

2026-07-07 のマルチエージェント設計検討（3視点並列）の統合結果。
検討観点: (1) lilToon プロパティ体系 / (2) エディタ UI・プレビュー実装 / (3) 適用処理・Undo 安全性。
lilToon 側の事実は lilxyzw/lilToon の **1.7.0 / 1.8.0 タグの実ソース**
（`lts.shader`, `lilMaterialUtils.cs`, `lilShaderManager.cs`, `lilToonSetting.cs`）を取得して検証済み。

## 1. アーキテクチャ

```
Editor/
├── MatTemplateSyncWindow.cs   EditorWindow（IMGUI）。D&D 受付・カテゴリ UI・リスト描画
├── LilToonPropertyTable.cs    カテゴリ → プロパティ名集合の静的テーブル（データのみ）
├── TemplateApplier.cs         Apply(src, dst, mask)。Undo グループ化・ロールバック
└── LilToonDetector.cs         lilToon 判定・バリアント分類
```

**最重要の設計判断: 適用ロジックを「(template, target, mask) を受ける純関数」として分離する。**
フェーズ2の Before/After プレビューは「複製マテリアルに同じ関数を適用して描画する」だけで実現でき、
プレビューと本適用が完全に同一コードパスになるため「プレビューと結果が違う」事故を構造的に防げる。

- **UI は全編 IMGUI**。D&D（`DragAndDrop`）・サムネイル（`AssetPreview`）・プレビュー
  （`PreviewRenderUtility.EndAndDrawPreview`）はすべて IMGUI 世代の API であり、
  UIToolkit を選ぶと結局 `IMGUIContainer` ハイブリッドになるため
- **データ（プロパティテーブル）とロジック（Applier）の分離は必須**。lilToon のバージョンアップで
  プロパティが増減してもテーブル 1 ファイルの修正で済む
- 配布は「リポジトリルート = UPM パッケージ」構成（git URL 1 行でインストール可能）。
  普及したら VPM 化（GitHub Pages で index.json ホスト）を検討

## 2. プロパティコピー方式

**「名前テーブル + 実行時型解決」のハイブリッド方式**を採用。

- カテゴリテーブルは**プロパティ名の集合だけ**を持つ（lilToon 1.8.0 の lts.shader 実証済みリスト）
- 型はテンプレート側シェーダーの `Shader.GetPropertyType(FindPropertyIndex(name))` で実行時解決し、
  Color / Float / Range / Vector / Int を型別 API でコピー。Texture はここでも二重に除外される
- **テンプレートと対象の両側で存在チェック**（`FindPropertyIndex >= 0` && `target.HasProperty`）。
  存在しない名前は黙ってスキップ → バリアント差（無印/Cutout/Transparent/Outline）と
  バージョン差（1.x/2.x）を無害に吸収。テーブルは「知っている全バージョンの和集合」で持ってよい
- `Material.CopyPropertiesFromMaterial` は全プロパティ一括のためカテゴリ選択式には使えない

### lilToon 固有の検証済み事実

- **`_MainColor` は存在しない。メインカラーは `_Color`**（よくある誤記なので注意）
- **通常版 lilToon はシェーダーキーワードを一切使わない**（キーワード操作は lilToonMulti 専用の
  `SetupMultiMaterial` のみ）。トグル（`_UseShadow` 等）は float 分岐なので **`SetFloat` だけで反映される**。
  むしろ余計なキーワードを付けるとアニメーション時に問題を起こすため、キーワードには触らないのが正しい
- **アウトライン有無・レンダリングモードは「プロパティ」ではなく「シェーダー自体」が差し替わる**
  （`lilToon` ↔ `Hidden/lilToonOutline`、`Hidden/lilToonCutout` ↔ `Hidden/lilToonCutoutOutline` 等）。
  アウトライン系プロパティは非アウトライン版にも宣言されているためコピー自体は成功するが描画されない
  → v0.1 は UI で注意表示のみ。シェーダー差し替えは将来のオプトイン（差し替え時は renderQueue の退避→復元が必要）
- **lilToonMulti / Lite / Fur / Gem / FakeShadow はプロパティ集合・セットアップ要件が異なるため対象外**。
  Multi 対応するなら `lilMaterialUtils.SetupMultiMaterial` のリフレクション呼び出しが必要
- **シェーダー設定（`LIL_FEATURE_*`）の罠**: プロジェクト側でその機能がコンパイル除外されていると
  トグルを 0→1 にしても描画されない。lilToon は保存/ビルド時に自動有効化する
  （`lilToonSetting.ApplyShaderSettingOptimized`、internal）。v0.1 では対応せず、反映されない場合の
  案内をドキュメントに記載。将来はリフレクション呼び出し + 失敗時警告の併用を検討
- `_lilToonVersion`（int, 1.8.0 では 44）でテンプレート/対象のバージョン齟齬を警告可能（将来）

### 除外リスト（コピーしてはいけないもの）

| 分類 | 例 | 理由 |
|---|---|---|
| テクスチャ全般 | `_MainTex`, `_EmissionMap`, `_ShadowColorTex`, 各種 Mask | ツールの基本方針（個別維持） |
| レンダーステート | `_SrcBlend`, `_DstBlend`, `_ZWrite`, `_Cutoff`, `_AlphaToMask`, Stencil 系 | シェーダー差し替えなしでコピーすると透過・カットアウトが壊れる |
| UV アニメ | `_MainTex_ScrollRotate` 等 | マテリアル個別性が強い |
| エミッショングラデ内部値 | `_egc0`〜`_ega7` | `_EmissionGradTex` へのベイク元。色だけコピーしても反映されない |
| RenderQueue | `material.renderQueue` | v0.1 では触らない（将来「描画設定」カテゴリでオプトイン） |

### テクスチャコピーの将来方針（v0.2）

- カテゴリごとの「□ テクスチャも含める」オプトイン（デフォルト全 OFF）
- **MatCap が筆頭候補**（`_MatCapTex` = 質感の本体。共有素材を全身に配る運用が多い）
- コピーする場合は `SetTexture` に加えて `GetTextureScale/Offset` も一緒にコピー
- **テンプレート側が null のとき対象側の設定済みテクスチャを null で潰さない**（非 null のみコピー）

## 3. Undo・安全性設計

```
Undo.IncrementCurrentGroup();
Undo.SetCurrentGroupName("MatTemplateSync: Apply Template");
int group = Undo.GetCurrentGroup();
try {
    Undo.RecordObjects(targets, ...);   // 複数形を 1 回（性能・グループ一体性）
    ...適用...
} catch { Undo.RevertAllDownToGroup(group); throw; }  // 途中失敗は全ロールバック
finally { Undo.CollapseUndoOperations(group); }        // Ctrl+Z 1 回で全戻し
AssetDatabase.SaveAssets();                            // 末尾に 1 回だけ
```

- `SetCurrentGroupName` はメニュー表示名を決めるだけでグループ化を保証しない。
  `IncrementCurrentGroup → GetCurrentGroup → CollapseUndoOperations(group)` の括りが必須
- マテリアルアセットへの `Undo.RecordObjects` は有効（MaterialEditor 自身も内部で使用）。
  ただし Undo が戻すのはメモリ上の状態のみで、ディスクは次の保存まで変わらない（正常挙動）
- `AssetDatabase.StartAssetEditing/StopAssetEditing` は**使わない**
  （インポートのバッチ化 API であり本件無関係。例外時にエディタが固まるリスクだけ持ち込む）
- `SaveAssets` は毎マテリアル呼ばず末尾 1 回（100 個規模で数秒差が出る）
- 本ツールはマテリアルアセット自体を書き換えるため、シーン・プレハブへの副作用は
  「参照レンダラーの見た目が変わる」という意図どおりのもののみ。プレハブ override も発生しない
- コード規約: `renderer.material`（非 shared）へのアクセス禁止（インスタンスリークの温床）

## 4. UI 実装の要点

- **D&D**: `DragUpdated` で必ず `DragAndDrop.visualMode` を設定（しないとドロップ不能に見える）。
  `Event.current.Use()` を忘れない
- **lilToon 判定**: シェーダー名の `lilToon` 部分一致（大文字小文字無視）。完全一致だと
  `Hidden/lilToonCutout` 等の実運用マテリアルの大半を弾いてしまう
- **サムネイル**: `AssetPreview.GetAssetPreview` が null の間は `GetMiniThumbnail` でフォールバックし
  `Repaint()` でポーリング。**`SetPreviewTextureCacheSize` を件数 + 余裕に設定**
  （デフォルトキャッシュ超過で「永遠にロード中」のちらつきが起きる）
- **状態永続化**: `[SerializeField]` フィールドでドメインリロード対応 + カテゴリマスクのみ EditorPrefs
- テンプレート自身が対象リストに混入するのはよくある事故 → 追加時に自動除外

## 5. フェーズ2（プレビュー）設計メモ

- `PreviewRenderUtility` は遅延生成し、**`OnDisable` で必ず `Cleanup()`**（カメラ/RT リーク防止）
- `farClipPlane` デフォルトが近く「何も映らない」定番トラブル → 20 程度に設定
- `BeginPreview〜EndAndDrawPreview` は **`EventType.Repaint` 時のみ**実行
- Before/After は**トグル切替（1 画面）**を推奨（`GUI.RepeatButton` で「押下中だけ Before」）
- After 表示は `new Material(target) { hideFlags = HideFlags.HideAndDontSave }` に
  同じ Applier を適用して描画。**アセット本体には確定ボタン経由でのみ書き込む**
- メッシュは球デフォルト + 任意メッシュ指定 ObjectField（アバターの顔メッシュで確認したい実需がある）
- 仮適用トランザクションを本体直接適用方式にする場合は、`HideAndDontSave` バックアップからの
  復元（shader → CopyPropertiesFromMaterial → renderQueue → shaderKeywords の 4 点セット）と
  `AssemblyReloadEvents.beforeAssemblyReload` での自動キャンセルが必須

## 6. テスト戦略（将来）

- 「何をコピーするか」の決定（カテゴリ照合・積集合・除外適用）を純データ変換として切り出せば
  Material 不要の NUnit テストが書ける
- lilToon 非インストール CI では、lilToon の代表プロパティ名を再現したダミー .shader
  （プロパティ集合の異なる 2 枚）で Scanner/Executor の統合テストが可能
- lilToon 実物との統合テストは `[Category("RequiresLilToon")]` でゲートした専用ジョブに分離

## 7. 要実機確認事項（Unity 2022 + lilToon 1.8 実機で確認する）

1. シェーダー設定自動走査（`ApplyShaderSettingOptimized`）の発火タイミングと、
   トグル 0→1 直後に手動リフレッシュが必要か
2. アウトライン版シェーダー差し替え時（v0.2 オプトイン実装時）の透過モードでの表示
3. Material Variant 使用時に `SetFloat` でオーバーライドが増える点の UX
4. lilToon 2.x（現行 v2.3.4）のプロパティ改名・削除の全量調査（2.x 正式対応を謳う場合）
