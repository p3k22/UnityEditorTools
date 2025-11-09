namespace P3k.UnityEditorTools.Tools
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;

   using UnityEditor;

   /*
    * Imports a multi-file bundle and writes content back into existing scripts
    * or creates new scripts based on detected namespace paths.
    *
    * Bundle files must contain separators like:  ***FileName.cs***
    * Each separator begins a new script entry, followed by its file content.
    */
   internal static class ScriptImporter
   {
      // Entry point for importing a script bundle into the Unity project
      internal static void ImportFromFile(string importFilePath, string rootFolder)
      {
         // --- Step 1: Read all lines and parse into file entries ---
         var lines = File.ReadAllLines(importFilePath);
         var entries = ParseBundle(lines);

         // --- Step 2: Process each file entry ---
         foreach (var (fileNameExt, contentLines) in entries)
         {
            var content = string.Join(Environment.NewLine, contentLines);
            var searchName = Path.GetFileNameWithoutExtension(fileNameExt);
            var ext = Path.GetExtension(fileNameExt);
            var written = false;

            // --- Step 2a: Try to find an existing asset with the same filename ---
            var guids = AssetDatabase.FindAssets(searchName);
            foreach (var guid in guids)
            {
               var assetPath = AssetDatabase.GUIDToAssetPath(guid);

               // Check exact filename match
               if (string.Equals(Path.GetFileName(assetPath), fileNameExt, StringComparison.OrdinalIgnoreCase))
               {
                  var full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                  File.WriteAllText(full, content); // Overwrite existing script
                  written = true;
               }
            }

            // --- Step 2b: If no existing file found, create a new .cs file based on namespace ---
            if (!written && ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
               // Attempt to extract namespace line
               var nsLine = contentLines.FirstOrDefault(l => l.TrimStart().StartsWith("namespace "));
               var nsPath = rootFolder;

               if (nsLine != null)
               {
                  // Split namespace declaration into segments
                  var parts = nsLine.Trim().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                  if (parts.Length >= 2)
                  {
                     var nsSegments = parts[1].TrimEnd('{', ';').Split('.');
                     var rootSegments = rootFolder.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

                     // If namespace already starts with root folder, append only the rest
                     var toAppend = nsSegments;
                     if (nsSegments.Length >= rootSegments.Length
                         && nsSegments.Take(rootSegments.Length).SequenceEqual(rootSegments))
                     {
                        toAppend = nsSegments.Skip(rootSegments.Length).ToArray();
                     }

                     // Build namespace path hierarchy as subfolders
                     foreach (var seg in toAppend)
                     {
                        nsPath = nsPath + "/" + seg;
                     }
                  }
               }

               // Ensure namespace-based folder path exists
               var fullDir = Path.Combine(Directory.GetCurrentDirectory(), nsPath);
               if (!Directory.Exists(fullDir))
               {
                  Directory.CreateDirectory(fullDir);
               }

               // Write new script file into namespace folder
               var newAsset = nsPath + "/" + fileNameExt;
               var fullNew = Path.Combine(Directory.GetCurrentDirectory(), newAsset);
               File.WriteAllText(fullNew, content);
            }
         }

         // --- Step 3: Refresh Unity Asset Database so editor sees the new/updated scripts ---
         AssetDatabase.Refresh();
      }

      // Parses bundle text into a dictionary of (filename → file content lines)
      private static Dictionary<string, List<string>> ParseBundle(string[] lines)
      {
         var entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
         string currentKey = null;
         var buffer = new List<string>();

         foreach (var raw in lines)
         {
            var line = raw.TrimEnd();

            // A line like ***MyFile.cs*** marks the start of a new file
            if (line.StartsWith("***") && line.EndsWith("***"))
            {
               // Store previous entry if one exists
               if (currentKey != null)
                  entries[currentKey] = new List<string>(buffer);

               // Extract filename (strip * characters)
               currentKey = line.Trim('*');
               buffer.Clear();
            }
            else if (currentKey != null)
            {
               // Collect lines for the current file
               buffer.Add(raw);
            }
         }

         // Store the final entry
         if (currentKey != null)
            entries[currentKey] = new List<string>(buffer);

         return entries;
      }
   }
}
