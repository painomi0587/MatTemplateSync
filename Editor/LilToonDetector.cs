using System;
using UnityEngine;

namespace MatTemplateSync
{
    /// <summary>
    /// lilToon マテリアルの判定とバリアント分類。
    /// lilToon はアウトライン有無・透過モードで "Hidden/lilToonCutoutOutline" のように
    /// シェーダー自体が差し替わるため、名前の部分一致で判定する。
    /// </summary>
    public static class LilToonDetector
    {
        // プロパティ集合が通常版と大きく異なる、または独自のセットアップ処理
        // （キーワード整備等）が必要なため v0.1 では適用対象外とする系統。
        private static readonly string[] UnsupportedTokens =
        {
            "Multi",      // _lil/lilToonMulti: Unity 予約キーワードの整備が別途必要
            "Lite",       // Hidden/lilToonLite*: プロパティ集合が大幅に異なる
            "Fur",        // ファー系
            "Gem",        // 宝石系
            "FakeShadow", // 疑似影
            "Refraction", // 屈折系
        };

        public static bool IsLilToon(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name.IndexOf("liltoon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>テンプレート/適用対象として扱える lilToon 系マテリアルかを判定する。</summary>
        public static bool IsSupported(Material material, out string reason)
        {
            if (!IsLilToon(material))
            {
                reason = "lilToon 系シェーダーではありません";
                return false;
            }

            string shaderName = material.shader.name;
            foreach (string token in UnsupportedTokens)
            {
                if (shaderName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reason = $"lilToon {token} 系は v0.1 では未対応です";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>UI 表示用のバリアント名（"Hidden/" プレフィックスを除いたシェーダー名）。</summary>
        public static string GetVariantLabel(Material material)
        {
            if (material == null || material.shader == null) return "(なし)";
            string name = material.shader.name;
            const string hiddenPrefix = "Hidden/";
            return name.StartsWith(hiddenPrefix, StringComparison.Ordinal)
                ? name.Substring(hiddenPrefix.Length)
                : name;
        }
    }
}
