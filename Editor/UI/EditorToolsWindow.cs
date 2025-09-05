#if UNITY_EDITOR
   namespace P3k.UnityEditorTools.UI
{
   using P3k.UnityEditorTools.Tools;

   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;

   using UnityEditor;

   using UnityEngine;

   public class EditorToolsWindow : EditorWindow
   {
      // ---------- Visual Constants ----------
      // Removed constant HEADER_H in favor of dynamic header height.
      private const float PAD = 6f;

      private const float ROW_H = 20f;

      private const float INDENT_W = 16f;

      private const float DIVIDER_THICKNESS = 6f;

      private static readonly Color RowEven = new Color(0.24f, 0.24f, 0.24f);

      private static readonly Color RowOdd = new Color(0.18f, 0.18f, 0.18f);

      private static readonly Color TreeBg = new Color(0.14f, 0.14f, 0.14f);

      // ---------- Styles ----------
      private static bool _stylesInit;

      private static GUIStyle _headerArea;

      private static GUIStyle _headerTitle;

      private static GUIStyle _headerSubLabel;

      private static GUIStyle _btnPrimary;

      private static GUIStyle _btnSecondary;

      private static GUIStyle _btnDisabled;

      // ---------- Persistent UI/Data State ----------
      private readonly Dictionary<string, bool> _expandedItems = new Dictionary<string, bool>();

      private readonly string _rootFolder = "Assets";

      private readonly Dictionary<string, bool> _selectedItems = new Dictionary<string, bool>();

      private readonly Dictionary<string, bool> _selectedScriptItems = new Dictionary<string, bool>();

      private string _fileTypes = ".cs .asset";

      // Dynamic header height computed each frame to fit its content (buttons + labels)
      private float _headerHeight = 124f;

      private Rect _lastBuildBtnRect;

      private FolderNode _rootNode;

      private Vector2 _scrollPos;

      // ---------- Unity ----------
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

         // Compute header height based on available width and content, then draw the header.
         _headerHeight = CalculateHeaderHeight(position.width) + 16;
         DrawHeaderBar(_headerHeight);

         // Draw visual divider directly below the header (non-interactive, no splitter behavior).
         var dividerRect = new Rect(0f, _headerHeight - (DIVIDER_THICKNESS * 0.5f), position.width, DIVIDER_THICKNESS);
         DrawDivider(dividerRect, true);

         // File types row sits immediately beneath the header
         var fileTypeRowY = _headerHeight + PAD;
         DrawFileTypeRow(fileTypeRowY);

         // Tree occupies the remaining space below the file types row
         var treeY = fileTypeRowY + EditorGUIUtility.singleLineHeight + PAD;
         DrawTree(new Rect(PAD, treeY, position.width - (2 * PAD), position.height - (treeY + PAD)));
      }

      // ---------- Entry ----------
      [MenuItem("Window/P3k's Editor Tools")]
      public static void ShowWindow()
      {
         var win = GetWindow<EditorToolsWindow>(true, "P3k's Editor Tools");
         win.minSize = new Vector2(290, 600);
      }

      // ---------- Header / Actions ----------
      // Changes:
      // 1) No scroll view; height expands to fit all content.
      // 2) Visual divider is rendered just beneath this area (outside, in OnGUI()).
      // ---------- Header / Actions ----------
      private void DrawHeaderBar(float headerH)
      {
         // Background
         var rect = new Rect(0f, 0f, position.width, headerH);
         var bg = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f);
         EditorGUI.DrawRect(rect, bg);

         // Begin size-fixed (but dynamically computed) area
         GUILayout.BeginArea(rect, _headerArea);

         GUILayout.BeginVertical();

         GUILayout.Space(6f);
         GUILayout.Label("Random Editor Functions", _headerTitle);
         GUILayout.Label("Primary actions wrap automatically", _headerSubLabel);
         GUILayout.Space(6f);

