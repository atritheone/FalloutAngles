// imports
using UnityEngine;
using UnityEditor;
using System.IO;



// class
public class AlphaPackerTool : EditorWindow
{
    
    // variables
    // The base color texture we want to embed an alpha channel into.
    private Texture2D baseColorTexture;
    
    // The alpha mask texture (white = keep, black = cut).
    private Texture2D alphaTexture;
    
    // If enabled, invert the alpha (useful if the mask is backwards).
    private bool invertAlpha;
    
    // Suffix added to the output filename.
    private string outputSuffix = "_PackedA";
    
    // If enabled, output will be written next to the base color texture.
    private bool saveNextToBase = true;
    
    // Optional: choose an output folder inside Assets when not saving next to base.
    private DefaultAsset outputFolder;



    // methods
    [MenuItem("Tools/Fallout Angles/Textures/Pack Alpha Into BaseColor")]
    private static void OpenWindow()
    {
        // Open the editor window.
        GetWindow<AlphaPackerTool>("Pack Alpha Into BaseColor");
    }
    
    
    private void OnGUI()
    {
        // Draw the title.
        EditorGUILayout.LabelField("Pack Alpha Into BaseColor (HDRP/Lit)", EditorStyles.boldLabel);
        
        // Add a small help line.
        EditorGUILayout.HelpBox("Creates a new RGBA BaseColor where A comes from your separate alpha mask, so HDRP Alpha Clipping works.", MessageType.Info);
        
        // Let the user assign the base color texture.
        baseColorTexture = (Texture2D)EditorGUILayout.ObjectField("Base Color (RGB)", baseColorTexture, typeof(Texture2D), false);
        
        // Let the user assign the alpha texture.
        alphaTexture = (Texture2D)EditorGUILayout.ObjectField("Alpha Mask (Grayscale)", alphaTexture, typeof(Texture2D), false);
        
        // Let the user invert alpha if needed.
        invertAlpha = EditorGUILayout.Toggle("Invert Alpha", invertAlpha);
        
        // Let the user choose the output suffix.
        outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);
        
        // Let the user choose where to save.
        saveNextToBase = EditorGUILayout.Toggle("Save Next To Base", saveNextToBase);
        
        // If not saving next to base, allow choosing an output folder.
        if (!saveNextToBase)
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder (Assets)", outputFolder, typeof(DefaultAsset), false);
        
        // Add spacing.
        EditorGUILayout.Space();
        
        // Only enable the button when both textures are assigned.
        GUI.enabled = baseColorTexture != null && alphaTexture != null;
        
        // Run button.
        if (GUILayout.Button("Generate Packed BaseColor (RGBA)"))
            PackAlphaIntoBaseColor(baseColorTexture, alphaTexture, invertAlpha, outputSuffix, saveNextToBase, outputFolder);
        
