using System.Collections.Generic;
using System.Linq;
using Dreamy.Debugger.Presentation;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Editor
{
    public static class DebuggerInterfaceAssets
    {
        private const string Root="Packages/com.dreamy.debugger/Runtime/Branding/Interface/";
        public static void Install()
        {
            AssetDatabase.Refresh();
            foreach(var file in new[]{"Button.png","Panel.png"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+file);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.spriteBorder=new Vector4(8,8,8,8);
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            }
            var catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
            var entries=catalog.Entries.Where(e=>!e.Key.StartsWith("DreamyDebugger/Theme/")).ToList();
            entries.Add(new CatalogEntry{Key="DreamyDebugger/Theme/Font",Asset=AssetDatabase.LoadAssetAtPath<Font>(Root+"Inter-Regular.ttf")});
            entries.Add(new CatalogEntry{Key="DreamyDebugger/Theme/Button",Asset=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Button.png")});
            entries.Add(new CatalogEntry{Key="DreamyDebugger/Theme/Panel",Asset=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Panel.png")});
            catalog.Entries=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }
}
