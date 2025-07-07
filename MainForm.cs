using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PokeViewer
{
    public class MainForm : Form
    {
        private TreeView fileTreeView = new();
        private TextBox tagBox = new();
        private TextBox commentBox = new();
        private Button saveButton = new();
        private TextBox searchBox = new();
        private FolderBrowserDialog folderBrowser = new();
        private ComboBox tagComboBox = new();
        private TextBox nameSearchBox = new();
        private Button nameSearchButton = new();
        private Button tagSearchButton = new();

        private MetadataStore store = new();
        private string currentFolder = "";

        private static readonly string[] PokemonExtensions = new[]
        {
            ".pk1", ".pk2", ".pk3", ".pk4", ".pk5", ".pk6", ".pk7", ".pk8", ".pk9",
            ".bk4", ".ck3", ".pa8", ".pb7", ".pb8", ".pkm", ".rk4", ".sk2", ".xk3",
            ".zip"
        };

        public MainForm()
        {
            Text = "PokeViewer";
            Width = 1000;
            Height = 600;

            fileTreeView.Dock = DockStyle.Left;
            fileTreeView.Width = 300;
            fileTreeView.AfterSelect += FileTreeView_AfterSelect;

            tagBox.Top = 10;
            tagBox.Left = 320;
            tagBox.Width = 650;
            tagBox.PlaceholderText = "Tags (séparés par des virgules)";

            tagComboBox.Top = 40;
            tagComboBox.Left = 320;
            tagComboBox.Width = 650;
            tagComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            tagComboBox.SelectedIndexChanged += TagComboBox_SelectedIndexChanged;

            commentBox.Top = 70;
            commentBox.Left = 320;
            commentBox.Width = 650;
            commentBox.Height = 60;
            commentBox.Multiline = true;
            commentBox.PlaceholderText = "Commentaire";

            saveButton.Text = "💾 Sauvegarder";
            saveButton.Left = 320;
            saveButton.Top = 140;
            saveButton.Click += SaveButton_Click;

            searchBox.Top = 180;
            searchBox.Left = 320;
            searchBox.Width = 250;
            searchBox.PlaceholderText = "Rechercher par tag ou commentaire...";

            nameSearchBox.Top = 180;
            nameSearchBox.Left = 650;
            nameSearchBox.Width = 250;
            nameSearchBox.PlaceholderText = "Rechercher par nom de fichier...";

            nameSearchButton.Text = "🔍";
            nameSearchButton.Top = 180;
            nameSearchButton.Left = 900;
            nameSearchButton.Width = 70;
            nameSearchButton.Height = nameSearchBox.Height;
            nameSearchButton.Click += NameSearchButton_Click;

            tagSearchButton.Text = "🔍";
            tagSearchButton.Top = 180;
            tagSearchButton.Left = 570;
            tagSearchButton.Width = 70;
            tagSearchButton.Height = searchBox.Height;
            tagSearchButton.Click += TagSearchButton_Click;

            Controls.Add(fileTreeView);
            Controls.Add(tagBox);
            Controls.Add(tagComboBox);
            Controls.Add(commentBox);
            Controls.Add(saveButton);
            Controls.Add(searchBox);
            Controls.Add(nameSearchBox);
            Controls.Add(nameSearchButton);
            Controls.Add(tagSearchButton);

            Load += (_, _) => LoadFolder();
        }

        private void LoadFolder()
        {
            folderBrowser.Description = "Choisir un dossier contenant des fichiers Pokémon";
            if (folderBrowser.ShowDialog() != DialogResult.OK)
                return;

            currentFolder = folderBrowser.SelectedPath;
            store = MetadataStore.Load(currentFolder);
            RefreshFileTree();
        }

        private void RefreshFileTree()
        {
            fileTreeView.Nodes.Clear();
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
                return;
            var root = new DirectoryInfo(currentFolder);
            var rootNode = CreateDirectoryNode(root);
            fileTreeView.Nodes.Add(rootNode);
            fileTreeView.ExpandAll();
            RefreshTagComboBox();
        }

        private void RefreshTagComboBox()
        {
            var allTags = store.Entries.Values.SelectMany(m => m.Tags).Distinct().OrderBy(t => t).ToList();
            tagComboBox.Items.Clear();
            foreach (var tag in allTags)
                tagComboBox.Items.Add(tag);
        }

        private void TagComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tagComboBox.SelectedItem is string tag)
            {
                searchBox.Text = tag;
                SearchBox_Apply();
            }
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(currentFolder)) return fullPath;
            var uri1 = new Uri(currentFolder.EndsWith(Path.DirectorySeparatorChar.ToString()) ? currentFolder : currentFolder + Path.DirectorySeparatorChar);
            var uri2 = new Uri(fullPath);
            return Uri.UnescapeDataString(uri1.MakeRelativeUri(uri2).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private TreeNode CreateDirectoryNode(DirectoryInfo dir)
        {
            var node = new TreeNode(dir.Name);
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                    node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
            }
            foreach (var subDir in dir.GetDirectories())
            {
                node.Nodes.Add(CreateDirectoryNode(subDir));
            }
            return node;
        }

        private void FileTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            var node = e.Node;
            if (node == null || node.Tag == null)
            {
                tagBox.Text = "";
                commentBox.Text = "";
                return;
            }
            var filePath = node.Tag.ToString()!;
            var relPath = GetRelativePath(filePath);
            var meta = store.GetOrCreate(relPath);
            tagBox.Text = string.Join(", ", meta.Tags);
            commentBox.Text = meta.Comment;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            var filePath = fileTreeView.SelectedNode.Tag.ToString()!;
            var relPath = GetRelativePath(filePath);
            var meta = store.GetOrCreate(relPath);
            meta.Tags = tagBox.Text.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            meta.Comment = commentBox.Text;
            store.Save(currentFolder);
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            // Ne rien faire dynamiquement
        }

        private void SearchBox_Apply()
        {
            var query = searchBox.Text.Trim().ToLower();
            fileTreeView.Nodes.Clear();
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
                return;
            var root = new DirectoryInfo(currentFolder);
            var rootNode = CreateFilteredDirectoryNode(root, query);
            if (rootNode != null)
                fileTreeView.Nodes.Add(rootNode);
            fileTreeView.ExpandAll();
        }

        private TreeNode? CreateFilteredDirectoryNode(DirectoryInfo dir, string query)
        {
            var node = new TreeNode(dir.Name);
            bool hasMatch = false;
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                {
                    var relPath = GetRelativePath(file.FullName);
                    var meta = store.Entries.TryGetValue(relPath, out var m) ? m : null;
                    bool match = (meta != null && (meta.Tags.Any(tag => tag.ToLower().Contains(query)) || meta.Comment.ToLower().Contains(query))) || string.IsNullOrEmpty(query);
                    if (match)
                    {
                        node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
                        hasMatch = true;
                    }
                }
            }
            foreach (var subDir in dir.GetDirectories())
            {
                var subNode = CreateFilteredDirectoryNode(subDir, query);
                if (subNode != null)
                {
                    node.Nodes.Add(subNode);
                    hasMatch = true;
                }
            }
            return hasMatch ? node : null;
        }

        private void NameSearchButton_Click(object? sender, EventArgs e)
        {
            var query = nameSearchBox.Text.Trim().ToLower();
            fileTreeView.Nodes.Clear();
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
                return;
            var root = new DirectoryInfo(currentFolder);
            var rootNode = CreateNameFilteredDirectoryNode(root, query);
            if (rootNode != null)
                fileTreeView.Nodes.Add(rootNode);
            fileTreeView.ExpandAll();
        }

        private TreeNode? CreateNameFilteredDirectoryNode(DirectoryInfo dir, string query)
        {
            var node = new TreeNode(dir.Name);
            bool hasMatch = false;
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                {
                    var fileName = file.Name.ToLower();
                    bool match = fileName.Contains(query) || string.IsNullOrEmpty(query);
                    if (match)
                    {
                        node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
                        hasMatch = true;
                    }
                }
            }
            foreach (var subDir in dir.GetDirectories())
            {
                var subNode = CreateNameFilteredDirectoryNode(subDir, query);
                if (subNode != null)
                {
                    node.Nodes.Add(subNode);
                    hasMatch = true;
                }
            }
            return hasMatch ? node : null;
        }

        private void DeleteFileAndMetadata(string fullPath)
        {
            if (!File.Exists(fullPath)) return;
            File.Delete(fullPath);
            var relPath = GetRelativePath(fullPath);
            store.Delete(relPath);
            store.Save(currentFolder);
            RefreshFileTree();
        }

        private void TagSearchButton_Click(object? sender, EventArgs e)
        {
            SearchBox_Apply();
        }
    }
}
