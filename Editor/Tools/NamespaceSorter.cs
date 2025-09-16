namespace P3k.UnityEditorTools.Tools
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Text.RegularExpressions;

   internal static class NamespaceSorter
   {
      internal static void Sort(string scriptAssetPath, IEnumerable<string> excludedTokens)
      {
         var fullPath = Path.Combine(Directory.GetCurrentDirectory(), scriptAssetPath);
         if (!File.Exists(fullPath)) return;

         var folderPath = Path.GetDirectoryName(scriptAssetPath)?.Replace("\\", "/") ?? string.Empty;

         string trimmed = folderPath switch
            {
               "Assets" => string.Empty,
               _ when folderPath.StartsWith("Assets/") => folderPath["Assets/".Length..],
               _ => folderPath
            };

         var newNamespace = string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed.Replace("/", ".");
         newNamespace = SanitizeNamespace(newNamespace, excludedTokens);

         UpdateNamespaceInFile(fullPath, newNamespace);
      }

      internal static string[] ParseExcludedWords(string raw)
      {
         if (string.IsNullOrWhiteSpace(raw))
         {
            return Array.Empty<string>();
         }

         var cleaned = raw.Replace("\"", string.Empty).Replace("'", string.Empty).Trim();

         var tokens = cleaned.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

         return tokens;
      }

      private static string SanitizeNamespace(string ns, IEnumerable<string> excludedTokens)
      {
         if (string.IsNullOrWhiteSpace(ns)) return string.Empty;

         var exclude = new HashSet<string>(
         (excludedTokens ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)),
         StringComparer.OrdinalIgnoreCase);

         var parts = ns.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries).Where(p => !exclude.Contains(p))
            .ToArray();

         return parts.Length == 0 ? string.Empty : string.Join(".", parts);
      }

      private static void UpdateNamespaceInFile(string fullPath, string newNamespace)
      {
         var lines = File.ReadAllLines(fullPath).ToList();
         var nsRegex = new Regex(@"^\s*namespace\s+[^\s{;]+(\s*[{;])?", RegexOptions.Compiled);

         for (var i = 0; i < lines.Count; i++)
         {
            if (!nsRegex.IsMatch(lines[i])) continue;

            if (string.IsNullOrEmpty(newNamespace))
            {
               lines[i] = "// namespace removed by tool";
            }
            else
            {
               var isScoped = lines[i].TrimEnd().EndsWith(";");
               lines[i] = isScoped ? $"namespace {newNamespace};" : $"namespace {newNamespace}";
            }

            File.WriteAllLines(fullPath, lines);
            return;
         }
      }
   }
}
