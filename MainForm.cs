using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using PKHeX.Core;

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
        private string nameFilter = "";
        private string tagFilter = "";
        private string commentFilter = "";
        private TextBox tagFilterBox = new();
        private Button tagFilterButton = new();
        private TextBox commentFilterBox = new();
        private Button commentFilterButton = new();
        private Button saveCommentButton = new();
        private Button saveTagButton = new();
        private Button openFolderButton = new();

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

            // Recherche par nom de Pokémon (français)
            nameSearchBox.Top = 10;
            nameSearchBox.Left = 320;
            nameSearchBox.Width = 250;
            nameSearchBox.PlaceholderText = "Nom du Pokémon (français)";
            nameSearchButton.Text = "🔍";
            nameSearchButton.Top = 10;
            nameSearchButton.Left = 580;
            nameSearchButton.Width = 40;
            nameSearchButton.Height = nameSearchBox.Height;
            nameSearchButton.Click += NameSearchButton_Click;

            // Zone commentaire (doublée)
            commentBox.Top = 50;
            commentBox.Left = 320;
            commentBox.Width = 400;
            commentBox.Height = 80;
            commentBox.Multiline = true;
            commentBox.PlaceholderText = "Commentaire";
            saveCommentButton.Text = "💾";
            saveCommentButton.Top = 50;
            saveCommentButton.Left = 730;
            saveCommentButton.Width = 40;
            saveCommentButton.Height = commentBox.Height;
            saveCommentButton.Click += SaveCommentButton_Click;

            // Zone tags
            tagBox.Top = 140;
            tagBox.Left = 320;
            tagBox.Width = 400;
            tagBox.PlaceholderText = "Tags (séparés par virgule)";
            saveTagButton.Text = "💾";
            saveTagButton.Top = 140;
            saveTagButton.Left = 730;
            saveTagButton.Width = 40;
            saveTagButton.Height = tagBox.Height;
            saveTagButton.Click += SaveTagButton_Click;

            // Bouton ouvrir dossier Pokémon
            openFolderButton.Text = "📁";
            openFolderButton.Top = 140;
            openFolderButton.Left = 780;
            openFolderButton.Width = 40;
            openFolderButton.Height = tagBox.Height;
            openFolderButton.Click += OpenFolderButton_Click;
            Controls.Add(openFolderButton);

            // Filtres tag/commentaire côte à côte
            tagFilterBox.Top = 180;
            tagFilterBox.Left = 320;
            tagFilterBox.Width = 200;
            tagFilterBox.PlaceholderText = "Filtrer par tag";
            tagFilterButton.Text = "🔍";
            tagFilterButton.Top = 180;
            tagFilterButton.Left = 525;
            tagFilterButton.Width = 40;
            tagFilterButton.Height = tagFilterBox.Height;
            tagFilterButton.Click += TagFilterButton_Click;

            commentFilterBox.Top = 180;
            commentFilterBox.Left = 580;
            commentFilterBox.Width = 200;
            commentFilterBox.PlaceholderText = "Filtrer par commentaire";
            commentFilterButton.Text = "🔍";
            commentFilterButton.Top = 180;
            commentFilterButton.Left = 785;
            commentFilterButton.Width = 40;
            commentFilterButton.Height = commentFilterBox.Height;
            commentFilterButton.Click += CommentFilterButton_Click;

            Controls.Add(fileTreeView);
            Controls.Add(nameSearchBox);
            Controls.Add(nameSearchButton);
            Controls.Add(commentBox);
            Controls.Add(saveCommentButton);
            Controls.Add(tagBox);
            Controls.Add(saveTagButton);
            Controls.Add(tagFilterBox);
            Controls.Add(tagFilterButton);
            Controls.Add(commentFilterBox);
            Controls.Add(commentFilterButton);

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
            var foundIds = new HashSet<string>();
            var idToClones = new Dictionary<string, List<PkxFilesSaveUtil.BoxPokemonInfo>>();
            foreach (var savePath in Directory.GetFiles(currentFolder, "*", SearchOption.AllDirectories)
                .Where(f => SaveExtensions.Contains(Path.GetExtension(f).ToLower()) || Path.GetFileName(f).ToLower() == "main"))
            {
                try
                {
                    var mons = PkxFilesSaveUtil.LoadBoxPokemons(savePath);
                    foreach (var mon in mons)
                    {
                        if (!idToClones.ContainsKey(mon.UniqueID))
                            idToClones[mon.UniqueID] = new List<PkxFilesSaveUtil.BoxPokemonInfo>();
                        idToClones[mon.UniqueID].Add(mon);
                        if (!allBoxMons.ContainsKey(mon.UniqueID))
                            allBoxMons[mon.UniqueID] = mon;
                        foundIds.Add(mon.UniqueID);
                    }
                }
                catch { }
            }
            var knownIds = store.GetAllKeys();
            var newIds = foundIds.Except(knownIds).ToList();
            var removedIds = knownIds.Except(foundIds).ToList();
            // Détection d'évolution (inchangé)
            var baseIdToSpecies = new Dictionary<string, string>();
            foreach (var id in knownIds)
            {
                var baseId = GetBaseId(id);
                var species = GetSpeciesFromId(id);
                if (!string.IsNullOrEmpty(baseId) && !string.IsNullOrEmpty(species))
                    baseIdToSpecies[baseId] = species;
            }
            var evolutionIds = new List<string>();
            foreach (var id in newIds.ToList())
            {
                var baseId = GetBaseId(id);
                var species = GetSpeciesFromId(id);
                if (!string.IsNullOrEmpty(baseId) && !string.IsNullOrEmpty(species)
                    && baseIdToSpecies.TryGetValue(baseId, out var oldSpecies)
                    && oldSpecies != species)
                {
                    evolutionIds.Add(id);
                    newIds.Remove(id); // On ne le compte plus comme simple ajout
                }
            }
            // Préparer la liste des clones (groupes d'ID avec plusieurs entrées)
            var cloneGroups = idToClones.Where(kv => kv.Value.Count > 1).ToDictionary(kv => kv.Key, kv => kv.Value);
            // Pour chaque suppression, ajouter le tag 'disparu' automatiquement
            foreach (var id in removedIds)
            {
                var meta = store.GetOrCreate(id);
                if (!meta.Tags.Contains("disparu"))
                {
                    meta.Tags.Add("disparu");
                }
            }
            if (removedIds.Count > 0)
                store.Save(currentFolder);
            // Si au moins un ajout, évolution ou clone, afficher le pop-up
            if (newIds.Count > 0 || evolutionIds.Count > 0 || cloneGroups.Count > 0)
            {
                using (var popup = new StartupPopup(newIds, evolutionIds, removedIds, cloneGroups, allBoxMons, store))
                {
                    if (popup.ShowDialog() == DialogResult.OK)
                    {
                        filteredIds = popup.GetFilteredIds();
                        foreach (var id in newIds.Concat(evolutionIds))
                        {
                            if (!store.GetAllKeys().Contains(id))
                                store.GetOrCreate(id);
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

            // Création de l'arborescence "Pokemon Data"
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string dataRoot = Path.Combine(exeDir, "Pokemon Data");
                if (!Directory.Exists(dataRoot))
                    Directory.CreateDirectory(dataRoot);
                foreach (var savePath in Directory.GetFiles(currentFolder, "*", SearchOption.AllDirectories)
                    .Where(f => SaveExtensions.Contains(Path.GetExtension(f).ToLower()) || Path.GetFileName(f).ToLower() == "main"))
                {
                    var mons = PkxFilesSaveUtil.LoadBoxPokemons(savePath);
                    foreach (var mon in mons)
                    {
                        var monDir = Path.Combine(dataRoot, mon.UniqueID);
                        if (!Directory.Exists(monDir))
                            Directory.CreateDirectory(monDir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la création de l'arborescence Pokemon Data : {ex.Message}");
            }
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
                {
                    node.Nodes.Add(new TreeNode(file.Name) { Tag = file.FullName });
                }
                else if (SaveExtensions.Contains(file.Extension.ToLower()) || file.Name.ToLower() == "main")
                {
                    var saveNode = new TreeNode(Path.GetFileNameWithoutExtension(file.Name)) { Tag = file.FullName };
                    try
                    {
                        var boxMons = PkxFilesSaveUtil.LoadBoxPokemons(file.FullName);
                        var boxGroups = boxMons.GroupBy(x => x.Box).ToList();
                        // Afficher l'équipe en premier
                        var partyGroup = boxGroups.FirstOrDefault(g => g.Key == -1);
                        if (partyGroup != null)
                        {
                            var partyNode = new TreeNode("Équipe");
                            foreach (var mon in partyGroup)
                            {
                                if (filteredIds.Count > 0 && !filteredIds.Contains(mon.UniqueID))
                                    continue;
                                // Filtrage par nom (français)
                                if (!string.IsNullOrEmpty(nameFilter))
                                {
                                    var frName = GetFrenchName(mon.Pkm?.Species ?? 0);
                                    if (!frName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                                        continue;
                                }
                                // Filtrage par tag
                                if (!string.IsNullOrEmpty(tagFilter))
                                {
                                    var meta = store.GetOrCreate(mon.UniqueID);
                                    if (!meta.Tags.Any(t => t.ToLower().Contains(tagFilter)))
                                        continue;
                                }
                                // Filtrage par commentaire
                                if (!string.IsNullOrEmpty(commentFilter))
                                {
                                    var meta = store.GetOrCreate(mon.UniqueID);
                                    if (!meta.Comment.ToLower().Contains(commentFilter))
                                        continue;
                                }
                                var monName = mon.Pkm?.Nickname ?? "?";
                                partyNode.Nodes.Add(new TreeNode($"{monName} (Slot {mon.Slot + 1})") { Tag = mon });
                            }
                            if (partyNode.Nodes.Count > 0)
                                saveNode.Nodes.Add(partyNode);
                        }
                        // Puis les boîtes numérotées
                        foreach (var box in boxGroups.Where(g => g.Key != -1).OrderBy(g => g.Key))
                        {
                            var boxNode = new TreeNode($"Boîte {box.Key + 1}");
                            foreach (var mon in box)
                            {
                                if (filteredIds.Count > 0 && !filteredIds.Contains(mon.UniqueID))
                                    continue;
                                if (!string.IsNullOrEmpty(nameFilter))
                                {
                                    var frName = GetFrenchName(mon.Pkm?.Species ?? 0);
                                    if (!frName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                                        continue;
                                }
                                if (!string.IsNullOrEmpty(tagFilter))
                                {
                                    var meta = store.GetOrCreate(mon.UniqueID);
                                    if (!meta.Tags.Any(t => t.ToLower().Contains(tagFilter)))
                                        continue;
                                }
                                if (!string.IsNullOrEmpty(commentFilter))
                                {
                                    var meta = store.GetOrCreate(mon.UniqueID);
                                    if (!meta.Comment.ToLower().Contains(commentFilter))
                                        continue;
                                }
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

        private void NameSearchButton_Click(object? sender, EventArgs e)
        {
            nameFilter = nameSearchBox.Text.Trim();
            RefreshFileTree();
        }
        private void TagFilterButton_Click(object? sender, EventArgs e)
        {
            tagFilter = tagFilterBox.Text.Trim().ToLower();
            RefreshFileTree();
        }
        private void CommentFilterButton_Click(object? sender, EventArgs e)
        {
            commentFilter = commentFilterBox.Text.Trim().ToLower();
            RefreshFileTree();
        }
        private void SaveCommentButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            if (fileTreeView.SelectedNode.Tag is PkxFilesSaveUtil.BoxPokemonInfo boxMon)
            {
                var meta = store.GetOrCreate(boxMon.UniqueID);
                meta.Comment = commentBox.Text;
                store.Save(currentFolder);
            }
            else if (fileTreeView.SelectedNode.Tag is string filePath)
            {
                var fileName = Path.GetFileName(filePath);
                var meta = store.GetOrCreate(fileName);
                meta.Comment = commentBox.Text;
                store.Save(currentFolder);
            }
        }
        private void SaveTagButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            if (fileTreeView.SelectedNode.Tag is PkxFilesSaveUtil.BoxPokemonInfo boxMon)
            {
                var meta = store.GetOrCreate(boxMon.UniqueID);
                meta.Tags = tagBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                store.Save(currentFolder);
            }
            else if (fileTreeView.SelectedNode.Tag is string filePath)
            {
                var fileName = Path.GetFileName(filePath);
                var meta = store.GetOrCreate(fileName);
                meta.Tags = tagBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                store.Save(currentFolder);
            }
        }

        private void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode == null || fileTreeView.SelectedNode.Tag == null)
                return;
            string? id = null;
            if (fileTreeView.SelectedNode.Tag is PkxFilesSaveUtil.BoxPokemonInfo boxMon)
                id = boxMon.UniqueID;
            else if (fileTreeView.SelectedNode.Tag is string filePath)
                id = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(id)) return;
            // Ouvrir directement le dossier Pokemon Data\ID
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataRoot = Path.Combine(exeDir, "Pokemon Data");
            string monDir = Path.Combine(dataRoot, id);
            if (Directory.Exists(monDir))
                System.Diagnostics.Process.Start("explorer.exe", monDir);
            else
                MessageBox.Show($"Dossier non trouvé : {monDir}");
        }

        // Utilitaire pour obtenir le nom français d'une espèce
        private string GetFrenchName(int species)
        {
            try
            {
                var speciesList = GameInfo.Strings.Species;
                if (species >= 0 && species < speciesList.Count)
                    return speciesList[species] ?? "";
                return "";
            }
            catch { return ""; }
        }

        // Utilitaires pour extraire la base d'ID et l'espèce
        private string GetBaseId(string id)
        {
            var dash = id.IndexOf('-');
            if (dash < 0 || dash + 1 >= id.Length) return "";
            var basePart = id.Substring(dash + 1); // IV32_TID16_SID16_OT
            return basePart;
        }
        private string GetSpeciesFromId(string id)
        {
            var dash = id.IndexOf('-');
            if (dash < 0) return "";
            return id.Substring(0, dash); // 4 chiffres
        }
    }
}
