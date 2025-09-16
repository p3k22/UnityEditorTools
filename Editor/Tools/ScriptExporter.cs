namespace P3k.UnityEditorTools.Tools
{
   using System;
   using System.Collections.Generic;
   using System.Globalization;
   using System.IO;
   using System.Linq;

   using UnityEditor;

   internal static class ScriptExporter
   {
      internal static void ExportSelectedScripts(
         IEnumerable<string> selectedAssetPaths,
         string exportDir,
         string projectName)
         {
            var paths = selectedAssetPaths?.ToList() ?? new List<string>();
            if (paths.Count == 0) return;

            if (!Directory.Exists(exportDir))
            {
               Directory.CreateDirectory(exportDir);
            }

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            var fileName = $"{projectName}_{timestamp}.txt";
            var exportPath = Path.Combine(exportDir, fileName);

            using (var w = new StreamWriter(exportPath, false))
            {
               var first = true;
               foreach (var script in paths.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
               {
                  if (!first) w.WriteLine();
                  first = false;

                  var fName = Path.GetFileNameWithoutExtension(script);
                  var ext = Path.GetExtension(script);
                  w.WriteLine($"***{fName}{ext}***");

                  var full = Path.Combine(Directory.GetCurrentDirectory(), script);
                  w.WriteLine(File.ReadAllText(full));
               }
            }

            EditorUtility.RevealInFinder(exportPath);
         }
      }
}
