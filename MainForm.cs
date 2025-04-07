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
        private ListView fileListView = new();
        private TextBox tagBox = new();
        private TextBox commentBox = new();
        private Button saveButton = new();
        private TextBox searchBox = new();
        private FolderBrowserDialog folderBrowser = new();

        private MetadataStore store = new();
        private string currentFolder = "";

        public MainForm()
        {
            Text = "PokeViewer";
            Width = 1000;
            Height = 600;

            fileListView.View = View.List;
            fileListView.Dock = DockStyle.Left;
            fileListView.Width = 300;
            fileListView.SelectedIndexChanged += FileListView_SelectedIndexChanged;

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

            Controls.Add(fileListView);
            Controls.Add(tagBox);
            Controls.Add(commentBox);
            Controls.Add(saveButton);
            Controls.Add(searchBox);

            Load += (_, _) => LoadFolder();
        }

        private void LoadFolder()
        {
            folderBrowser.Description = "Choisir un dossier contenant des fichiers pkX";
            if (folderBrowser.ShowDialog() != DialogResult.OK)
                return;

            currentFolder = folderBrowser.SelectedPath;
            store = MetadataStore.Load(currentFolder);
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            fileListView.Items.Clear();
            var files = Directory.GetFiles(currentFolder, "*.pk*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                fileListView.Items.Add(new ListViewItem(fileName));
            }
        }

        private void FileListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (fileListView.SelectedItems.Count == 0)
                return;

            var selected = fileListView.SelectedItems[0].Text;
            var meta = store.GetOrCreate(selected);

            tagBox.Text = string.Join(", ", meta.Tags);
            commentBox.Text = meta.Comment;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (fileListView.SelectedItems.Count == 0)
                return;

            var selected = fileListView.SelectedItems[0].Text;
            var meta = store.GetOrCreate(selected);
            meta.Tags = tagBox.Text.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            meta.Comment = commentBox.Text;

            store.Save(currentFolder);
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            var query = searchBox.Text.Trim().ToLower();
            fileListView.Items.Clear();

            foreach (var kvp in store.Entries)
            {
                var tagsMatch = kvp.Value.Tags.Any(tag => tag.ToLower().Contains(query));
                var commentMatch = kvp.Value.Comment.ToLower().Contains(query);

                if (tagsMatch || commentMatch || query == "")
                    fileListView.Items.Add(new ListViewItem(kvp.Key));
            }
        }
    }
}
