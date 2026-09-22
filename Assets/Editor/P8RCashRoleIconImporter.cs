using UnityEditor;
using UnityEngine;

/// <summary>Only these two new UI assets; never changes existing icons.
/// 仅约束新角色图标的导入，保留透明抗锯齿边缘，不修改旧素材。</summary>
public sealed class P8RCashRoleIconImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/UI/P8R/RoleIcons/employee-apron.png"
            && assetPath != "Assets/Resources/UI/P8R/RoleIcons/customer-bag.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
