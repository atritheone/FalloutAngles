// imports
using UnityEngine;
using UnityEditor;
using System.IO;



// class
public class HDRPMaskMapGenerator : EditorWindow
{
    
    // variables
    // The roughness texture we will convert into an HDRP mask map.
    private Texture2D roughnessTexture;

    // The output filename suffix.
    private string outputSuffix = "_mask";



    // methods
    [MenuItem("Tools/Fallout Angles/HDRP/Mask Map Generator")]
    private static void OpenWindow()
    {
        // Create and show the editor window.
        GetWindow<HDRPMaskMapGenerator>("Mask Map Generator");
    }


    private void OnGUI()
    {
        // Draw a label for the tool.
        EditorGUILayout.LabelField("Create HDRP Mask Map from Roughness", EditorStyles.boldLabel);

        // Let the user assign the roughness texture.
        roughnessTexture = (Texture2D)EditorGUILayout.ObjectField("Roughness Texture", roughnessTexture, typeof(Texture2D), false);

        // Let the user change the output suffix if desired.
        outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);

        // Add spacing for readability.
        EditorGUILayout.Space();

        // Only enable the button if a texture is assigned.
        GUI.enabled = roughnessTexture != null;

        // Button to generate the mask map.
        if (GUILayout.Button("Generate Mask Map"))
        {
            // Run the conversion.
            GenerateMaskMapFromRoughness(roughnessTexture, outputSuffix);
        }

        // Re-enable GUI.
        GUI.enabled = true;
    }


    private static void GenerateMaskMapFromRoughness(Texture2D roughness, string suffix)
    {
        // Get the asset path for the roughness texture.
        string roughnessPath = AssetDatabase.GetAssetPath(roughness);

        // Stop if we can't find the asset path.
        if (string.IsNullOrEmpty(roughnessPath))
            return;

        // Get the texture importer so we can ensure the texture is readable.
        TextureImporter importer = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;

        // Stop if we couldn't get an importer.
        if (importer == null)
            return;

        // Store the current readability so we can restore it later.
        bool wasReadable = importer.isReadable;

        // Make the roughness texture readable so we can read pixels.
        importer.isReadable = true;

        // Ensure the roughness is treated as data (not color).
        importer.sRGBTexture = false;

        // Apply importer changes.
        importer.SaveAndReimport();

        // Reload the texture now that it is readable.
        Texture2D readableRoughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);

        // Stop if reload failed.
        if (readableRoughness == null)
            return;

        // Read all pixels from the roughness texture.
        Color[] roughPixels = readableRoughness.GetPixels();

        // Create an output texture in RGBA32 format.
        Texture2D mask = new Texture2D(readableRoughness.width, readableRoughness.height, TextureFormat.RGBA32, false, true);

        // Create an array for the output pixels.
        Color[] maskPixels = new Color[roughPixels.Length];

        // Convert each pixel into HDRP mask map channels.
        for (int i = 0; i < roughPixels.Length; i++)
        {
            // Read roughness from the red channel (common for grayscale maps).
            float roughValue = roughPixels[i].r;

            // Invert roughness to get smoothness.
            float smoothValue = 1.0f - roughValue;

            // Build the HDRP mask pixel:
            // R = metallic (0 for skin)
            // G = AO (1 if you don't have an AO map)
            // B = detail mask (0)
            // A = smoothness (inverted roughness)
            maskPixels[i] = new Color(0.0f, 1.0f, 0.0f, smoothValue);
        }

        // Apply pixels into the output texture.
        mask.SetPixels(maskPixels);

        // Upload to GPU.
        mask.Apply();

        // Build the output path next to the source texture.
        string directory = Path.GetDirectoryName(roughnessPath);

        // Build the output filename.
        string filename = Path.GetFileNameWithoutExtension(roughnessPath) + suffix + ".png";

        // Combine into a full asset path.
        string outputPath = Path.Combine(directory, filename).Replace("\\", "/");

        // Encode the mask texture to PNG bytes.
        byte[] pngBytes = mask.EncodeToPNG();

        // Write the file to disk.
        File.WriteAllBytes(outputPath, pngBytes);

        // Import the new texture into Unity.
        AssetDatabase.ImportAsset(outputPath);

        // Set correct import settings on the new mask texture.
        TextureImporter maskImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;

        // If we got an importer, configure it.
        if (maskImporter != null)
        {
            // Treat mask maps as data.
            maskImporter.sRGBTexture = false;

            // Ensure it can be sampled normally.
            maskImporter.textureCompression = TextureImporterCompression.Uncompressed;

            // Apply changes.
            maskImporter.SaveAndReimport();
        }

        // Restore the original readability on the roughness texture.
        importer.isReadable = wasReadable;

        // Reimport to restore previous state.
        importer.SaveAndReimport();

        // Refresh the asset database view.
        AssetDatabase.Refresh();
    }
}
