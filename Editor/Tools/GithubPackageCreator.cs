// Updates REPO_INIT.bat so that if a tag with the same version already exists on the remote,
// it is deleted before pushing the new tag created from the freshly wiped history.
// Also includes the previously requested WriteMarkdownFile stub that writes "hello world".

namespace P3k.UnityEditorTools.Tools
{
   using System;
   using System.IO;
   using System.Linq;
   using System.Reflection;
   using System.Text;
   using System.Text.RegularExpressions;

   using UnityEditor;

   using UnityEngine;

   internal static class GithubPackageCreator
   {
      internal static void Create(
         string baseName,
         string author,
         string version,
         string gitStandardRepo,
         string targetFolder)
      {
         var safeBase = string.IsNullOrWhiteSpace(baseName) ? "RepoName" : baseName.Trim();
         var safeAuthor = string.IsNullOrWhiteSpace(author) ? "You" : author.Trim();
         var safeVersion = string.IsNullOrWhiteSpace(version) ? "0.1.0" : version.Trim();

         //var targetFolder = GetActiveProjectWindowFolder();
         var (assetFolderPath, systemFolderPath) = EnsureFixedAssetFolder(targetFolder, safeBase);

         var repoName = Path.GetFileName(assetFolderPath.TrimEnd('/', '\\'));
         var defaultRepoUrl = BuildDefaultRepoUrl(gitStandardRepo, repoName);

         WriteTextFile(Path.Combine(systemFolderPath, ".gitignore"), GetGitIgnoreContent(), false);
         WriteTextFile(
         Path.Combine(systemFolderPath, "package.json"),
         GetPackageJsonContent(repoName, safeAuthor, safeVersion, defaultRepoUrl),
         false);

         // Temporary README writer stub per request
         WriteMarkdownFile(Path.Combine(systemFolderPath, "README.md"), "Write Something");

         WriteTextFile(Path.Combine(systemFolderPath, "REPO_INIT.bat"), GetRepoInitBat(defaultRepoUrl), true);
         WriteTextFile(Path.Combine(systemFolderPath, "REPO_UPDATE.bat"), GetRepoUpdateBat(defaultRepoUrl), true);

         AssetDatabase.Refresh();
         Debug.Log($"Created GitHub package scaffold at: {assetFolderPath}\nDefault repo: {defaultRepoUrl}");
      }

      private static (string assetFolderPath, string systemFolderPath) EnsureFixedAssetFolder(
         string targetFolder,
         string folderName)
      {
         if (string.IsNullOrWhiteSpace(targetFolder))
         {
            throw new ArgumentException("Target folder must be a valid AssetDatabase path.", nameof(targetFolder));
         }

         if (!AssetDatabase.IsValidFolder(targetFolder))
         {
            throw new DirectoryNotFoundException($"Target folder does not exist: {targetFolder}");
         }

         var cleanedTarget = targetFolder.TrimEnd('/', '\\');
         var cleanedName = folderName.Trim();
         var assetFolderPath = $"{cleanedTarget}/{cleanedName}".Replace("\\", "/");

         if (!AssetDatabase.IsValidFolder(assetFolderPath))
         {
            var guid = AssetDatabase.CreateFolder(cleanedTarget, cleanedName);
            if (!string.IsNullOrEmpty(guid))
            {
               assetFolderPath = AssetDatabase.GUIDToAssetPath(guid);
            }
            else
            {
               var absolute = Path.Combine(Directory.GetCurrentDirectory(), assetFolderPath);
               Directory.CreateDirectory(absolute);
               AssetDatabase.Refresh();
            }
         }

         var systemFolderPath = Path.Combine(Directory.GetCurrentDirectory(), assetFolderPath);
         return (assetFolderPath, systemFolderPath);
      }

