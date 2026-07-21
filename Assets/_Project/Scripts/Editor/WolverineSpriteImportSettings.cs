using UnityEditor;

namespace FA.EditorTools
{
    /// <summary>
    /// ÉP chuẩn import cho sprite Wolverine MỚI (Phase W0): Single / PPU 100 / Pivot Bottom —
    /// khớp chuẩn Emiya Archer. CHỈ áp khi asset chưa có .meta (importSettingsMissing) để KHÔNG
    /// đè 521 sprite cũ đang dùng pivot custom theo axis MUGEN (ép Bottom lên bộ cũ sẽ lệch frame
    /// vì sprite crop sát; bộ mới đã được extractor pad canvas cho axis nằm đúng bottom-center).
    /// </summary>
    public sealed class WolverineSpriteImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Art/Characters/Wolverine/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            if (!assetImporter.importSettingsMissing) return;   // chỉ file mới, không đụng meta cũ.

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.mipmapEnabled = false;

            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)UnityEngine.SpriteAlignment.BottomCenter;
            ti.SetTextureSettings(s);
        }
    }
}
