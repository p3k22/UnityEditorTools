#if UNITY_EDITOR
   namespace P3k.UnityEditorTools.UI
   {
      using P3k.UnityEditorTools.Tools;

      using System;
      using System.Collections.Generic;
      using System.IO;
      using System.Linq;

      using UnityEditor;
      using UnityEditor.SceneManagement;

      using UnityEngine;
      using UnityEngine.SceneManagement;

      public class EditorToolsWindow : EditorWindow
      {
         private const float DIVIDER_THICKNESS = 6f;

         private const float INDENT_W = 16f;

         // Purpose: Visual constants
         private const float PAD = 6f;

         private const float ROW_H = 20f;

         private static GUIStyle _btnDisabled;

         private static GUIStyle _btnPrimary;

         private static GUIStyle _btnSecondary;

         private static GUIStyle _headerArea;

         private static GUIStyle _headerSubLabel;

         private static GUIStyle _headerTitle;

         // Purpose: Cached styles
         private static bool _stylesInit;

         // Purpose: Row colors
         private static readonly Color RowEven = new Color(0.24f, 0.24f, 0.24f);

         private static readonly Color RowOdd = new Color(0.18f, 0.18f, 0.18f);

         private static readonly Color TreeBg = new Color(0.14f, 0.14f, 0.14f);

         // Purpose: Persistent UI/data state
         private readonly Dictionary<string, bool> _expandedItems = new Dictionary<string, bool>();

         private string _fileTypes = ".cs .asset";

         private float _headerHeight = 124f;

         private Rect _lastBuildBtnRect;

         private readonly string _rootFolder = "Assets";

         private FolderNode _rootNode;

         private Vector2 _scrollPos;

         private readonly Dictionary<string, bool> _selectedItems = new Dictionary<string, bool>();

         private readonly Dictionary<string, bool> _selectedScriptItems = new Dictionary<string, bool>();

         // Purpose: Unity lifecycle
         private void OnEnable()
         {
            RefreshScriptList();
         }

         private void OnGUI()
         {
            EnsureStyles();

            if (_rootNode == null && AssetDatabase.IsValidFolder(_rootFolder))
            {
               RefreshScriptList();
            }

            _headerHeight = CalculateHeaderHeight(position.width) + 16;
            DrawHeaderBar(_headerHeight);

            var dividerRect = new Rect(0f, _headerHeight - (DIVIDER_THICKNESS * 0.5f), position.width, DIVIDER_THICKNESS);
            DrawDivider(dividerRect, true);

            var fileTypeRowY = _headerHeight + PAD;
            DrawFileTypeRow(fileTypeRowY);

            var treeY = fileTypeRowY + EditorGUIUtility.singleLineHeight + PAD;
            DrawTree(new Rect(PAD, treeY, position.width - (2 * PAD), position.height - (treeY + PAD)));
         }

         // Purpose: Menu entry
         [MenuItem("Window/P3k's Editor Tools")]
         public static void ShowWindow()
         {
            var win = GetWindow<EditorToolsWindow>(true, "P3k's Editor Tools");
            win.minSize = new Vector2(290, 600);
         }

         // Purpose: Compute header height for wrapping content
         private static float CalculateHeaderHeight(float windowWidth)
         {
            EnsureStyles();

            var titleH = _headerTitle.CalcHeight(new GUIContent("Random Editor Functions"), windowWidth);
            var subH = _headerSubLabel.CalcHeight(new GUIContent("Primary actions wrap automatically"), windowWidth);

            var buttonWidths = new List<float>
                                  {
                                     140f, // Import From File
                                     180f, // Create Empty Github Package
                                     150f, // Set Persistent Tags and Layers
                                     180f, // Change Scene Lighting
                                     180f, // Set Prefab Scene
                                     150f, // Build AssetBundles
                                     140f, // Export Selected
                                     140f // Sort Namespaces
                                  };

            var availableWidth = Mathf.Max(0f, windowWidth - 20f);
            var rows = 1;
            var rowWidth = 0f;

            foreach (var bw in buttonWidths)
            {
               var needed = rowWidth == 0f ? bw : bw + 6f;
               if (rowWidth + needed > availableWidth)
               {
                  rows++;
                  rowWidth = bw;
               }
               else
               {
                  rowWidth += needed;
               }
            }

            var buttonH = 24f;
            var vertical = 6f + titleH + subH + 6f + (rows * buttonH) + ((rows - 1) * 4f) + _headerArea.padding.top
                           + _headerArea.padding.bottom;

            return Mathf.Max(90f, vertical);
         }

         // Purpose: Non-interactive divider
         private static void DrawDivider(Rect rect, bool horizontal)
         {
            var old = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.08f) : new Color(0, 0, 0, 0.08f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = old;

            if (horizontal)
            {
               var line = new Rect(rect.x, rect.center.y, rect.width, 1);
               EditorGUI.DrawRect(line, new Color(0, 0, 0, EditorGUIUtility.isProSkin ? 0.3f : 0.45f));
            }
            else
            {
               var line = new Rect(rect.center.x, rect.y, 1, rect.height);
               EditorGUI.DrawRect(line, new Color(0, 0, 0, EditorGUIUtility.isProSkin ? 0.3f : 0.45f));
            }
         }

         // Purpose: Init styles
         private static void EnsureStyles()
         {
            if (_stylesInit)
            {
               return;
            }

            _stylesInit = true;

            _headerArea = new GUIStyle {padding = new RectOffset(10, 10, 6, 6)};
            _headerTitle = new GUIStyle(EditorStyles.boldLabel) {fontSize = 12, alignment = TextAnchor.LowerLeft};
            _headerSubLabel = new GUIStyle(EditorStyles.miniLabel) {alignment = TextAnchor.UpperLeft};

            _btnPrimary = new GUIStyle(GUI.skin.button)
                             {
                                fontSize = 11,
                                fontStyle = FontStyle.Bold,
                                fixedHeight = 24f,
                                padding = new RectOffset(10, 10, 4, 4)
                             };

            _btnSecondary = new GUIStyle(GUI.skin.button)
                               {
                                  fontSize = 11, fixedHeight = 24f, padding = new RectOffset(10, 10, 4, 4)
                               };

            _btnDisabled = new GUIStyle(_btnSecondary);
         }

         // Purpose: Find a node by its asset path
         private static FolderNode FindNodeByPath(FolderNode node, string path)
         {
            if (node.Path == path)
            {
               return node;
            }

            foreach (var c in node.Items.Values)
            {
               var f = FindNodeByPath(c, path);
               if (f != null)
               {
                  return f;
               }
            }

            return null;
         }

         // Purpose: Check if all scripts are selected in a folder subtree
         private bool AreAllScriptsSelectedInFolder(FolderNode node)
         {
            return node.Scripts.All(s => _selectedScriptItems[s]) && node.Items.Values.All(AreAllScriptsSelectedInFolder);
         }

         // Purpose: Build visible rows
         private void BuildVisibleRows(FolderNode node, int indent, List<RowItem> rows)
         {
            rows.Add(new RowItem {IsFolder = true, Node = node, Indent = indent});

            if (!_expandedItems.TryGetValue(node.Path, out var isExpanded) || !isExpanded)
            {
               return;
            }

            foreach (var script in node.Scripts.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
               rows.Add(new RowItem {IsFolder = false, ScriptPath = script, Indent = indent + 1});
            }

            foreach (var child in node.Items.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
               BuildVisibleRows(child, indent + 1, rows);
            }
         }

         // Purpose: File type filter row
         private void DrawFileTypeRow(float y)
         {
            var labelW = 80f;
            var rect = new Rect(PAD, y, position.width - (2 * PAD), EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(new Rect(rect.x, rect.y, labelW, rect.height), "File Types:");
            EditorGUI.BeginChangeCheck();
            _fileTypes = EditorGUI.TextField(
            new Rect(rect.x + labelW + 4f, rect.y, rect.width - labelW - 124 - 4f, rect.height),
            _fileTypes);
            if (EditorGUI.EndChangeCheck())
            {
               RefreshScriptList();
            }

            if (GUI.Button(
                new Rect(rect.x + (rect.width - 124), rect.y, 120, rect.height),
                "Refresh Directory",
                EditorStyles.miniButton))
            {
               AssetDatabase.Refresh();
               RefreshScriptList();
               Repaint();
            }
         }

         // Purpose: Draw a folder row
         private void DrawFolderRow(Rect rowRect, FolderNode node, int indent)
         {
            var x = rowRect.x + (indent * INDENT_W);

            var toggleRect = new Rect(x + 2f, rowRect.y + 2f, 16f, rowRect.height - 4f);
            var sel = _selectedItems[node.Path];
            var newSel = EditorGUI.Toggle(toggleRect, sel);
            if (newSel != sel)
            {
               SetFolderSelection(node, newSel);
            }

            var foldRect = new Rect(x + 24f, rowRect.y, rowRect.width - (x + 24f) - 4f, rowRect.height);
            var exp = _expandedItems[node.Path];
            var newExp = EditorGUI.Foldout(foldRect, exp, node.Name, true);
            if (newExp != exp)
            {
               _expandedItems[node.Path] = newExp;
            }
         }

         // Purpose: Header with actions
         private void DrawHeaderBar(float headerH)
         {
            var rect = new Rect(0f, 0f, position.width, headerH);
            var bg = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
            EditorGUI.DrawRect(rect, bg);

            GUILayout.BeginArea(rect, _headerArea);
            GUILayout.BeginVertical();

            GUILayout.Space(6f);
            GUILayout.Label("Random Editor Functions", _headerTitle);
            GUILayout.Label("Primary actions wrap automatically", _headerSubLabel);
            GUILayout.Space(6f);

            var anySelected = _selectedScriptItems.Values.Any(v => v) || _selectedItems.Values.Any(v => v);

            var buttonDefs = new List<Action>
                                {
                                   () =>
                                      {
                                         if (GUILayout.Button(
                                             "Import From File",
                                             _btnPrimary,
                                             GUILayout.Height(24f),
                                             GUILayout.MinWidth(140f)))
                                         {
                                            var path = EditorUtility.OpenFilePanel(
                                            "Import File",
                                            string.Empty,
                                            "cs,txt,asset");
                                            if (!string.IsNullOrEmpty(path))
                                            {
                                               ScriptImporter.ImportFromFile(path, _rootFolder);
                                               RefreshScriptList();
                                            }
                                         }
                                      },
                                   () =>
                                      {
                                         using (new EditorGUI.DisabledGroupScope(!anySelected))
                                         {
                                            if (GUILayout.Button(
                                                "Create Empty Github Package",
                                                _btnPrimary,
                                                GUILayout.Height(24f),
                                                GUILayout.MinWidth(180f)))
                                            {
                                               var baseDefault = EditorPrefs.GetString(
                                               "P3k_Package_BaseName",
                                               "RepoName");
                                               var authorDefault = EditorPrefs.GetString("P3k_Package_Author", "You");
                                               var versionDefault = EditorPrefs.GetString(
                                               "P3k_Package_Version",
                                               "0.1.0");
                                               var repoDefault = EditorPrefs.GetString(
                                               "P3k_Git_StandardRepo",
                                               "git@github.com:p3k22/");

                                               var targetFolder = GetFirstSelectedFolderPath();

                                               var anchor = new Rect(position.width * 0.5f, _headerHeight, 0f, 0f);
                                               PopupWindow.Show(
                                               anchor,
                                               new GithubPackageCreatorPopup(
                                               baseDefault,
                                               authorDefault,
                                               versionDefault,
                                               repoDefault,
                                               targetFolder,
                                               GithubPackageCreator.Create));
                                            }
                                         }
                                      },
                                   () =>
                                      {
                                         if (GUILayout.Button(
                                             "Set Persistent Tags and Layers",
                                             _btnPrimary,
                                             GUILayout.Height(24f),
                                             GUILayout.MinWidth(150f)))
                                         {
                                            PersistentTagsAndLayersWindow.ShowWindow();
                                            Close();
                                         }
                                      },
                                   () =>
                                      {
                                         if (GUILayout.Button(
                                             "Change Scene Lighting",
                                             _btnPrimary,
                                             GUILayout.Height(24f),
                                             GUILayout.MinWidth(180f)))
                                         {
                                            var anchor = new Rect(position.width * 0.6f, _headerHeight, 0f, 0f);
                                            PopupWindow.Show(anchor, new SceneLightingPopup());
                                         }
                                      },
                                   () =>
                                      {
                                         if (GUILayout.Button(
                                             "Set Prefab Scene",
                                             _btnPrimary,
                                             GUILayout.Height(24f),
                                             GUILayout.MinWidth(180f)))
                                         {
                                            var anchor = new Rect(position.width * 0.6f, _headerHeight, 0f, 0f);
                                            PopupWindow.Show(anchor, new PrefabScenePopup());
                                         }
                                      },
                                   () =>
                                      {
                                         if (GUILayout.Button(
                                             "Build AssetBundles",
                                             _btnPrimary,
                                             GUILayout.Height(24f),
                                             GUILayout.MinWidth(150f)))
                                         {
                                            _lastBuildBtnRect = GUILayoutUtility.GetLastRect();
                                            var defaultExt = EditorPrefs.GetString("P3k_ModBundle_Ext", ".modbundle");
                                            var anchor = new Rect(_lastBuildBtnRect.x, _headerHeight, 0f, 0f);

                                            PopupWindow.Show(
                                            anchor,
                                            new TextPromptPopup(
                                            "Custom Bundle Extension",
                                            "Enter a file extension for built bundles.",
                                            "Extension",
                                            defaultExt,
                                            ext =>
                                               {
                                                  var normalized = BuildAssetBundles.NormalizeExtension(ext);
                                                  if (!BuildAssetBundles.IsValidExtension(normalized, out var error))
                                                  {
                                                     EditorUtility.DisplayDialog("Invalid Extension", error, "OK");
                                                     return;
                                                  }

                                                  EditorPrefs.SetString("P3k_ModBundle_Ext", normalized);

                                                  BuildAssetBundles.BuildAllBundles(
                                                  "Assets/AssetBundles",
                                                  normalized,
                                                  true);
                                               }));
                                         }
                                      }
                                };

            var exportStyle = anySelected ? _btnSecondary : _btnDisabled;
            var sortStyle = anySelected ? _btnSecondary : _btnDisabled;

            buttonDefs.Add(() =>
               {
                  using (new EditorGUI.DisabledGroupScope(!anySelected))
                  {
                     if (GUILayout.Button("Export Selected", exportStyle, GUILayout.Height(24f), GUILayout.MinWidth(140f)))
                     {
                        ScriptExporter.ExportSelectedScripts(
                        _selectedScriptItems.Where(kv => kv.Value).Select(kv => kv.Key),
                        @"C:\UnityScriptExports",
                        Application.productName);
                     }
                  }
               });

            buttonDefs.Add(() =>
               {
                  using (new EditorGUI.DisabledGroupScope(!anySelected))
                  {
                     if (GUILayout.Button("Sort Namespaces", sortStyle, GUILayout.Height(24f), GUILayout.MinWidth(140f)))
                     {
                        var defaultList = EditorPrefs.GetString("P3k_Namespace_Excluded", ".Scripts");
                        var anchor = new Rect(position.width - 300f, _headerHeight, 0f, 0f);

                        PopupWindow.Show(
                        anchor,
                        new TextPromptPopup(
                        "Sort Namespaces",
                        "Enter namespace parts (e.g., .Scripts.Editor.Core) to omit from updated namespace",
                        "To Omit:",
                        defaultList,
                        raw =>
                           {
                              var cleaned = raw ?? string.Empty;
                              cleaned = cleaned.Trim();

                              EditorPrefs.SetString("P3k_Namespace_Excluded", cleaned);

                              var tokens = NamespaceSorter.ParseExcludedWords(cleaned);

                              foreach (var scriptPath in _selectedScriptItems.Where(kv => kv.Value).Select(kv => kv.Key))
                              {
                                 NamespaceSorter.Sort(scriptPath, tokens);
                              }

                              AssetDatabase.Refresh();
                           }));
                     }
                  }
               });

            var availableWidth = position.width - 20f;
            var currentRowWidth = 0f;

            GUILayout.BeginHorizontal();
            foreach (var drawBtn in buttonDefs)
            {
               var expectedButtonWidth = 150f;

               if (currentRowWidth + expectedButtonWidth > availableWidth)
               {
                  GUILayout.EndHorizontal();
                  GUILayout.Space(4f);
                  GUILayout.BeginHorizontal();
                  currentRowWidth = 0f;
               }

               drawBtn.Invoke();
               GUILayout.Space(6f);
               currentRowWidth += expectedButtonWidth + 6f;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
         }

         // Purpose: Draw a script row
         private void DrawScriptRow(Rect rowRect, string scriptPath, int indent)
         {
            var x = rowRect.x + (indent * INDENT_W) + 20f;

            var tRect = new Rect(x + 2f, rowRect.y + 2f, 16f, rowRect.height - 4f);
            var sel = _selectedScriptItems[scriptPath];
            var newSel = EditorGUI.Toggle(tRect, sel);
            if (newSel != sel)
            {
               UpdateFolderSelectionForScript(scriptPath, newSel);
            }

            var lRect = new Rect(x + 24f, rowRect.y, rowRect.width - (x + 24f) - 4f, rowRect.height);
            EditorGUI.LabelField(lRect, Path.GetFileName(scriptPath));
         }

         // Purpose: Scripts tree
         private void DrawTree(Rect treeRect)
         {
            EditorGUI.DrawRect(treeRect, TreeBg);

            if (_rootNode == null)
            {
               var warnRect = new Rect(treeRect.x + PAD, treeRect.y + PAD, treeRect.width - (2 * PAD), 40f);
               EditorGUI.HelpBox(warnRect, $"Invalid folder: {_rootFolder}", MessageType.Warning);
               return;
            }

            var rows = new List<RowItem>(256);
            BuildVisibleRows(_rootNode, 0, rows);

            var contentRect = new Rect(0f, 0f, treeRect.width, Mathf.Max(rows.Count * ROW_H, treeRect.height));

            _scrollPos = GUI.BeginScrollView(treeRect, _scrollPos, contentRect, false, true);

            for (var i = 0; i < rows.Count; i++)
            {
               var r = rows[i];
               var rowRect = new Rect(0f, i * ROW_H, contentRect.width, ROW_H);

               EditorGUI.DrawRect(rowRect, i % 2 == 0 ? RowEven : RowOdd);

               if (r.IsFolder)
               {
                  DrawFolderRow(rowRect, r.Node, r.Indent);
               }
               else
               {
                  DrawScriptRow(rowRect, r.ScriptPath, r.Indent);
               }
            }

            GUI.EndScrollView();
         }

         // Purpose: Resolve a target folder from current tree selection
         private string GetFirstSelectedFolderPath()
         {
            var scriptFolder = _selectedScriptItems.Where(kv => kv.Value).Select(kv => Path.GetDirectoryName(kv.Key))
               .Where(p => !string.IsNullOrEmpty(p)).Select(p => p.Replace("\\", "/")).FirstOrDefault();

            if (!string.IsNullOrEmpty(scriptFolder))
            {
               return scriptFolder;
            }

            // Get the first selected folder (not the deepest)
            var folder = _selectedItems.Where(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();

            if (!string.IsNullOrEmpty(folder))
            {
               return folder;
            }

            return _rootFolder;
         }

         // Purpose: Refresh tree content
         private void RefreshScriptList()
         {
            _selectedItems.Clear();
            _expandedItems.Clear();
            _selectedScriptItems.Clear();

            if (!AssetDatabase.IsValidFolder(_rootFolder))
            {
               _rootNode = null;
               return;
            }

            _rootNode = new FolderNode(Path.GetFileName(_rootFolder), _rootFolder);
            _expandedItems[_rootFolder] = true;
            _selectedItems[_rootFolder] = false;

            var types = _fileTypes.Split(new[] {' ', ',', ';'}, StringSplitOptions.RemoveEmptyEntries)
               .Select(t => t.Trim().ToLower()).Where(t => t.StartsWith(".")).ToList();

            var systemRoot = Path.Combine(Directory.GetCurrentDirectory(), _rootFolder);
            if (!Directory.Exists(systemRoot))
            {
               return;
            }

            var files = Directory.GetFiles(systemRoot, "*.*", SearchOption.AllDirectories)
               .Where(f => types.Contains(Path.GetExtension(f).ToLower()));

            foreach (var file in files)
            {
               var relative = file.Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, string.Empty);
               var assetPath = relative.Replace(Path.DirectorySeparatorChar, '/');
               var folderPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");

               if (folderPath != null)
               {
                  var parts = folderPath[_rootFolder.Length..].Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                  var current = _rootNode;
                  var currentPath = _rootFolder;

                  foreach (var part in parts)
                  {
                     currentPath = currentPath + "/" + part;

                     if (!current.Items.ContainsKey(part))
                     {
                        current.Items[part] = new FolderNode(part, currentPath);
                        _expandedItems[currentPath] = false;
                        _selectedItems[currentPath] = false;
                     }

                     current = current.Items[part];
                  }

                  current.Scripts.Add(assetPath);
               }

               _selectedScriptItems[assetPath] = false;
            }
         }

         // Purpose: Apply selection to an entire folder subtree
         private void SetFolderSelection(FolderNode node, bool sel)
         {
            _selectedItems[node.Path] = sel;

            foreach (var f in node.Scripts)
            {
               _selectedScriptItems[f] = sel;
            }

            foreach (var c in node.Items.Values)
            {
               SetFolderSelection(c, sel);
            }
         }

         // Purpose: Propagate script selection back up to folders
         private void UpdateFolderSelectionForScript(string script, bool isSel)
         {
            _selectedScriptItems[script] = isSel;

            var fp = Path.GetDirectoryName(script)?.Replace("\\", "/");
            while (!string.IsNullOrEmpty(fp))
            {
               var node = FindNodeByPath(_rootNode, fp);
               if (node == null)
               {
                  break;
               }

               _selectedItems[fp] = node.Scripts.All(s => _selectedScriptItems[s])
                                    && node.Items.Values.All(AreAllScriptsSelectedInFolder);

               if (fp == _rootFolder)
               {
                  break;
               }

               var idx = fp.LastIndexOf('/');
               fp = idx > 0 ? fp[..idx] : _rootFolder;
            }
         }

         private struct RowItem
         {
            public int Indent;

            public bool IsFolder;

            public FolderNode Node;

            public string ScriptPath;
         }
      }

      internal sealed class SceneLightingPopup : PopupWindowContent
      {
         private PitchBlackLightingSettings _s;

         public SceneLightingPopup()
         {
            _s = new PitchBlackLightingSettings
                    {
                       EnvLighting = EnvLightingSource.Color,
                       AmbientColor = Color.black,
                       AmbientSky = Color.black,
                       AmbientEquator = Color.black,
                       AmbientGround = Color.black,
                       SkyboxMaterial = null,
                       SubtractiveShadowColor = Color.black,
                       ReflectionSource = EnvReflectionSource.Skybox,
                       CustomReflection = null,
                       ReflectionIntensity = 0f,
                       ReflectionBounces = 1,
                       DisableFog = true,
                       ClearSkyboxWhenNotSky = true,
                       DisableRealtimeGI = true,
                       DisableBakedGI = true
                    };
         }

         public override void OnGUI(Rect rect)
         {
            EditorGUILayout.LabelField("Change Scene Lighting", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _s.EnvLighting = (EnvLightingSource) EditorGUILayout.EnumPopup("Environment Lighting", _s.EnvLighting);
            switch (_s.EnvLighting)
            {
               case EnvLightingSource.Color:
                  _s.AmbientColor = EditorGUILayout.ColorField("Ambient Color", _s.AmbientColor);
                  break;
               case EnvLightingSource.Gradient:
                  _s.AmbientSky = EditorGUILayout.ColorField("Ambient Sky", _s.AmbientSky);
                  _s.AmbientEquator = EditorGUILayout.ColorField("Ambient Equator", _s.AmbientEquator);
                  _s.AmbientGround = EditorGUILayout.ColorField("Ambient Ground", _s.AmbientGround);
                  break;
               case EnvLightingSource.Skybox:
                  _s.SkyboxMaterial = (Material) EditorGUILayout.ObjectField(
                  "Skybox",
                  _s.SkyboxMaterial,
                  typeof(Material),
                  false);
                  break;
            }

            _s.SubtractiveShadowColor = EditorGUILayout.ColorField("Realtime Shadow Color", _s.SubtractiveShadowColor);

            _s.ReflectionSource =
               (EnvReflectionSource) EditorGUILayout.EnumPopup("Reflections Source", _s.ReflectionSource);
            if (_s.ReflectionSource == EnvReflectionSource.Custom)
            {
               _s.CustomReflection = (Cubemap) EditorGUILayout.ObjectField(
               "Custom Cubemap",
               _s.CustomReflection,
               typeof(Cubemap),
               false);
            }

            _s.ReflectionIntensity = EditorGUILayout.Slider("Reflection Intensity", _s.ReflectionIntensity, 0f, 1f);
            _s.ReflectionBounces = EditorGUILayout.IntSlider("Reflection Bounces", _s.ReflectionBounces, 1, 5);

            _s.DisableFog = EditorGUILayout.Toggle("Disable Fog", _s.DisableFog);
            _s.ClearSkyboxWhenNotSky = EditorGUILayout.Toggle("Clear Skybox if Not Sky", _s.ClearSkyboxWhenNotSky);
            //_s.DisableRealtimeGI = EditorGUILayout.Toggle("Disable Realtime GI", _s.DisableRealtimeGI);
            //_s.DisableBakedGI = EditorGUILayout.Toggle("Disable Baked GI", _s.DisableBakedGI);

            //wanker
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
               GUILayout.FlexibleSpace();
               if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  editorWindow.Close();
               }

               if (GUILayout.Button("OK", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  PitchBlackLightingTool.Apply(_s, true);
                  editorWindow.Close();
               }
            }
         }

         public override Vector2 GetWindowSize()
         {
            return new Vector2(360f, 420f);
         }
      }

      internal sealed class GithubPackageCreatorPopup : PopupWindowContent
      {
         // Purpose: Input fields
         private string _author;

         private string _baseName;

         private string _gitStandardRepo;

         // Purpose: Callback to create package
         private readonly Action<string, string, string, string, string> _onConfirm;

         private string _targetFolder;

         private string _version;

         public GithubPackageCreatorPopup(
            string baseNameDefault,
            string authorDefault,
            string versionDefault,
            string gitStandardRepoDefault,
            string targetFolder,
            Action<string, string, string, string, string> onConfirm)
         {
            _baseName = string.IsNullOrWhiteSpace(baseNameDefault) ? "RepoName" : baseNameDefault;
            _author = string.IsNullOrWhiteSpace(authorDefault) ? "You" : authorDefault;
            _version = string.IsNullOrWhiteSpace(versionDefault) ? "0.1.0" : versionDefault;
            _gitStandardRepo = string.IsNullOrWhiteSpace(gitStandardRepoDefault) ?
                                  "git@github.com:YourGithubUserName/" :
                                  gitStandardRepoDefault;
            _targetFolder = string.IsNullOrWhiteSpace(targetFolder) ? "Assets" : targetFolder;
            _onConfirm = onConfirm;
         }

         public override void OnGUI(Rect rect)
         {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Create Empty GitHub Package", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fields are remembered for next time.", EditorStyles.miniLabel);
            GUILayout.Space(6f);

            _baseName = EditorGUILayout.TextField("baseName", _baseName);
            _author = EditorGUILayout.TextField("author", _author);
            _version = EditorGUILayout.TextField("version", _version);
            _gitStandardRepo = EditorGUILayout.TextField("gitstandardRepo", _gitStandardRepo);
            EditorGUILayout.LabelField("targetFolder", _targetFolder);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
               GUILayout.FlexibleSpace();

               if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  editorWindow.Close();
               }

               if (GUILayout.Button("Create", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  var baseName = (_baseName ?? "RepoName").Trim();
                  var author = (_author ?? "You").Trim();
                  var version = (_version ?? "0.1.0").Trim();
                  var gitStandardRepo = (_gitStandardRepo ?? "git@github.com:GithubUserName/").Trim();
                  var targetFolder = _targetFolder ?? "Assets";

                  EditorPrefs.SetString("P3k_Package_BaseName", baseName);
                  EditorPrefs.SetString("P3k_Package_Author", author);
                  EditorPrefs.SetString("P3k_Package_Version", version);
                  EditorPrefs.SetString("P3k_Git_StandardRepo", gitStandardRepo);
                  EditorPrefs.SetString("P3k_Package_TargetFolder", targetFolder);

                  _onConfirm?.Invoke(baseName, author, version, gitStandardRepo, targetFolder);
                  editorWindow.Close();
               }
            }
         }

         public override Vector2 GetWindowSize()
         {
            return new Vector2(420f, 200f);
         }
      }

      internal sealed class PrefabScenePopup : PopupWindowContent
      {
         private UnityEngine.Object _scene;

         public override void OnGUI(Rect rect)
         {
            var scenePath = "";
            GUILayout.Space(6f);
            _scene = EditorGUILayout.ObjectField("Use Existing Scene", _scene, typeof(SceneAsset), false);
            GUILayout.Space(4f);
            if (GUILayout.Button("Use Dark Template Scene", GUILayout.Width(160f), GUILayout.Height(22f)))
            {
               var ls = new PitchBlackLightingSettings
                           {
                              EnvLighting = EnvLightingSource.Color,
                              AmbientColor = Color.black,
                              AmbientSky = Color.black,
                              AmbientEquator = Color.black,
                              AmbientGround = Color.black,
                              SkyboxMaterial = null,
                              SubtractiveShadowColor = Color.black,
                              ReflectionSource = EnvReflectionSource.Skybox,
                              CustomReflection = null,
                              ReflectionIntensity = 0f,
                              ReflectionBounces = 1,
                              DisableFog = true,
                              ClearSkyboxWhenNotSky = true,
                              DisableRealtimeGI = true,
                              DisableBakedGI = true
                           };
               if (CreateAssignPrefabEditingSceneWithLighting(ls, out scenePath))
               {
                  SetPrefabScene(scenePath);
                  editorWindow.Close();
               }
            }

            if (GUILayout.Button("Use None", GUILayout.Width(80f), GUILayout.Height(22f)))
            {
               EditorSettings.prefabRegularEnvironment = null;
               EditorSettings.prefabUIEnvironment = null;
               editorWindow.Close();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
               GUILayout.FlexibleSpace();

               if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  editorWindow.Close();
               }

               if (GUILayout.Button("Ok", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  if (_scene)
                  {
                     scenePath = AssetDatabase.GetAssetPath(_scene);
                     SetPrefabScene(scenePath);
                  }

                  Debug.Log($"Assigned {scenePath} as Prefab editing environment (regular & UI).");
                  editorWindow.Close();
               }
            }
         }

         // Creates an empty scene, applies lighting, saves it under Assets/Scenes, assigns to Prefab Environments, restores previous active scene.
         private static bool CreateAssignPrefabEditingSceneWithLighting(
            PitchBlackLightingSettings lighting,
            out string savedPath)
         {
            savedPath = null;

            var folder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(folder))
            {
               AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var prevActive = SceneManager.GetActiveScene();

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!newScene.IsValid())
            {
               return false;
            }

            SceneManager.SetActiveScene(newScene);

            PitchBlackLightingTool.Apply(lighting, saveActiveScene: false);

            var targetPath = $"{folder}/PrefabEnvironment.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
            {
               targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);
            }

            if (!EditorSceneManager.SaveScene(newScene, targetPath))
            {
               // Restore and clean up
               SceneManager.SetActiveScene(prevActive);
               EditorSceneManager.CloseScene(newScene, true);
               return false;
            }

            savedPath = targetPath;

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(savedPath);
            if (sceneAsset == null)
            {
               SceneManager.SetActiveScene(prevActive);
               EditorSceneManager.CloseScene(newScene, true);
               return false;
            }

            SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(newScene, true);

            return true;
         }

         private static void SetPrefabScene(string scenePath)
         {
            // Load the scene asset as SceneAsset
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
               Debug.LogError($"Failed to load SceneAsset at {scenePath}");
               return;
            }

            EditorSettings.prefabRegularEnvironment = sceneAsset;
            EditorSettings.prefabUIEnvironment = sceneAsset;
         }

         public override Vector2 GetWindowSize()
         {
            return new Vector2(420f, 120f);
         }
      }

      internal sealed class TextPromptPopup : PopupWindowContent
      {
         private readonly string _label;

         private readonly string _message;

         // Purpose: Generic OK/cancel text prompt
         private readonly Action<string> _onConfirm;

         private string _text;

         private readonly string _title;

         public TextPromptPopup(string title, string message, string label, string defaultValue, Action<string> onConfirm)
         {
            _title = title;
            _message = message;
            _label = label;
            _text = string.IsNullOrWhiteSpace(defaultValue) ? string.Empty : defaultValue;
            _onConfirm = onConfirm;
         }

         public override void OnGUI(Rect rect)
         {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_message, EditorStyles.miniLabel);
            GUILayout.Space(6f);

            GUI.SetNextControlName("PromptTextField");
            _text = EditorGUILayout.TextField(_label, _text);

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
               GUILayout.FlexibleSpace();

               if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  editorWindow.Close();
               }

               if (GUILayout.Button("OK", GUILayout.Width(90f), GUILayout.Height(22f)))
               {
                  var value = _text?.Trim() ?? string.Empty;
                  _onConfirm?.Invoke(value);
                  editorWindow.Close();
               }
            }

            if (Event.current.type == EventType.Repaint)
            {
               EditorGUI.FocusTextInControl("PromptTextField");
            }
         }

         public override Vector2 GetWindowSize()
         {
            return new Vector2(360f, 120f);
         }
      }
   }
#endif