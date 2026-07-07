using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MatTemplateSync
{
    public struct ApplyReport
    {
        public int MaterialCount;
        public int AppliedProperties;
        public int SkippedProperties;
    }

    /// <summary>
    /// テンプレートマテリアルから対象マテリアル群へのプロパティコピー。
    ///
    /// 型はテーブルに持たせず、テンプレート側シェーダーの Shader.GetPropertyType で
    /// 実行時解決する。テクスチャコピーはテーブルに名前が載っているもの（MatCap テクスチャ等）
    /// のみ実行される。テーブルに載っていない名前はそもそも走査されないため安全。
    ///
    /// プレビューと本適用を同一コードパスにするため、単体適用（ApplyToMaterial）は
    /// Undo や保存に関知しない純粋な変換として実装する。
    /// </summary>
    public static class TemplateApplier
    {
        /// <summary>
        /// 複数マテリアルへの一括適用。Ctrl+Z 1回で全対象が元に戻るよう
        /// 1 Undo グループにまとめ、途中で例外が出た場合は全ロールバックする。
        /// </summary>
        public static ApplyReport ApplyToMaterials(
            Material template, IReadOnlyList<Material> targets, SyncCategory mask)
        {
            List<string> propertyNames = LilToonPropertyTable.CollectProperties(mask);
            var report = new ApplyReport();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("MatTemplateSync: Apply Template");
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                Object[] recordTargets = targets.Where(t => t != null).Cast<Object>().ToArray();
                Undo.RecordObjects(recordTargets, "MatTemplateSync: Apply Template");
                foreach (Material target in targets)
                {
                    if (target == null) continue;
                    ApplyToMaterial(template, target, propertyNames, ref report);
                    EditorUtility.SetDirty(target);
                    report.MaterialCount++;
                }
                // 成功時のみ collapse する（Revert で消費済みのグループへの collapse は
                // 意図しないグループ統合を招き得るため、例外パスでは呼ばない）
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception)
            {
                // 途中まで適用済みの対象を全て巻き戻し、中途半端な状態を残さない
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>
        /// 単体マテリアルへの適用。テンプレート・対象の両方にプロパティが存在する
        /// 場合のみコピーする（lilToon のバージョン差・バリアント差を無害に吸収する）。
        /// </summary>
        public static void ApplyToMaterial(
            Material template, Material target, IReadOnlyList<string> propertyNames, ref ApplyReport report)
        {
            Shader templateShader = template.shader;
            foreach (string name in propertyNames)
            {
                int index = templateShader.FindPropertyIndex(name);
                if (index < 0 || !target.HasProperty(name))
                {
                    report.SkippedProperties++;
                    continue;
                }

                switch (templateShader.GetPropertyType(index))
                {
                    case ShaderPropertyType.Color:
                        target.SetColor(name, template.GetColor(name));
                        report.AppliedProperties++;
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        target.SetFloat(name, template.GetFloat(name));
                        report.AppliedProperties++;
                        break;
                    case ShaderPropertyType.Vector:
                        target.SetVector(name, template.GetVector(name));
                        report.AppliedProperties++;
                        break;
                    case ShaderPropertyType.Int:
                        target.SetInteger(name, template.GetInteger(name));
                        report.AppliedProperties++;
                        break;
                    case ShaderPropertyType.Texture:
                        target.SetTexture(name, template.GetTexture(name));
                        report.AppliedProperties++;
                        break;
                    default:
                        report.SkippedProperties++;
                        break;
                }
            }
        }
    }
}
