using System.Collections.Generic;

namespace MatTemplateSync
{
    [System.Flags]
    public enum SyncCategory
    {
        None         = 0,
        MainColor    = 1 << 0,
        Shadow       = 1 << 1,
        RimLight     = 1 << 2,
        RimShade     = 1 << 3,
        Backlight    = 1 << 4,
        Outline      = 1 << 5,
        Emission     = 1 << 6,
        Lighting     = 1 << 7,
        Reflection   = 1 << 8,
        MatCap       = 1 << 9,
        Glitter      = 1 << 10,
        DistanceFade = 1 << 11,
    }

    /// <summary>
    /// カテゴリ → lilToon プロパティ名の対応表。
    ///
    /// プロパティ名は lilToon 1.8.0 の lts.shader から採取した実在名。
    /// 型（Color/Float/Vector）はここでは持たず、適用時にテンプレート側シェーダーの
    /// Shader.GetPropertyType で実行時解決する（バージョン間の型変更に耐えるため）。
    /// テーブルは「知っている全バージョンの和集合」で持ってよい —
    /// 存在しない名前は適用時の HasProperty ガードで無害にスキップされる。
    ///
    /// テクスチャはカテゴリ単位でオプトイン方式を取る。
    /// MatCap テクスチャ（_MatCapTex / _MatCap2ndTex）はスタイルの本体のため含める。
    /// メインテクスチャ・各種マスク等、個別マテリアルに依存するテクスチャは含めない。
    /// レンダーステート系（_SrcBlend/_DstBlend/_ZWrite/_Cutoff/Stencil 等）は
    /// シェーダー差し替えなしでコピーすると描画が壊れるため対象外。
    /// </summary>
    public static class LilToonPropertyTable
    {
        public readonly struct CategoryInfo
        {
            public readonly SyncCategory Category;
            public readonly string Label;

            public CategoryInfo(SyncCategory category, string label)
            {
                Category = category;
                Label = label;
            }
        }

        public static readonly CategoryInfo[] Categories =
        {
            new CategoryInfo(SyncCategory.MainColor,    "メインカラー"),
            new CategoryInfo(SyncCategory.Shadow,       "影"),
            new CategoryInfo(SyncCategory.RimLight,     "リムライト"),
            new CategoryInfo(SyncCategory.RimShade,     "リムシェード"),
            new CategoryInfo(SyncCategory.Backlight,    "逆光ライト"),
            new CategoryInfo(SyncCategory.Outline,      "アウトライン"),
            new CategoryInfo(SyncCategory.Emission,     "エミッション"),
            new CategoryInfo(SyncCategory.Lighting,     "ライティング設定"),
            new CategoryInfo(SyncCategory.Reflection,   "光沢"),
            new CategoryInfo(SyncCategory.MatCap,       "MatCap"),
            new CategoryInfo(SyncCategory.Glitter,      "ラメ"),
            new CategoryInfo(SyncCategory.DistanceFade, "距離フェード"),
        };