        // Re-enable GUI.
        GUI.enabled = true;
    }
    
    
    private static void PackAlphaIntoBaseColor(Texture2D baseTex, Texture2D alphaTex, bool invert, string suffix, bool saveNextToBase, DefaultAsset folderAsset)
    {
        // Get the asset paths for both textures.
        string basePath = AssetDatabase.GetAssetPath(baseTex);
        
        string alphaPath = AssetDatabase.GetAssetPath(alphaTex);
        
        // Stop if paths are invalid.
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(alphaPath))
            return;
        
        // Get importers so we can ensure textures are readable and correctly treated as color/data.
        TextureImporter baseImporter = AssetImporter.GetAtPath(basePath) as TextureImporter;
        
        TextureImporter alphaImporter = AssetImporter.GetAtPath(alphaPath) as TextureImporter;
        
        // Stop if we couldn't get importers.
        if (baseImporter == null || alphaImporter == null)
            return;
        
        // Cache original importer settings so we can restore them afterwards.
        bool baseWasReadable = baseImporter.isReadable;
        
        bool alphaWasReadable = alphaImporter.isReadable;
        
        bool baseWasSRGB = baseImporter.sRGBTexture;
        
        bool alphaWasSRGB = alphaImporter.sRGBTexture;
        
        TextureImporterCompression baseCompression = baseImporter.textureCompression;
        
        TextureImporterCompression alphaCompression = alphaImporter.textureCompression;
        
        // Make base readable and treated as color (sRGB).
        baseImporter.isReadable = true;
        
        baseImporter.sRGBTexture = true;
        
        baseImporter.textureCompression = TextureImporterCompression.Uncompressed;
        
        baseImporter.SaveAndReimport();
        
        // Make alpha readable and treated as data (linear).
        alphaImporter.isReadable = true;
        
        alphaImporter.sRGBTexture = false;
        
        alphaImporter.textureCompression = TextureImporterCompression.Uncompressed;
        
        alphaImporter.SaveAndReimport();
        
        // Reload readable textures after reimport.
        Texture2D readableBase = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        
        Texture2D readableAlpha = AssetDatabase.LoadAssetAtPath<Texture2D>(alphaPath);
        
        // Stop if reload failed.
        if (readableBase == null || readableAlpha == null)
            return;
        
        // Ensure dimensions match (required for 1:1 packing).
        if (readableBase.width != readableAlpha.width || readableBase.height != readableAlpha.height)
        {
            // Tell the user exactly what is wrong.
            Debug.LogError("Alpha pack failed: BaseColor and Alpha textures must be the same resolution.");
            
            // Restore importers before exiting.
            RestoreImporter(baseImporter, baseWasReadable, baseWasSRGB, baseCompression);
            
            RestoreImporter(alphaImporter, alphaWasReadable, alphaWasSRGB, alphaCompression);
            
            return;
        }
        
        // Read pixel arrays.
        Color[] basePixels = readableBase.GetPixels();
        
        Color[] alphaPixels = readableAlpha.GetPixels();
        
        // Create output texture (RGBA).
        Texture2D output = new Texture2D(readableBase.width, readableBase.height, TextureFormat.RGBA32, false, false);
        
        // Create output pixel buffer.
        Color[] outPixels = new Color[basePixels.Length];
        
        // Pack alpha into base color.
        for (int i = 0; i < outPixels.Length; i++)
        {
            // Read alpha from the alpha texture's red channel (grayscale).
            float a = alphaPixels[i].r;
            
            // Optionally invert alpha.
            if (invert)
                a = 1.0f - a;
            
            // Write RGB from base, and A from mask.
            outPixels[i] = new Color(basePixels[i].r, basePixels[i].g, basePixels[i].b, a);
        }
        
        // Apply pixels.
        output.SetPixels(outPixels);
        
        // Upload changes.
        output.Apply();
        
        // Decide output directory.
        string outputDir = "";
        
        // If saving next to base, use base texture directory.
        if (saveNextToBase)
            outputDir = Path.GetDirectoryName(basePath);
        
        // Otherwise, use the chosen folder asset.
        if (!saveNextToBase)
        {
            // Stop if user did not choose a folder.
            if (folderAsset == null)
            {
                Debug.LogError("Alpha pack failed: Please choose an Output Folder (Assets).");
                
                RestoreImporter(baseImporter, baseWasReadable, baseWasSRGB, baseCompression);
                
                RestoreImporter(alphaImporter, alphaWasReadable, alphaWasSRGB, alphaCompression);
                
                return;
            }
            
            // Convert folder asset to path.
            outputDir = AssetDatabase.GetAssetPath(folderAsset);
        }
        
        // Build output filename.
        string baseName = Path.GetFileNameWithoutExtension(basePath);
        
        string outName = baseName + suffix + ".png";
        
        // Build full output path inside Assets.
        string outPath = Path.Combine(outputDir, outName).Replace("\\", "/");
        
        // Encode as PNG.
        byte[] png = output.EncodeToPNG();
        
        // Write file.
        File.WriteAllBytes(outPath, png);
        
        // Import into Unity.
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
        
        // Configure the new packed texture importer so HDRP treats it as BaseColor with alpha.
        TextureImporter outImporter = AssetImporter.GetAtPath(outPath) as TextureImporter;
        
        // Apply correct settings if importer exists.
        if (outImporter != null)
        {
            // Ensure it is treated as a color texture.
            outImporter.textureType = TextureImporterType.Default;
            
            outImporter.sRGBTexture = true;
            
            outImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            
            outImporter.textureCompression = TextureImporterCompression.CompressedHQ;
            
            outImporter.SaveAndReimport();
        }
        
        // Restore original importer settings.
        RestoreImporter(baseImporter, baseWasReadable, baseWasSRGB, baseCompression);
        
        RestoreImporter(alphaImporter, alphaWasReadable, alphaWasSRGB, alphaCompression);
        
        // Log success.
        Debug.Log("Packed alpha into BaseColor: " + outPath);
    }
    
    
    private static void RestoreImporter(TextureImporter importer, bool wasReadable, bool wasSRGB, TextureImporterCompression compression)
    {
        // Stop if importer is null.
        if (importer == null)
            return;
        
        // Restore readability.
        importer.isReadable = wasReadable;
        
        // Restore sRGB flag.
        importer.sRGBTexture = wasSRGB;
        
        // Restore compression.
        importer.textureCompression = compression;
        
        // Reimport to apply restore.
        importer.SaveAndReimport();
    }
}