         // Collect button definitions (added: Refresh Directory)
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
                                      if (GUILayout.Button(
                                          "Create Empty Github Package",
                                          _btnPrimary,
                                          GUILayout.Height(24f),
                                          GUILayout.MinWidth(180f)))
                                      {
                                         var baseDefault = EditorPrefs.GetString("P3k_Package_BaseName", "RepoName");
                                         var authorDefault = EditorPrefs.GetString("P3k_Package_Author", "You");
                                         var versionDefault = EditorPrefs.GetString("P3k_Package_Version", "0.1.0");
                                         var repoDefault = EditorPrefs.GetString(
                                         "P3k_Git_StandardRepo",
                                         "git@github.com:p3k22/");

                                         var anchor = new Rect(position.width * 0.5f, _headerHeight, 0f, 0f);
                                         PopupWindow.Show(
                                         anchor,
                                         new GithubPackageCreatorPopup(
                                         baseDefault,
                                         authorDefault,
                                         versionDefault,
                                         repoDefault,
                                         (baseName, author, version, gitStandardRepo) =>
                                            {
                                               GithubPackageCreator.Create(baseName, author, version, gitStandardRepo);
                                            }));
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

         var anySelected = _selectedScriptItems.Values.Any(v => v);
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

         // Lay out buttons with wrapping, no scrollbars; rows are created as needed
         var availableWidth = position.width - 20f; // headerArea padding (10 left + 10 right)
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

      // Computes the header height necessary to fit all content without scrollbars.
      private static float CalculateHeaderHeight(float windowWidth)
      {
         // Ensure styles so CalcHeight is valid
         EnsureStyles();

         // Title + sub label heights
         var titleH = _headerTitle.CalcHeight(new GUIContent("Random Editor Functions"), windowWidth);
         var subH = _headerSubLabel.CalcHeight(new GUIContent("Primary actions wrap automatically"), windowWidth);

         // Button expected widths (aligned with MinWidth in DrawHeaderBar)
         // Order must match the actual buttons:
         // Import (140), Create Package (180), Build Bundles (150), Export Selected (140), Sort Namespaces (140)
         var buttonWidths = new List<float>
                               {
                                  140f,
                                  180f,
                                  150f,
                                  140f,
                                  140f
                               };

         // Available inner width inside the header area
         var availableWidth = Mathf.Max(0f, windowWidth - 20f); // headerArea: padding 10 on each side
         var rows = 1;
         var rowWidth = 0f;

         foreach (var bw in buttonWidths)
         {
            var needed = rowWidth == 0f ? bw : bw + 6f;
            if (rowWidth + needed > availableWidth)
            {
               rows++;
               rowWidth = bw; // first in new row
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

      // ---------- Styles ----------
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

      // ---------- File Type Row ----------
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

      // ---------- Tree ----------
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

      // ---------- Divider (non-interactive) ----------
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

      // ---------- Tree Helpers ----------
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

      private bool AreAllScriptsSelectedInFolder(FolderNode node)
      {
         return node.Scripts.All(s => _selectedScriptItems[s]) && node.Items.Values.All(AreAllScriptsSelectedInFolder);
      }

      private struct RowItem
      {
         public bool IsFolder;

         public FolderNode Node;

         public string ScriptPath;

         public int Indent;
      }
   }

   internal sealed class GithubPackageCreatorPopup : PopupWindowContent
   {
      private readonly Action<string, string, string, string> _onConfirm;

      private string _author;

      private string _baseName;

      private string _gitStandardRepo;

      private string _version;

      public GithubPackageCreatorPopup(
         string baseNameDefault,
         string authorDefault,
         string versionDefault,
         string gitStandardRepoDefault,
         Action<string, string, string, string> onConfirm)
      {
         _baseName = string.IsNullOrWhiteSpace(baseNameDefault) ? "RepoName" : baseNameDefault;
         _author = string.IsNullOrWhiteSpace(authorDefault) ? "You" : authorDefault;
         _version = string.IsNullOrWhiteSpace(versionDefault) ? "0.1.0" : versionDefault;
         _gitStandardRepo = string.IsNullOrWhiteSpace(gitStandardRepoDefault) ?
                               "git@github.com:YourGithubUserName/" :
                               gitStandardRepoDefault;
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

               EditorPrefs.SetString("P3k_Package_BaseName", "RepoName");
               EditorPrefs.SetString("P3k_Package_Author", author);
               EditorPrefs.SetString("P3k_Package_Version", version);
               EditorPrefs.SetString("P3k_Git_StandardRepo", gitStandardRepo);

               _onConfirm?.Invoke(baseName, author, version, gitStandardRepo);
               editorWindow.Close();
            }
         }
      }

      public override Vector2 GetWindowSize()
      {
         return new Vector2(420f, 180f);
      }
   }

   // Purpose: Compact text-entry popup used for both bundle extension and namespace exclusion prompts.
   internal sealed class TextPromptPopup : PopupWindowContent
   {
      private readonly Action<string> _onConfirm;

      private readonly string _label;

      private readonly string _message;

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