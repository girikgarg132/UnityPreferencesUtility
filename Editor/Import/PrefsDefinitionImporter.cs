using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Imports <c>.prefsdef</c> files as <see cref="PrefsDefinitionAsset"/>. Importing never writes other files.</summary>
    [ScriptedImporter(ImporterVersion, PrefsDefinitionPaths.Extension)]
    public sealed class PrefsDefinitionImporter : ScriptedImporter
    {
        private const int ImporterVersion = 1;
        private const string MainObjectIdentifier = "PrefsDefinition";
        private const string IconName = "Settings";

        /// <summary>Icon used for definition assets.</summary>
        internal static Texture2D Icon => EditorGUIUtility.IconContent(IconName).image as Texture2D;

        /// <inheritdoc />
        public override void OnImportAsset(AssetImportContext ctx)
        {
            PrefsDefinition definition = null;
            string importError = null;
            try
            {
                definition = PrefsDefinitionSerializer.Load(Path.GetFullPath(ctx.assetPath));
            }
            catch (Exception exception)
            {
                importError = exception.Message;
                ctx.LogImportError($"Invalid preferences definition: {exception.Message}");
            }

            PrefsDefinitionAsset asset = ScriptableObject.CreateInstance<PrefsDefinitionAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            asset.Initialize(definition, importError);
            ctx.AddObjectToAsset(MainObjectIdentifier, asset, Icon);
            ctx.SetMainObject(asset);
        }
    }
}
