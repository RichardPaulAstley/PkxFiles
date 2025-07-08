using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PokeViewer
{
    public class StartupPopup : Form
    {
        private ListBox listBox;
        private Button okButton;
        private List<string> filteredIds = new();

        public StartupPopup(
            List<string> newIds,
            List<string> removedIds,
            List<string> conflictIds,
            Dictionary<string, PkxFilesSaveUtil.BoxPokemonInfo> allBoxMons,
            MetadataStore store)
        {
            Text = "Synchronisation des Pokémon";
            Width = 700;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            listBox = new ListBox()
            {
                Dock = DockStyle.Top,
                Height = 380,
                Font = new Font(FontFamily.GenericMonospace, 10)
            };
            okButton = new Button()
            {
                Text = "Valider et filtrer",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            okButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            Controls.Add(listBox);
            Controls.Add(okButton);

            // Affichage des cas
            if (newIds.Count > 0)
            {
                listBox.Items.Add($"Ajouts détectés :");
                foreach (var id in newIds)
                {
                    string desc = allBoxMons.TryGetValue(id, out var mon) ?
                        $"[Ajout] {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"}) (ID: {id})" : $"[Ajout] ID: {id}";
                    listBox.Items.Add(desc);
                    filteredIds.Add(id);
                }
                listBox.Items.Add("");
            }
            if (conflictIds.Count > 0)
            {
                listBox.Items.Add($"Conflits détectés :");
                foreach (var id in conflictIds)
                {
                    string desc = allBoxMons.TryGetValue(id, out var mon) ?
                        $"[Conflit] {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"}) (ID: {id})" : $"[Conflit] ID: {id}";
                    listBox.Items.Add(desc);
                    filteredIds.Add(id);
                }
                listBox.Items.Add("");
            }
            if (removedIds.Count > 0)
            {
                listBox.Items.Add($"Suppressions détectées :");
                foreach (var id in removedIds)
                {
                    var meta = store.GetOrCreate(id);
                    string desc = $"[Suppression] {meta.Comment} (ID: {id})";
                    listBox.Items.Add(desc);
                    filteredIds.Add(id);
                }
                listBox.Items.Add("");
            }
            if (listBox.Items.Count == 0)
            {
                listBox.Items.Add("Aucun changement détecté.");
            }
        }

        private string GetMonDesc(PkxFilesSaveUtil.BoxPokemonInfo mon)
        {
            if (mon.Pkm == null) return "?";
            return $"Espèce {mon.Pkm.Species} - {mon.Pkm.Nickname} (Box {mon.Box + 1}, Slot {mon.Slot + 1})";
        }

        public List<string> GetFilteredIds()
        {
            return filteredIds.Distinct().ToList();
        }
    }
} 