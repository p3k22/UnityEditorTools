namespace P3k.UnityEditorTools.UI
{
   using System.Collections.Generic;
   using System.Linq;

   /* Simple tree node representing a folder with child folders and scripts */
   internal sealed class FolderNode
   {
      internal readonly string Name;

      internal readonly string Path;

      internal readonly Dictionary<string, FolderNode> Items = new Dictionary<string, FolderNode>();

      internal readonly List<string> Scripts = new List<string>();

      internal FolderNode(string name, string path)
      {
            Name = name;
            Path = path;
         }
      }
   }