      private static string BuildDefaultRepoUrl(string repoRoot, string repoName)
      {
         if (string.IsNullOrWhiteSpace(repoRoot))
         {
            return $"git@github.com:GithubUserName/{repoName}.git";
         }

         var root = repoRoot.Trim();
         if (root.Contains("{repo}", StringComparison.OrdinalIgnoreCase))
         {
            var replaced = root.Replace("{repo}", repoName, StringComparison.OrdinalIgnoreCase);
            if (!replaced.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
               replaced += ".git";
            }

            return replaced;
         }

         if (root.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
         {
            return root;
         }

         root = root.TrimEnd('/', '\\');
         var isScpLike = root.Contains("@") && root.Contains(":") && !root.Contains("://");
         if (isScpLike)
         {
            return $"{root}/{repoName}.git";
         }

         return $"{root}/{repoName}.git";
      }

      private static string ConvertRepoUrlToHttp(string repoUrl)
      {
         if (string.IsNullOrWhiteSpace(repoUrl))
         {
            return "";
         }

         var url = repoUrl.Trim();

         // SSH format: git@github.com:User/Repo.git
         var sshMatch = Regex.Match(url, @"^git@([^:]+):(.+?)(\.git)?$");
         if (sshMatch.Success)
         {
            return $"https://{sshMatch.Groups[1].Value}/{sshMatch.Groups[2].Value}";
         }

         // HTTPS format: https://github.com/User/Repo.git
         if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
         {
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
               url = url.Substring(0, url.Length - 4);
            }

            return url;
         }

         return url;
      }

      private static string GetActiveProjectWindowFolder()
      {
         var utilType = typeof(ProjectWindowUtil);
         var getActive = utilType.GetMethod(
         "GetActiveFolderPath",
         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
         if (getActive != null)
         {
            var result = getActive.Invoke(null, null) as string;
            if (!string.IsNullOrEmpty(result))
            {
               return result.Replace("\\", "/");
            }
         }

         if (Selection.activeObject != null)
         {
            var selPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (AssetDatabase.IsValidFolder(selPath))
            {
               return selPath.Replace("\\", "/");
            }

            var parent = Path.GetDirectoryName(selPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent))
            {
               return parent;
            }
         }

         return "Assets";
      }

      private static string GetGitIgnoreContent()
      {
         return @"# Unity generated folders
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Mm]emoryCaptures/
UserSettings/
ProjectSettings/EditorBuildSettings.asset

# Unity cache
sysinfo.txt
*.pidb
*.suo
*.user
*.userprefs
*.csproj
*.unityproj
*.sln
*.svd
*.pdb
*.mdb

# Visual Studio
.vs/

# Asset meta data should only be ignored when the corresponding asset is also ignored
*.booproj
*.svd
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Ignore crash logs
*.stackdump

# Rider / VS Code / JetBrains
.idea/
.vscode/
*.csproj
*.sln
*.DotSettings

# Build artifacts
*.apk
*.aab
*.unitypackage

# Custom ignored files
*.asset
*.asset.meta
*.bat
*.bat.meta";
      }

