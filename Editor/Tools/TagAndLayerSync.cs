namespace P3k.UnityEditorTools.Tools
{
   // Removes init-on-load. Persists desired tags/layers in ProjectSettings JSON.
   // SyncPersistent() now adds missing and prunes extra entries from TagManager.
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;

   using UnityEditor;

   using UnityEngine;

   public static class TagAndLayerSync
   {
      // ---------- Storage ----------
      private const string STORE_PATH = "ProjectSettings/P3k.PersistentTagsAndLayers.json";

      // ---------- Unity defaults ----------
      // Never remove these tags. Unity creates them by default.
      private static readonly HashSet<string> DefaultTags = new(StringComparer.Ordinal)
                                                               {
                                                                  "Untagged",
                                                                  "Respawn",
                                                                  "Finish",
                                                                  "EditorOnly",
                                                                  "MainCamera",
                                                                  "Player",
                                                                  "GameController"
                                                               };

      public static IEnumerable<string> LoadPersistentLayers()
      {
         var s = LoadStore();
         return s.layers ?? new List<string>();
      }

      // ---------- Public API: Load ----------
      public static IEnumerable<string> LoadPersistentTags()
      {
         var s = LoadStore();
         return s.tags ?? new List<string>();
      }

      // ---------- Public API: Save ----------
      public static void SavePersistent(IEnumerable<string> tags, IEnumerable<string> layers)
      {
         var store = new Store {tags = Normalize(tags), layers = Normalize(layers)};
         var json = JsonUtility.ToJson(store, true);

         var dir = Path.GetDirectoryName(STORE_PATH);
         if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
         {
            Directory.CreateDirectory(dir);
         }

         File.WriteAllText(STORE_PATH, json);
         AssetDatabase.Refresh();
      }

      // ---------- Public API: Apply (Add + Prune) ----------
      [MenuItem("Tools/P3k's Persistent Tags & Layers/Sync Now")]
      public static void SyncPersistent()
      {
         var store = LoadStore();
         var desiredTags = new HashSet<string>(Normalize(store.tags), StringComparer.Ordinal);
         var desiredLayers = new HashSet<string>(Normalize(store.layers), StringComparer.Ordinal);

         var tagManager = LoadTagManager();
         if (tagManager == null)
         {
            Debug.LogError("TagManager.asset not found. ShowWindow any scene to generate ProjectSettings, then retry.");
            return;
         }

         var tagsProp = tagManager.FindProperty("tags");
         var layersProp = tagManager.FindProperty("layers");

         // Add missing
         var addedTags = EnsureTags(tagsProp, desiredTags);
         var (addedLayers, noSlotLayers) = EnsureLayers(layersProp, desiredLayers);

         // Prune extras
         var removedTags = RemoveTagsNotIn(tagsProp, desiredTags);
         var removedLayers = RemoveLayersNotIn(layersProp, desiredLayers);

         tagManager.ApplyModifiedPropertiesWithoutUndo();
         AssetDatabase.SaveAssets();

         if (addedTags.Count > 0)
         {
            Debug.Log($"Tags added: {string.Join(", ", addedTags)}");
         }

         if (removedTags.Count > 0)
         {
            Debug.Log($"Tags removed: {string.Join(", ", removedTags)}");
         }

         if (addedLayers.Count > 0)
         {
            Debug.Log($"Layers added: {string.Join(", ", addedLayers)}");
         }

         if (removedLayers.Count > 0)
         {
            Debug.Log($"Layers cleared: {string.Join(", ", removedLayers)}");
         }

         if (noSlotLayers.Count > 0)
         {
            Debug.LogWarning(
            $"No free user layer slots for: {string.Join(", ", noSlotLayers)} (valid slots are 8..31)");
         }
      }

      // ---------- Internals: Layer ops ----------
      private static (List<string> added, List<string> noSlot) EnsureLayers(
         SerializedProperty layersProp,
         IEnumerable<string> names)
      {
         var added = new List<string>();
         var noSlot = new List<string>();

         foreach (var name in Normalize(names))
         {
            if (LayerExists(layersProp, name))
            {
               continue;
            }

            var slot = FindFirstEmptyUserLayerSlot(layersProp);
            if (slot < 0)
            {
               noSlot.Add(name);
               continue;
            }

            var element = layersProp.GetArrayElementAtIndex(slot);
            element.stringValue = name;
            added.Add(name);
         }

         return (added, noSlot);
      }

      private static bool LayerExists(SerializedProperty layersProp, string name)
      {
         for (var i = 0; i < layersProp.arraySize; i++)
         {
            var el = layersProp.GetArrayElementAtIndex(i);
            if (el is {propertyType: SerializedPropertyType.String} && el.stringValue == name)
            {
               return true;
            }
         }

         return false;
      }

      private static bool SerializedStringArrayContains(SerializedProperty arrayProp, string value)
      {
         for (var i = 0; i < arrayProp.arraySize; i++)
         {
            var el = arrayProp.GetArrayElementAtIndex(i);
            if (el is {propertyType: SerializedPropertyType.String} && el.stringValue == value)
            {
               return true;
            }
         }

         return false;
      }

      private static int FindFirstEmptyUserLayerSlot(SerializedProperty layersProp)
      {
         const int FirstUser = 8;
         const int LastUser = 31;
         for (var i = FirstUser; i <= LastUser; i++)
         {
            var el = layersProp.GetArrayElementAtIndex(i);
            var s = el is {propertyType: SerializedPropertyType.String} ? el.stringValue : null;
            if (string.IsNullOrEmpty(s))
            {
               return i;
            }
         }

         return -1;
      }

      // ---------- Internals: Tag ops ----------
      private static List<string> EnsureTags(SerializedProperty tagsProp, IEnumerable<string> names)
      {
         var added = new List<string>();
         foreach (var name in Normalize(names))
         {
            if (!SerializedStringArrayContains(tagsProp, name))
            {
               var idx = tagsProp.arraySize;
               tagsProp.InsertArrayElementAtIndex(idx);
               tagsProp.GetArrayElementAtIndex(idx).stringValue = name;
               added.Add(name);
            }
         }

         return added;
      }

      private static List<string> Normalize(IEnumerable<string> raw)
      {
         if (raw == null)
         {
            return new List<string>();
         }

         var seen = new HashSet<string>(StringComparer.Ordinal);
         var list = new List<string>();
         foreach (var r in raw)
         {
            var v = (r ?? string.Empty).Trim();
            if (v.Length == 0)
            {
               continue;
            }

            if (seen.Add(v))
            {
               list.Add(v);
            }
         }

         return list;
      }

      // Clears any user slot [8..31] whose name is not in desired set.
      private static List<string> RemoveLayersNotIn(SerializedProperty layersProp, HashSet<string> desired)
      {
         var removed = new List<string>();
         const int FirstUser = 8;
         const int LastUser = 31;

         for (var i = FirstUser; i <= LastUser; i++)
         {
            var el = layersProp.GetArrayElementAtIndex(i);
            if (el.propertyType != SerializedPropertyType.String)
            {
               continue;
            }

            var name = el.stringValue;
            if (string.IsNullOrEmpty(name))
            {
               continue;
            }

            if (desired.Contains(name))
            {
               continue;
            }

            el.stringValue = string.Empty;
            removed.Add(name);
         }

         return removed;
      }

      // Removes any non-default tag not present in desired set.
      private static List<string> RemoveTagsNotIn(SerializedProperty tagsProp, HashSet<string> desired)
      {
         var removed = new List<string>();
         for (var i = tagsProp.arraySize - 1; i >= 0; i--)
         {
            var el = tagsProp.GetArrayElementAtIndex(i);
            if (el.propertyType != SerializedPropertyType.String)
            {
               continue;
            }

            var name = el.stringValue;
            if (string.IsNullOrEmpty(name))
            {
               continue;
            }

            if (DefaultTags.Contains(name))
            {
               continue;
            }

            if (desired.Contains(name))
            {
               continue;
            }

            tagsProp.DeleteArrayElementAtIndex(i);
            removed.Add(name);
         }

         removed.Reverse();
         return removed;
      }

      private static SerializedObject LoadTagManager()
      {
         var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
         if (assets == null || assets.Length == 0)
         {
            return null;
         }

         return new SerializedObject(assets[0]);
      }

      // ---------- Internals: Load/Normalize ----------
      private static Store LoadStore()
      {
         try
         {
            if (!File.Exists(STORE_PATH))
            {
               return new Store();
            }

            var json = File.ReadAllText(STORE_PATH);
            var s = JsonUtility.FromJson<Store>(json) ?? new Store();
            s.tags = Normalize(s.tags);
            s.layers = Normalize(s.layers);
            return s;
         }
         catch
         {
            return new Store();
         }
      }

      // ---------- DTO ----------
      [Serializable]
      private sealed class Store
      {
         public List<string> tags = new();

         public List<string> layers = new();
      }
   }
}
