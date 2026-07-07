---
name: unity-editor-sharedmaterials-sync
description: Unity エディタ拡張で Renderer の sharedMaterials 変化を検出・差し替えする実装パターン
metadata:
  type: project
---

## OnInspectorUpdate で sharedMaterials の変化を検出する

`EditorWindow.OnInspectorUpdate` は ~10fps で呼ばれる。polling に適している。

```csharp
private HashSet<Material> _trackedMaterials = new HashSet<Material>();

private void OnInspectorUpdate()
{
    if (_sourceObject == null) return;
    var current = CollectMaterials(_sourceObject);
    if (!current.SetEquals(_trackedMaterials))
    {
        SyncTargets(current);
        Repaint();
    }
}

private static HashSet<Material> CollectMaterials(GameObject go) =>
    new HashSet<Material>(
        go.GetComponentsInChildren<Renderer>(includeInactive: true)
          .SelectMany(r => r.sharedMaterials)
          .Where(m => m != null));
```

**Why:** `hierarchyChanged` はマテリアルスロット変更では発火しない。Undo/Redo は `Undo.undoRedoPerformed` でカバーする。

## sharedMaterials を差し替えるときの Undo 順序

`Undo.RecordObject` は **変更前** に呼ぶ。`sharedMaterials` のゲッターはコピーを返すため、ローカル配列を編集してからアサインする。

```csharp
Material[] mats = renderer.sharedMaterials; // コピーが返る
for (int i = 0; i < mats.Length; i++)
    if (copyMap.TryGetValue(mats[i], out var copy)) mats[i] = copy;

Undo.RecordObject(renderer, "Replace Materials"); // 変更前に記録
renderer.sharedMaterials = mats;                  // 変更
EditorUtility.SetDirty(renderer);
```

## ドメインリロード後の復元

`[SerializeField]` フィールドはドメインリロードを跨いで復元される。`OnEnable` で `_trackedMaterials` (非シリアライズ) を再構築すること。

```csharp
private void OnEnable()
{
    if (_sourceObject != null) SyncFromSourceObject();
}
```
