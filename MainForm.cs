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

        private MetadataStore store = new();
        private string currentFolder = "";

        private static readonly string[] PokemonExtensions = new[]
        {
            ".pk1", ".pk2", ".pk3", ".pk4", ".pk5", ".pk6", ".pk7", ".pk8",
            ".bk4", ".ck3", ".pa8", ".pb7", ".pb8", ".pkm", ".rk4", ".sk2", ".xk3"
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

            commentBox.Top = 40;
            commentBox.Left = 320;
            commentBox.Width = 650;
            commentBox.Height = 60;
            commentBox.Multiline = true;
            commentBox.PlaceholderText = "Commentaire";

            saveButton.Text = "💾 Sauvegarder";
            saveButton.Left = 320;
            saveButton.Top = 110;
            saveButton.Click += SaveButton_Click;

            searchBox.Top = 150;
            searchBox.Left = 320;
            searchBox.Width = 650;
            searchBox.PlaceholderText = "Rechercher par tag ou commentaire...";
            searchBox.TextChanged += SearchBox_TextChanged;

            Controls.Add(fileTreeView);
            Controls.Add(tagBox);
            Controls.Add(commentBox);
            Controls.Add(saveButton);
            Controls.Add(searchBox);

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
        }

        private TreeNode CreateDirectoryNode(DirectoryInfo dir)
        {
            var node = new TreeNode(dir.Name);
            // Ajoute les fichiers Pokémon dans ce dossier
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                    node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
            }
            // Ajoute les sous-dossiers
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
            var fileName = Path.GetFileName(filePath);
            var meta = store.GetOrCreate(fileName);
            tagBox.Text = string.Join(", ", meta.Tags);
            commentBox.Text = meta.Comment;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            var filePath = fileTreeView.SelectedNode.Tag.ToString()!;
            var fileName = Path.GetFileName(filePath);
            var meta = store.GetOrCreate(fileName);
            meta.Tags = tagBox.Text.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            meta.Comment = commentBox.Text;
            store.Save(currentFolder);
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
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
            // Fichiers
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                {
                    var fileName = file.Name.ToLower();
                    var meta = store.GetOrCreate(file.Name);
                    bool match = fileName.Contains(query) ||
                        meta.Tags.Any(tag => tag.ToLower().Contains(query)) ||
                        meta.Comment.ToLower().Contains(query) ||
                        string.IsNullOrEmpty(query);
                    if (match)
                    {
                        node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
                        hasMatch = true;
                    }
                }
            }
            // Sous-dossiers
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
    }
}
