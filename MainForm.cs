using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

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
        private List<string> filteredIds = new();

        private static readonly string[] PokemonExtensions = new[]
        {
            ".pk1", ".pk2", ".pk3", ".pk4", ".pk5", ".pk6", ".pk7", ".pk8", ".pk9",
            ".bk4", ".ck3", ".pa8", ".pb7", ".pb8", ".pkm", ".rk4", ".sk2", ".xk3",
            ".zip"
        };

        private static readonly string[] SaveExtensions = new[]
        {
            ".sav", ".dsv", ".dat", ".gci", ".raw", ".bin", ".sa1", ".sa2", ".sa3", ".sa4", ".bak"
        };

        public MainForm()
        {
            Text = "PokeViewer";
            Width = 1000;
            Height = 600;

            fileTreeView.Dock = DockStyle.Left;
            fileTreeView.Width = 300;
            fileTreeView.AfterSelect += FileTreeView_AfterSelect;

            tagComboBox.Top = 40;
            tagComboBox.Left = 320;
            tagComboBox.Width = 650;
            tagComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            // tagComboBox.SelectedIndexChanged += TagComboBox_SelectedIndexChanged;
            

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

            Load += (_, _) => OnStartupScan();
        }

        private void OnStartupScan()
        {
            folderBrowser.Description = "Choisir un dossier contenant des fichiers Pokémon";
            if (folderBrowser.ShowDialog() != DialogResult.OK)
                return;

            currentFolder = folderBrowser.SelectedPath;
            store = MetadataStore.Load(currentFolder);

            // Scan toutes les saves pour détecter ajouts, conflits, suppressions
            var allBoxMons = new Dictionary<string, PkxFilesSaveUtil.BoxPokemonInfo>();
            var duplicateIds = new HashSet<string>();
            var foundIds = new HashSet<string>();
            foreach (var savePath in Directory.GetFiles(currentFolder, "*", SearchOption.AllDirectories)
                .Where(f => SaveExtensions.Contains(Path.GetExtension(f).ToLower()) || Path.GetFileName(f).ToLower() == "main"))
            {
                try
                {
                    var mons = PkxFilesSaveUtil.LoadBoxPokemons(savePath);
                    foreach (var mon in mons)
                    {
                        if (allBoxMons.ContainsKey(mon.UniqueID))
                        {
                            duplicateIds.Add(mon.UniqueID);
                        }
                        else
                        {
                            allBoxMons[mon.UniqueID] = mon;
                        }
                        foundIds.Add(mon.UniqueID);
                    }
                }
                catch { }
            }
            var knownIds = store.GetAllKeys();
            var newIds = foundIds.Except(knownIds).ToList();
            var removedIds = knownIds.Except(foundIds).ToList();
            var conflictIds = duplicateIds.ToList();

            // Si au moins un cas, afficher le pop-up
            if (newIds.Count > 0 || removedIds.Count > 0 || conflictIds.Count > 0)
            {
                using (var popup = new StartupPopup(newIds, removedIds, conflictIds, allBoxMons, store))
                {
                    if (popup.ShowDialog() == DialogResult.OK)
                    {
                        filteredIds = popup.GetFilteredIds();
                        // Marquer les nouveaux IDs comme connus dans le store
                        foreach (var id in newIds)
                        {
                            if (!store.GetAllKeys().Contains(id))
                                store.GetOrCreate(id); // Crée une entrée vide
                        }
                        store.Save(currentFolder);
                    }
                }
            }
            else
            {
                filteredIds.Clear();
            }
            RefreshFileTree();
        }

        private void RefreshFileTree()
        {
            fileTreeView.Nodes.Clear();
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
                return;
            var root = new DirectoryInfo(currentFolder);
            var rootNode = CreateDirectoryNodeWithSaves(root);
            fileTreeView.Nodes.Add(rootNode);
            fileTreeView.ExpandAll();
        }

        private TreeNode CreateDirectoryNodeWithSaves(DirectoryInfo dir)
        {
            var node = new TreeNode(dir.Name);
            foreach (var file in dir.GetFiles())
            {
                if (PokemonExtensions.Contains(file.Extension.ToLower()))
                    node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
                else if (SaveExtensions.Contains(file.Extension.ToLower()) || file.Name.ToLower() == "main")
                {
                    var saveNode = new TreeNode("[SAVE] " + file.Name) { Tag = file.FullName };
                    try
                    {
                        var boxMons = PkxFilesSaveUtil.LoadBoxPokemons(file.FullName);
                        var boxGroups = boxMons.GroupBy(x => x.Box);
                        foreach (var box in boxGroups)
                        {
                            var boxNode = new TreeNode($"Boîte {box.Key + 1}");
                            foreach (var mon in box)
                            {
                                if (filteredIds.Count > 0 && !filteredIds.Contains(mon.UniqueID))
                                    continue;
                                var monName = mon.Pkm?.Nickname ?? "?";
                                boxNode.Nodes.Add(new TreeNode($"{monName} (Slot {mon.Slot + 1})") { Tag = mon });
                            }
                            if (boxNode.Nodes.Count > 0)
                                saveNode.Nodes.Add(boxNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        saveNode.Nodes.Add(new TreeNode($"Erreur lecture save: {ex.Message}"));
                    }
                    if (saveNode.Nodes.Count > 0)
                        node.Nodes.Add(saveNode);
                }
            }
            foreach (var subDir in dir.GetDirectories())
            {
                var subNode = CreateDirectoryNodeWithSaves(subDir);
                if (subNode.Nodes.Count > 0)
                    node.Nodes.Add(subNode);
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
            // Si c'est un Pokémon issu d'une save
            if (node.Tag is PkxFilesSaveUtil.BoxPokemonInfo boxMon)
            {
                var meta = store.GetOrCreate(boxMon.UniqueID);
                tagBox.Text = string.Join(", ", meta.Tags);
                commentBox.Text = meta.Comment;
            }
            // Sinon, comportement classique (fichier pkx)
            else if (node.Tag is string filePath)
            {
                var fileName = Path.GetFileName(filePath);
                var meta = store.GetOrCreate(fileName);
                tagBox.Text = string.Join(", ", meta.Tags);
                commentBox.Text = meta.Comment;
            }
            else
            {
                tagBox.Text = "";
                commentBox.Text = "";
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            // Si c'est un Pokémon issu d'une save
            if (fileTreeView.SelectedNode.Tag is PkxFilesSaveUtil.BoxPokemonInfo boxMon)
            {
                var meta = store.GetOrCreate(boxMon.UniqueID);
                meta.Tags = tagBox.Text.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                meta.Comment = commentBox.Text;
                store.Save(currentFolder);
            }
            // Sinon, comportement classique (fichier pkx)
            else if (fileTreeView.SelectedNode.Tag is string filePath)
            {
                var fileName = Path.GetFileName(filePath);
                var meta = store.GetOrCreate(fileName);
                meta.Tags = tagBox.Text.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                meta.Comment = commentBox.Text;
                store.Save(currentFolder);
            }
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
