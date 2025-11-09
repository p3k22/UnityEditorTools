namespace P3k.UnityEditorTools.Tools
{
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Text.RegularExpressions;

   using UnityEditor;

   using UnityEngine;

   public sealed class GuidRemapFolderWindow : EditorWindow
   {
      private string _folderPath = string.Empty;

      private Vector2 _scroll;

      private void OnGUI()
      {
         _scroll = EditorGUILayout.BeginScrollView(_scroll);

         EditorGUILayout.LabelField("Folder", EditorStyles.boldLabel);
         EditorGUILayout.BeginHorizontal();
         EditorGUILayout.SelectableLabel(
         string.IsNullOrEmpty(_folderPath) ? "(none selected)" : _folderPath,
         GUILayout.Height(18));
         if (GUILayout.Button("Select Folder", GUILayout.Width(120f)))
         {
            var selected = EditorUtility.OpenFolderPanel("Select Folder To Remap GUIDs", "", "");
            if (!string.IsNullOrEmpty(selected))
            {
               _folderPath = selected;
            }
         }

         EditorGUILayout.EndHorizontal();

         EditorGUILayout.Space();

         if (GUILayout.Button("Remap GUIDs In Folder"))
         {
            if (string.IsNullOrWhiteSpace(_folderPath))
            {
               EditorUtility.DisplayDialog("Error", "No folder selected.", "OK");
            }
            else if (!Directory.Exists(_folderPath))
            {
               EditorUtility.DisplayDialog("Error", "Selected directory does not exist.", "OK");
            }
            else
            {
               RemapGuids(_folderPath);
            }
         }

         EditorGUILayout.EndScrollView();
      }

      public static void ShowWindow()
      {
         var window = GetWindow<GuidRemapFolderWindow>("GUID Remap Folder");
         window.minSize = new Vector2(520f, 140f);
      }

      private static bool IsBinary(string filePath)
      {
         var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
         switch (ext)
         {
            case ".dll":
            case ".exe":
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".psd":
            case ".mp3":
            case ".wav":
            case ".ogg":
            case ".fbx":
            case ".anim":
            case ".mp4":
            case ".mov":
            case ".ttf":
            case ".otf":
            case ".aac":
            case ".webm":
               return true;
            default:
               return false;
         }
      }

      private static void RemapGuids(string rootPath)
      {
         var metaFiles = Directory.GetFiles(rootPath, "*.meta", SearchOption.AllDirectories);
         if (metaFiles.Length == 0)
         {
            EditorUtility.DisplayDialog("Result", "No .meta files found.", "OK");
            return;
         }

         var guidRegex = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase);
         var map = new Dictionary<string, string>();

         foreach (var metaPath in metaFiles)
         {
            string text;
            try
            {
               text = File.ReadAllText(metaPath);
            }
            catch
            {
               continue;
            }

            var match = guidRegex.Match(text);
            if (!match.Success)
            {
               continue;
            }

            var oldGuid = match.Groups[1].Value;
            if (string.IsNullOrEmpty(oldGuid))
            {
               continue;
            }

            if (!map.ContainsKey(oldGuid))
            {
               var newGuid = GUID.Generate().ToString();
               map.Add(oldGuid, newGuid);
               text = guidRegex.Replace(text, "guid: " + newGuid, 1);
               try
               {
                  File.WriteAllText(metaPath, text);
               }
               catch
               {
               }
            }
         }

         if (map.Count == 0)
         {
            EditorUtility.DisplayDialog("Result", "No GUIDs found to remap.", "OK");
            return;
         }

         var allFiles = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
         var changedFiles = 0;

         foreach (var file in allFiles)
         {
            if (IsBinary(file))
            {
               continue;
            }

            string text;
            try
            {
               text = File.ReadAllText(file);
            }
            catch
            {
               continue;
            }

            var original = text;
            foreach (var kvp in map)
            {
               if (text.Contains(kvp.Key))
               {
                  text = text.Replace(kvp.Key, kvp.Value);
               }
            }

            if (!ReferenceEquals(original, text) && original != text)
            {
               try
               {
                  File.WriteAllText(file, text);
                  changedFiles++;
               }
               catch
               {
               }
            }
         }

         if (rootPath.StartsWith(Application.dataPath))
         {
            AssetDatabase.Refresh();
         }

         EditorUtility.DisplayDialog(
         "GUID Remap Complete",
         $"Meta files processed: {metaFiles.Length}\nGUIDs remapped: {map.Count}\nFiles updated: {changedFiles}",
         "OK");
      }
   }
}