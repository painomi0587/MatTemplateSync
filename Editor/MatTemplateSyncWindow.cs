using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MatTemplateSync
{
    /// <summary>
    /// MatTemplateSync のメインウィンドウ（v0.1: D&amp;D + カテゴリ選択 + 即時一括適用）。
    /// プレビュー（Before/After）はフェーズ2で追加予定。
    /// </summary>
    public class MatTemplateSyncWindow : EditorWindow
    {
        private const string CategoryMaskPrefKey = "MatTemplateSync.CategoryMask";
        private const float ThumbnailSize = 32f;

        // [SerializeField] によりドメインリロード（再コンパイル/Play）を跨いで自動復元される
        [SerializeField] private Material _template;
        [SerializeField] private List<Material> _targets = new List<Material>();
        [SerializeField] private GameObject _sourceObject;
        [SerializeField] private int _categoryMask = (int)(
            SyncCategory.Shadow | SyncCategory.RimLight | SyncCategory.Lighting);
        [SerializeField] private Vector2 _scroll;

        private string _lastMessage;
        private MessageType _lastMessageType = MessageType.Info;

        // ソースオブジェクト由来として追跡中のマテリアル（非シリアライズ・実行時キャッシュ）
        private HashSet<Material> _trackedMaterials = new HashSet<Material>();

        [MenuItem("Tools/MatTemplateSync")]
        public static void Open()
        {
            var window = GetWindow<MatTemplateSyncWindow>("MatTemplateSync");
            window.minSize = new Vector2(380f, 480f);
        }

        private void OnEnable()
        {
            _categoryMask = EditorPrefs.GetInt(CategoryMaskPrefKey, _categoryMask);
            Undo.undoRedoPerformed += OnUndoRedo;
            // ドメインリロード後にソースオブジェクトが復元されていれば初期同期
            if (_sourceObject != null) SyncFromSourceObject();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (_sourceObject != null) { SyncFromSourceObject(); Repaint(); }
        }

        private void OnInspectorUpdate()
        {
            if (_sourceObject == null) return;
            var current = CollectMaterialsFromObject(_sourceObject);
            if (!current.SetEquals(_trackedMaterials))
            {
                SyncFromSourceObject(current);
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTemplateSection();
            EditorGUILayout.Space();
            DrawCategorySection();
            EditorGUILayout.Space();
            DrawTargetSection();
            EditorGUILayout.EndScrollView();

            DrawApplySection();
        }

        // ---- テンプレート ----

        private void DrawTemplateSection()
        {
            EditorGUILayout.LabelField("テンプレートマテリアル", EditorStyles.boldLabel);
            var picked = (Material)EditorGUILayout.ObjectField(
                _template, typeof(Material), allowSceneObjects: false);
            if (picked != _template)
            {
                if (picked == null)
                {
                    _template = null;
                }
                else if (LilToonDetector.IsSupported(picked, out string reason))
                {
                    _template = picked;
                    _targets.Remove(picked); // テンプレート自身が対象に混入していたら除外
                    _lastMessage = null;
                }
                else
                {
                    ShowMessage($"{picked.name}: {reason}", MessageType.Warning);
                }
            }

            if (_template != null)
            {
                EditorGUILayout.LabelField(
                    $"バリアント: {LilToonDetector.GetVariantLabel(_template)}", EditorStyles.miniLabel);
            }
        }

        // ---- カテゴリ選択 ----

        private void DrawCategorySection()
        {
            EditorGUILayout.LabelField("反映するカテゴリ", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて ON", EditorStyles.miniButtonLeft))
                {
                    foreach (var info in LilToonPropertyTable.Categories)
                        _categoryMask |= (int)info.Category;
                    SaveCategoryMask();
                }
                if (GUILayout.Button("すべて OFF", EditorStyles.miniButtonRight))
                {
                    _categoryMask = 0;
                    SaveCategoryMask();
                }
            }

            var categories = LilToonPropertyTable.Categories;
            const int columns = 2;
            for (int i = 0; i < categories.Length; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < columns && i + c < categories.Length; c++)
                    {
                        var info = categories[i + c];
                        bool on = ((int)info.Category & _categoryMask) != 0;
                        int count = LilToonPropertyTable.CountProperties(info.Category);
                        bool next = EditorGUILayout.ToggleLeft(
                            $"{info.Label} ({count})", on, GUILayout.MinWidth(150f));
                        if (next != on)
                        {
                            _categoryMask = next
                                ? _categoryMask | (int)info.Category
                                : _categoryMask & ~(int)info.Category;
                            SaveCategoryMask();
                        }
                    }
                }
            }

            var mask = (SyncCategory)_categoryMask;
            if ((mask & SyncCategory.Outline) != 0)
            {
                EditorGUILayout.HelpBox(
                    "アウトラインは値をコピーしても、対象がアウトライン版シェーダー" +
                    "（例: lilToonOutline）でなければ描画には反映されません。",
                    MessageType.Info);
            }
            if ((mask & SyncCategory.MatCap) != 0)
            {
                EditorGUILayout.HelpBox(
                    "MatCap はテクスチャ（_MatCapTex）を含むすべての設定がコピーされます。",
                    MessageType.Info);
            }
        }

        private void SaveCategoryMask()
        {
            EditorPrefs.SetInt(CategoryMaskPrefKey, _categoryMask);
        }

        // ---- 対象マテリアル ----

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField($"対象マテリアル ({_targets.Count})", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("選択中から追加"))
                {
                    AddTargets(Selection.objects.OfType<Material>());
                }
                if (GUILayout.Button("全クリア"))
                {
                    _targets.Clear();
                }
            }

            DrawObjectPickerSection();
            DrawDropArea();
            DrawTargetList();
        }

        private void DrawObjectPickerSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var picked = (GameObject)EditorGUILayout.ObjectField(
                    _sourceObject, typeof(GameObject), allowSceneObjects: true);
                if (picked != _sourceObject)
                {
                    _sourceObject = picked;
                    _trackedMaterials.Clear();
                    SyncFromSourceObject();
                }
                using (new EditorGUI.DisabledScope(_sourceObject == null))
                {
                    if (GUILayout.Button("再同期", GUILayout.Width(60f)))
                    {
                        _trackedMaterials.Clear();
                        SyncFromSourceObject();
                    }
                }
            }
            if (_sourceObject != null)
            {
                EditorGUILayout.LabelField(
                    "オブジェクトのマテリアルを自動追跡中", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private static HashSet<Material> CollectMaterialsFromObject(GameObject go)
        {
            return new HashSet<Material>(
                go.GetComponentsInChildren<Renderer>(includeInactive: true)
                  .SelectMany(r => r.sharedMaterials)
                  .Where(m => m != null));
        }

        private void AddMaterialsFromGameObject(GameObject go)
        {
            if (go == null) return;
            AddTargets(CollectMaterialsFromObject(go));
        }

        /// <summary>
        /// ソースオブジェクト由来のターゲットだけを差分更新する。
        /// 手動追加したマテリアルはそのまま維持する。
        /// </summary>
        private void SyncFromSourceObject(HashSet<Material> current = null)
        {
            if (_sourceObject == null) { _trackedMaterials.Clear(); return; }

            current ??= CollectMaterialsFromObject(_sourceObject);

            // 前回追跡していたがオブジェクトから消えたものをターゲットから除去
            _targets.RemoveAll(m => _trackedMaterials.Contains(m) && !current.Contains(m));

            // 新たに追加されたものをターゲットに追加（lilToon チェック付き）
            foreach (Material m in current)
            {
                if (m == null || m == _template || _targets.Contains(m)) continue;
                if (!LilToonDetector.IsSupported(m, out _)) continue;
                _targets.Add(m);
            }

            _trackedMaterials = current;
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(64, _targets.Count + 32));
        }

        private void DrawDropArea()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "ここにマテリアルまたはオブジェクトをドラッグ&ドロップ", EditorStyles.helpBox);

            Event evt = Event.current;
            if ((evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                || !rect.Contains(evt.mousePosition))
            {
                return;
            }

            var materials = DragAndDrop.objectReferences.OfType<Material>().ToList();
            var gameObjects = DragAndDrop.objectReferences.OfType<GameObject>().ToList();

            IEnumerable<Material> candidates = materials.Concat(
                gameObjects.SelectMany(go =>
                    go.GetComponentsInChildren<Renderer>(includeInactive: true)
                      .SelectMany(r => r.sharedMaterials)
                      .Where(m => m != null))
            ).Distinct();

            bool anyValid = candidates.Any(m => LilToonDetector.IsSupported(m, out _));
            // visualMode を設定しないとカーソルが「禁止」のままドロップできない
            DragAndDrop.visualMode = anyValid
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform)
            {
                // 拒否時は Use() せずイベントを伝播させる（AcceptDrag なしの消費は
                // ドラッグ元のカーソル/結果フィードバックを不整合にすることがある）
                if (!anyValid) return;
                DragAndDrop.AcceptDrag();
                AddTargets(candidates);
            }
            evt.Use();
        }

        private void AddTargets(IEnumerable<Material> materials)
        {
            int added = 0;
            int rejected = 0;
            string firstReason = null;

            foreach (Material material in materials)
            {
                if (material == null || material == _template || _targets.Contains(material)) continue;
                if (!LilToonDetector.IsSupported(material, out string reason))
                {
                    rejected++;
                    firstReason ??= $"{material.name}: {reason}";
                    continue;
                }
                _targets.Add(material);
                added++;
            }

            // サムネイルキャッシュが件数より小さいと取得済みプレビューが追い出されて
            // 「永遠にロード中」のちらつきになるため、必ず件数に追随させる
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(64, _targets.Count + 32));

            if (rejected > 0)
            {
                ShowMessage($"{rejected} 件をスキップしました（{firstReason}）", MessageType.Warning);
            }
            else if (added > 0)
            {
                _lastMessage = null;
            }
        }

        private void DrawTargetList()
        {
            _targets.RemoveAll(m => m == null); // 破棄済みアセットの掃除

            bool anyLoading = false;
            // GUI 描画中のリスト変更は Layout/Repaint 間の件数不一致を招くため削除は遅延実行する
            int pendingRemove = -1;

            for (int i = 0; i < _targets.Count; i++)
            {
                Material material = _targets[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D thumbnail = AssetPreview.GetAssetPreview(material);
                    if (thumbnail == null)
                    {
                        thumbnail = AssetPreview.GetMiniThumbnail(material);
                        // 生成失敗が確定したアセットで Repaint が無限ループしないよう
                        // 「まだロード中」の場合のみ再描画を要求する
                        if (AssetPreview.IsLoadingAssetPreview(material.GetInstanceID()))
                        {
                            anyLoading = true;
                        }
                    }
                    GUILayout.Label(thumbnail,
                        GUILayout.Width(ThumbnailSize), GUILayout.Height(ThumbnailSize));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        if (GUILayout.Button(material.name, EditorStyles.linkLabel))
                        {
                            EditorGUIUtility.PingObject(material);
                        }
                        string variant = LilToonDetector.GetVariantLabel(material);
                        bool shaderMismatch = _template != null && material.shader != _template.shader;
                        EditorGUILayout.LabelField(
                            shaderMismatch ? $"{variant} ⚠ テンプレートとバリアントが異なります" : variant,
                            EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("×", GUILayout.Width(22f)))
                    {
                        pendingRemove = i;
                    }
                }
            }

            if (pendingRemove >= 0)
            {
                _targets.RemoveAt(pendingRemove);
                Repaint();
            }

            if (anyLoading)
            {
                Repaint(); // サムネイル非同期ロード中はポーリング再描画
            }
        }

        // ---- 適用 ----

        private void DrawApplySection()
        {
            if (!string.IsNullOrEmpty(_lastMessage))
            {
                EditorGUILayout.HelpBox(_lastMessage, _lastMessageType);
            }

            bool canApply = _template != null && _targets.Count > 0 && _categoryMask != 0;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"適用 ({_targets.Count})", GUILayout.Height(32f)))
                    {
                        Apply();
                    }
                    if (GUILayout.Button($"コピーして適用 ({_targets.Count})", GUILayout.Height(32f)))
                    {
                        CopyAndApply();
                    }
                }
            }
            if (!canApply)
            {
                EditorGUILayout.LabelField(
                    "テンプレート・対象マテリアル・カテゴリをすべて指定してください。",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void Apply()
        {
            try
            {
                ApplyReport report = TemplateApplier.ApplyToMaterials(
                    _template, _targets, (SyncCategory)_categoryMask);
                ShowMessage(
                    $"{report.MaterialCount} マテリアルに適用しました" +
                    $"（プロパティ {report.AppliedProperties} 件、スキップ {report.SkippedProperties} 件）。" +
                    "Ctrl+Z で一括で元に戻せます。",
                    MessageType.Info);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MatTemplateSync] 適用に失敗したため全対象をロールバックしました: {e}");
                ShowMessage($"適用に失敗しました（全対象をロールバック済み）: {e.Message}", MessageType.Error);
            }
        }

        private void CopyAndApply()
        {
            try
            {
                ApplyReport report = TemplateApplier.CopyAndApplyToMaterials(
                    _template, _targets, (SyncCategory)_categoryMask, out var copyMap);

                if (_sourceObject != null && copyMap.Count > 0)
                    ReplaceOnSourceObject(copyMap);

                string msg = $"{report.MaterialCount} マテリアルをコピーして適用しました（_synced サフィックスで保存）。";
                if (_sourceObject != null && copyMap.Count > 0)
                    msg += " オブジェクトのマテリアルスロットを差し替えました。";
                if (report.SkippedMaterials > 0)
                    msg += $" {report.SkippedMaterials} 件はコピーできませんでした。";
                ShowMessage(msg, report.SkippedMaterials > 0 ? MessageType.Warning : MessageType.Info);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MatTemplateSync] コピー適用に失敗しました: {e}");
                ShowMessage($"コピー適用に失敗しました: {e.Message}", MessageType.Error);
            }
        }

        private void ReplaceOnSourceObject(Dictionary<Material, Material> copyMap)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("MatTemplateSync: Replace Materials");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Renderer r in _sourceObject.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && copyMap.TryGetValue(mats[i], out Material copy))
                    {
                        mats[i] = copy;
                        changed = true;
                    }
                }
                if (!changed) continue;
                Undo.RecordObject(r, "MatTemplateSync: Replace Materials");
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }

            Undo.CollapseUndoOperations(undoGroup);

            // 追跡キャッシュをコピー後の状態に更新
            _trackedMaterials = CollectMaterialsFromObject(_sourceObject);
        }

        private void ShowMessage(string message, MessageType type)
        {
            _lastMessage = message;
            _lastMessageType = type;
        }
    }
}
