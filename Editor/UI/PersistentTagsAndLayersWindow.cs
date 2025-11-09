namespace P3k.UnityEditorTools.UI
{
   using P3k.UnityEditorTools.Tools;

   using System;
   using System.Collections.Generic;
   using System.Linq;

   using UnityEditor;

   using UnityEngine;

   public sealed class PersistentTagsAndLayersWindow : EditorWindow
   {
      private readonly List<string> _layers = new List<string>(32);

      // ---------- Data ----------
      private readonly List<string> _tags = new List<string>(32);

      private Vector2 _scrollLayers;

      // ---------- UI ----------
      private Vector2 _scrollTags;

      private void OnEnable()
      {
         LoadData();
      }

      private void OnGUI()
      {
         DrawToolbar();

         var rect = position;
         var split = Mathf.Floor(rect.width * 0.5f);

         EditorGUILayout.BeginHorizontal();

         // Left: Tags
         EditorGUILayout.BeginVertical(GUILayout.Width(split));
         EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
         DrawColumnHeader("Tag");
         _scrollTags = EditorGUILayout.BeginScrollView(_scrollTags);
         DrawEditableList(_tags, "Tag");
         EditorGUILayout.EndScrollView();
         if (GUILayout.Button("Add Tag", GUILayout.Height(22f))) _tags.Add(string.Empty);
         EditorGUILayout.EndVertical();

         // Right: Layers
         EditorGUILayout.BeginVertical(GUILayout.Width(split));
         EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
         DrawColumnHeader("Layer");
         _scrollLayers = EditorGUILayout.BeginScrollView(_scrollLayers);
         DrawEditableList(_layers, "Layer");
         EditorGUILayout.EndScrollView();
         if (GUILayout.Button("Add Layer", GUILayout.Height(22f))) _layers.Add(string.Empty);
         EditorGUILayout.EndVertical();

         EditorGUILayout.EndHorizontal();
      }

      // ---------- ShowWindow ----------
      [MenuItem("Window/P3k's Persistent Tags & Layers Editor")]
      public static void ShowWindow()
      {
         var w = GetWindow<PersistentTagsAndLayersWindow>(true, "Persistent Tags & Layers");
         w.minSize = new Vector2(520, 360);
         w.LoadData();
         w.Show();
      }

      private void LoadData()
      {
         _tags.Clear();
         _layers.Clear();
         _tags.AddRange(TagAndLayerSync.LoadPersistentTags());
         _layers.AddRange(TagAndLayerSync.LoadPersistentLayers());
         if (_tags.Count == 0) _tags.Add(string.Empty);
         if (_layers.Count == 0) _layers.Add(string.Empty);
      }

      private void DrawToolbar()
      {
         using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
         {
            if (GUILayout.Button("Save and Close", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
               SaveAndApplyThenClose();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
               LoadData();
               Repaint();
            }
         }

         GUILayout.Space(6f);
      }

      private static void DrawColumnHeader(string colName)
      {
         var r = EditorGUILayout.GetControlRect(false, 18f);
         EditorGUI.DrawRect(r, new Color(0, 0, 0, EditorGUIUtility.isProSkin ? 0.25f : 0.1f));
         var label = new Rect(r.x + 6, r.y, r.width - 12, r.height);
         EditorGUI.LabelField(label, colName, EditorStyles.miniBoldLabel);
      }

      private void DrawEditableList(List<string> list, string placeholder)
      {
         for (var i = 0; i < list.Count; i++)
         {
            using (new EditorGUILayout.HorizontalScope())
            {
               var val = list[i] ?? string.Empty;
               var newVal = EditorGUILayout.TextField(val);
               if (!ReferenceEquals(newVal, val)) list[i] = newVal;

               if (GUILayout.Button("−", GUILayout.Width(24)))
               {
                  list.RemoveAt(i);
                  i--;
               }
            }
         }
      }

      private void SaveAndApplyThenClose()
      {
         var tags = Normalize(_tags);
         var layers = Normalize(_layers);

         TagAndLayerSync.SavePersistent(tags, layers);
         TagAndLayerSync.SyncPersistent();

         Close();
      }

      private static List<string> Normalize(IEnumerable<string> raw)
      {
         return raw.Select(s => (s ?? string.Empty).Trim()).Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal).ToList();
      }
   }
}