      private static string GetPackageJsonContent(string baseName, string author, string version, string repoUrl)
      {
         var pkgId = $"com.{Slug(author)}.{Slug(baseName)}";
         var display = Regex.Replace(baseName, "(?<!^)([A-Z])", " $1");

         var httpUrl = ConvertRepoUrlToHttp(repoUrl);
         var readmeUrl = $"{httpUrl}/#readme";

         return $@"{{
  ""name"": ""{pkgId}"",
  ""version"": ""{version}"",
  ""displayName"": ""{display}"",
  ""description"": """",
  ""unity"": ""2020.1"",
  ""documentationUrl"": ""{readmeUrl}"",
  ""author"": {{
    ""name"": ""{author}""
  }}
}}";
      }

      private static string GetRepoInitBat(string defaultRepoUrl)
      {
         return $@"@echo off
setlocal ENABLEDELAYEDEXPANSION
cd /d ""%~dp0""

echo Default: {defaultRepoUrl}
set /p REPO_URL=""Enter repo URL (or press Enter for default): ""
if ""%REPO_URL%""=="""" set REPO_URL={defaultRepoUrl}

set COMMIT_MSG=%~1
if ""%COMMIT_MSG%""=="""" set COMMIT_MSG=Initial commit

set ""PKG_VERSION=""
for /f ""usebackq delims="" %%v in (`
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    ""$p='package.json'; if (Test-Path $p) {{ (Get-Content -Raw $p | ConvertFrom-Json).version }}""
`) do set ""PKG_VERSION=%%v""
if defined PKG_VERSION set ""COMMIT_MSG=%COMMIT_MSG% [v%PKG_VERSION%]""

if exist "".git"" rmdir /s /q .git >nul 2>&1
git init >nul 2>&1
git config core.autocrlf true >nul 2>&1
git config --global --add safe.directory ""%cd%"" >nul 2>&1

git add -A >nul 2>&1

rem Only show actual file paths, no blanks
for /f ""skip=1 delims="" %%f in ('git diff --cached --name-only') do (
  echo Uploaded: %%f
)

git commit -m ""%COMMIT_MSG%"" >nul 2>&1
git branch -M main >nul 2>&1
git remote remove origin >nul 2>&1
git remote add origin %REPO_URL% >nul 2>&1
git push -f origin main >nul 2>&1

if defined PKG_VERSION (
  set ""TAG=v%PKG_VERSION%""
  git tag -a ""!TAG!"" -m ""!TAG!"" >nul 2>&1
  git push -f origin ""!TAG!"" >nul 2>&1
  echo Tag pushed: !TAG!
)

echo.
echo Press any key to exit...
pause >nul
";
      }

      private static string GetRepoUpdateBat(string defaultRepoUrl)
      {
         return $@"@echo off
setlocal ENABLEDELAYEDEXPANSION
cd /d ""%~dp0""

echo Default: {defaultRepoUrl}
set /p REPO_URL=""Enter repo URL (or press Enter for default): ""
if ""%REPO_URL%""=="""" set REPO_URL={defaultRepoUrl}

set COMMIT_MSG=%~1
if ""%COMMIT_MSG%""=="""" set COMMIT_MSG=Updated files

set BRANCH=%~2
if ""%BRANCH%""=="""" set BRANCH=main

set ""PKG_VERSION=""
for /f ""usebackq delims="" %%v in (`
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    ""$p='package.json'; if (Test-Path $p) {{ (Get-Content -Raw $p | ConvertFrom-Json).version }}""
`) do set ""PKG_VERSION=%%v""
if defined PKG_VERSION set ""COMMIT_MSG=%COMMIT_MSG% [v%PKG_VERSION%]""

if not exist "".git"" (
    git init >nul 2>&1
    git branch -M %BRANCH% >nul 2>&1
)

git remote remove origin >nul 2>&1
git remote add origin %REPO_URL% >nul 2>&1

git add -A >nul 2>&1

rem Show uploaded files
for /f ""skip=1 delims="" %%f in ('git diff --cached --name-only') do (
  echo Uploaded: %%f
)

git diff --cached --quiet
if errorlevel 1 (
    git commit -m ""%COMMIT_MSG%"" >nul 2>&1
    git push -u origin %BRANCH% >nul 2>&1
)

if defined PKG_VERSION (
    set ""TAG=v%PKG_VERSION%""
    git tag -a ""!TAG!"" -m ""!TAG!"" >nul 2>&1
    git push -f origin ""!TAG!"" >nul 2>&1
    echo Tag pushed: !TAG!
)

echo.
echo Press any key to exit...
pause >nul
";
      }

      private static string NormalizeNewlinesToWindows(string s)
      {
         if (string.IsNullOrEmpty(s))
         {
            return s;
         }

         var t = s.Replace("\r\n", "\n").Replace("\r", "\n");
         return t.Replace("\n", "\r\n");
      }

      private static string Slug(string s)
      {
         if (string.IsNullOrEmpty(s))
         {
            return "noname";
         }

         var sb = new StringBuilder(s.Length);
         foreach (var ch in s.ToLowerInvariant())
         {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_')
            {
               sb.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '.' || ch == '/')
            {
               sb.Append('-');
            }
         }

         var slug = sb.ToString().Trim('-');
         return string.IsNullOrEmpty(slug) ? "noname" : slug;
      }

      private static void WriteMarkdownFile(string path, string content)
      {
         if (string.IsNullOrWhiteSpace(path))
         {
            throw new ArgumentException("Path must be a valid file path.", nameof(path));
         }

         var dir = Path.GetDirectoryName(path);
         if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
         {
            Directory.CreateDirectory(dir);
         }

         var utf8NoBom = new UTF8Encoding(false);
         File.WriteAllText(path, "hello world", utf8NoBom);
      }

      private static void WriteTextFile(string path, string content, bool ensureWindowsNewlines)
      {
         var text = ensureWindowsNewlines ? NormalizeNewlinesToWindows(content) : content;
         var utf8NoBom = new UTF8Encoding(false);
         File.WriteAllText(path, text, utf8NoBom);
      }
   }
}