        private static readonly Dictionary<SyncCategory, string[]> Properties =
            new Dictionary<SyncCategory, string[]>
        {
            // 注意: メインカラーは "_MainColor" ではなく "_Color"（lts.shader 実証済み）
            [SyncCategory.MainColor] = new[]
            {
                "_Color", "_MainTexHSVG", "_MainGradationStrength",
            },
            [SyncCategory.Shadow] = new[]
            {
                "_UseShadow", "_ShadowStrength", "_ShadowColorType",
                "_ShadowColor", "_ShadowBorder", "_ShadowBlur",
                "_ShadowNormalStrength", "_ShadowReceive",
                "_Shadow2ndColor", "_Shadow2ndBorder", "_Shadow2ndBlur",
                "_Shadow2ndNormalStrength", "_Shadow2ndReceive",
                "_Shadow3rdColor", "_Shadow3rdBorder", "_Shadow3rdBlur",
                "_Shadow3rdNormalStrength", "_Shadow3rdReceive",
                "_ShadowBorderColor", "_ShadowBorderRange",
                "_ShadowMainStrength", "_ShadowEnvStrength",
                "_ShadowAOShift", "_ShadowAOShift2", "_ShadowPostAO",
                "_ShadowMaskType", "_ShadowFlatBorder", "_ShadowFlatBlur",
            },
            [SyncCategory.RimLight] = new[]
            {
                "_UseRim", "_RimColor", "_RimBorder", "_RimBlur",
                "_RimFresnelPower", "_RimMainStrength", "_RimNormalStrength",
                "_RimEnableLighting", "_RimShadowMask", "_RimBackfaceMask",
                "_RimVRParallaxStrength", "_RimApplyTransparency",
                "_RimDirStrength", "_RimDirRange",
                "_RimIndirRange", "_RimIndirColor", "_RimIndirBorder", "_RimIndirBlur",
                "_RimBlendMode",
            },
            [SyncCategory.RimShade] = new[]
            {
                "_UseRimShade", "_RimShadeColor", "_RimShadeNormalStrength",
                "_RimShadeBorder", "_RimShadeBlur", "_RimShadeFresnelPower",
            },
            [SyncCategory.Backlight] = new[]
            {
                "_UseBacklight", "_BacklightColor", "_BacklightMainStrength",
                "_BacklightNormalStrength", "_BacklightBorder", "_BacklightBlur",
                "_BacklightDirectivity", "_BacklightViewStrength",
                "_BacklightReceiveShadow", "_BacklightBackfaceMask",
            },
            // アウトライン系プロパティは非アウトライン版シェーダーにも宣言されているため
            // コピー自体は成功するが、描画に反映されるのはアウトライン版のみ（UI で注意表示）
            [SyncCategory.Outline] = new[]
            {
                "_OutlineColor", "_OutlineTexHSVG", "_OutlineWidth", "_OutlineFixWidth",
                "_OutlineVertexR2Width", "_OutlineDeleteMesh", "_OutlineEnableLighting",
                "_OutlineZBias", "_OutlineDisableInVR",
                "_OutlineLitColor", "_OutlineLitApplyTex", "_OutlineLitScale",
                "_OutlineLitOffset", "_OutlineLitShadowReceive",
                "_OutlineVectorScale", "_OutlineVectorUVMode",
            },
            [SyncCategory.Emission] = new[]
            {
                "_UseEmission", "_EmissionColor", "_EmissionMap_UVMode",
                "_EmissionMainStrength", "_EmissionBlend", "_EmissionBlendMode",
                "_EmissionBlink", "_EmissionUseGrad", "_EmissionGradSpeed",
                "_EmissionParallaxDepth", "_EmissionFluorescence",
                "_UseEmission2nd", "_Emission2ndColor", "_Emission2ndMap_UVMode",
                "_Emission2ndMainStrength", "_Emission2ndBlend", "_Emission2ndBlendMode",
                "_Emission2ndBlink", "_Emission2ndUseGrad", "_Emission2ndGradSpeed",
                "_Emission2ndParallaxDepth", "_Emission2ndFluorescence",
            },
            // アバター全身のマテリアルで統一するのが定石の値。本ツールの主用途
            [SyncCategory.Lighting] = new[]
            {
                "_LightMinLimit", "_LightMaxLimit", "_MonochromeLighting",
                "_AsUnlit", "_VertexLightStrength", "_lilDirectionalLightStrength",
                "_BeforeExposureLimit", "_AlphaBoostFA", "_AAStrength",
                "_LightDirectionOverride",
            },
            [SyncCategory.Reflection] = new[]
            {
                "_UseReflection", "_Smoothness", "_Metallic", "_Reflectance",
                "_GSAAStrength", "_ApplySpecular", "_ApplySpecularFA", "_SpecularToon",
                "_SpecularNormalStrength", "_SpecularBorder", "_SpecularBlur",
                "_ApplyReflection", "_ReflectionNormalStrength", "_ReflectionColor",
                "_ReflectionApplyTransparency", "_ReflectionCubeColor",
                "_ReflectionCubeOverride", "_ReflectionCubeEnableLighting",
                "_ReflectionBlendMode",
            },
            [SyncCategory.MatCap] = new[]
            {
                "_UseMatCap", "_MatCapTex", "_MatCapColor", "_MatCapMainStrength", "_MatCapBlend",
                "_MatCapEnableLighting", "_MatCapShadowMask", "_MatCapBackfaceMask",
                "_MatCapLod", "_MatCapBlendMode", "_MatCapApplyTransparency",
                "_MatCapNormalStrength", "_MatCapZRotCancel", "_MatCapPerspective",
                "_MatCapVRParallaxStrength", "_MatCapBlendUV1",
                "_MatCapCustomNormal", "_MatCapBumpMap", "_MatCapBumpScale",
                "_UseMatCap2nd", "_MatCap2ndTex", "_MatCap2ndColor", "_MatCap2ndMainStrength",
                "_MatCap2ndBlend", "_MatCap2ndEnableLighting", "_MatCap2ndShadowMask",
                "_MatCap2ndBackfaceMask", "_MatCap2ndLod", "_MatCap2ndBlendMode",
                "_MatCap2ndApplyTransparency", "_MatCap2ndNormalStrength",
                "_MatCap2ndZRotCancel", "_MatCap2ndPerspective",
                "_MatCap2ndVRParallaxStrength", "_MatCap2ndBlendUV1",
                "_MatCap2ndCustomNormal", "_MatCap2ndBumpMap", "_MatCap2ndBumpScale",
            },
            [SyncCategory.Glitter] = new[]
            {
                "_UseGlitter", "_GlitterUVMode", "_GlitterColor", "_GlitterMainStrength",
                "_GlitterNormalStrength", "_GlitterScaleRandomize", "_GlitterAngleRandomize",
                "_GlitterApplyShape", "_GlitterAtras", "_GlitterParams1", "_GlitterParams2",
                "_GlitterPostContrast", "_GlitterSensitivity", "_GlitterEnableLighting",
                "_GlitterShadowMask", "_GlitterBackfaceMask", "_GlitterApplyTransparency",
                "_GlitterVRParallaxStrength",
            },
            [SyncCategory.DistanceFade] = new[]
            {
                "_DistanceFadeColor", "_DistanceFade", "_DistanceFadeMode",
                "_DistanceFadeRimColor", "_DistanceFadeRimFresnelPower",
            },
        };

        /// <summary>選択カテゴリに含まれる全プロパティ名を収集する。</summary>
        public static List<string> CollectProperties(SyncCategory mask)
        {
            var result = new List<string>();
            foreach (CategoryInfo info in Categories)
            {
                if ((mask & info.Category) != 0 && Properties.TryGetValue(info.Category, out string[] names))
                {
                    result.AddRange(names);
                }
            }
            return result;
        }

        public static int CountProperties(SyncCategory category)
        {
            return Properties.TryGetValue(category, out string[] names) ? names.Length : 0;
        }
    }
}
