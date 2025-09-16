namespace P3k.UnityEditorTools.Tools
{
   using System;
   using System.IO;
   using System.Linq;

   using UnityEditor;

   internal static class BuildAssetBundles
   {
      // Purpose: Normalizes an extension by trimming and ensuring it starts with a dot.
      internal static string NormalizeExtension(string ext)
      {
         if (string.IsNullOrWhiteSpace(ext))
         {
            return ".modbundle";
         }

         var trimmed = ext.Trim();
         return trimmed.StartsWith(".") ? trimmed : "." + trimmed;
      }

      // Purpose: Validates the normalized extension for filename safety and structure.
      internal static bool IsValidExtension(string ext, out string error)
      {
         error = string.Empty;

         if (string.IsNullOrWhiteSpace(ext))
         {
            error = "Extension cannot be empty.";
            return false;
         }

         if (!ext.StartsWith("."))
         {
            error = "Extension must start with a dot (e.g., .modbundle).";
            return false;
         }

         if (ext.Length == 1)
         {
            error = "Extension must contain characters after the dot.";
            return false;
         }

         var invalidChars = Path.GetInvalidFileNameChars();
         if (ext.Any(c => invalidChars.Contains(c)))
         {
            error = "Extension contains invalid filename characters.";
            return false;
         }

         if (ext.Contains(Path.DirectorySeparatorChar) || ext.Contains(Path.AltDirectorySeparatorChar))
         {
            error = "Extension must not contain directory separators.";
            return false;
         }

         return true;
      }

      // Purpose: Builds all AssetBundles and renames outputs by appending the custom extension.
      internal static void BuildAllBundles(string outputPath, string customExtension, bool notifyOnComplete)
      {
         var normalized = NormalizeExtension(customExtension);

         if (!IsValidExtension(normalized, out var error))
         {
            EditorUtility.DisplayDialog("Invalid Extension", error, "OK");
            return;
         }

         if (!Directory.Exists(outputPath))
         {
            Directory.CreateDirectory(outputPath);
         }

         var existingFiles = Directory.GetFiles(outputPath);
         foreach (var file in existingFiles)
         {
            File.Delete(file);
         }

         BuildPipeline.BuildAssetBundles(
         outputPath,
         BuildAssetBundleOptions.None,
         EditorUserBuildSettings.activeBuildTarget);

         var allFiles = Directory.GetFiles(outputPath);
         foreach (var file in allFiles)
         {
            var fileName = Path.GetFileName(file);

            if (file.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) || fileName == "AssetBundles")
            {
               continue;
            }

            var newPath = Path.Combine(outputPath, fileName + normalized);

            if (File.Exists(newPath))
            {
               File.Delete(newPath);
            }

            File.Move(file, newPath);
         }

         if (notifyOnComplete)
         {
            EditorUtility.DisplayDialog("AssetBundles", "Build and rename complete.", "OK");
         }

         AssetDatabase.Refresh();
      }
   }
}
