using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO; // Added for Path and Directory

namespace PokeViewer
{
    public class StartupPopup : Form
    {
        private ListBox listBox;
        private Button okButton;
        private List<string> filteredIds = new();
        private Dictionary<string, CheckBox> evolutionCheckboxes = new();
        private MetadataStore store; // Added for MetadataStore

        public StartupPopup(
            List<string> newIds,
            List<string> evolutionIds,
            List<string> conflictIds,
            List<string> removedIds,
            Dictionary<string, PkxFilesSaveUtil.BoxPokemonInfo> allBoxMons,
            MetadataStore store)
        {
            Text = "Synchronisation des Pokémon";
            Width = 700;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            this.store = store; // Initialize store

            var panel = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 380,
                AutoScroll = true
            };
            listBox = new ListBox()
            {
                Visible = false // On n'utilise plus le ListBox pour l'affichage
            };
            okButton = new Button()
            {
                Text = "Valider et filtrer",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            okButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            Controls.Add(panel);
            Controls.Add(okButton);
            Controls.Add(listBox);

            int y = 0;
            void AddLabel(string text)
            {
                var label = new Label { Text = text, Left = 5, Top = y, Width = 650, Font = new Font(FontFamily.GenericMonospace, 10, FontStyle.Bold) };
                panel.Controls.Add(label);
                y += 22;
            }
            void AddText(string text)
            {
                var label = new Label { Text = text, Left = 20, Top = y, Width = 650, Font = new Font(FontFamily.GenericMonospace, 10) };
                panel.Controls.Add(label);
                y += 20;
            }

            if (newIds.Count > 0)
            {
                AddLabel("Ajouts détectés :");
                foreach (var id in newIds)
                {
                    string desc = allBoxMons.TryGetValue(id, out var mon) ?
                        $"[Ajout] {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"}) (ID: {id})" : $"[Ajout] ID: {id}";
                    AddText(desc);
                    filteredIds.Add(id);
                }
                y += 8;
            }
            if (evolutionIds.Count > 0)
            {
                AddLabel("Évolutions détectées :");
                foreach (var id in evolutionIds)
                {
                    string desc = allBoxMons.TryGetValue(id, out var mon) ?
                        $"[Évolution] {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"}) (ID: {id})" : $"[Évolution] ID: {id}";
                    var cb = new CheckBox { Text = desc + "  → Confirmer évolution", Left = 20, Top = y, Width = 650 };
                    panel.Controls.Add(cb);
                    evolutionCheckboxes[id] = cb;
                    y += 24;
                }
                y += 8;
            }
            if (conflictIds.Count > 0)
            {
                AddLabel("Conflits détectés :");
                foreach (var id in conflictIds)
                {
                    string desc = allBoxMons.TryGetValue(id, out var mon) ?
                        $"[Conflit] {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"}) (ID: {id})" : $"[Conflit] ID: {id}";
                    AddText(desc);
                    filteredIds.Add(id);
                }
                y += 8;
            }
            if (removedIds.Count > 0)
            {
                AddLabel("Suppressions détectées :");
                foreach (var id in removedIds)
                {
                    var meta = store.GetOrCreate(id);
                    string desc = $"[Suppression] {meta.Comment} (ID: {id})";
                    AddText(desc);
                    filteredIds.Add(id);
                }
                y += 8;
            }
            if (panel.Controls.Count == 0)
            {
                AddText("Aucun changement détecté.");
            }
        }

        private string GetMonDesc(PkxFilesSaveUtil.BoxPokemonInfo mon)
        {
            if (mon.Pkm == null) return "?";
            return $"Espèce {mon.Pkm.Species} - {mon.Pkm.Nickname} (Box {mon.Box + 1}, Slot {mon.Slot + 1})";
        }

        public List<string> GetFilteredIds()
        {
            // On ajoute seulement les évolutions confirmées
            var confirmedEvos = evolutionCheckboxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
            // Pour chaque évolution confirmée, mettre à jour l'ID dans le store et renommer le dossier
            foreach (var evoId in confirmedEvos)
            {
                // On cherche l'ancien ID (base d'ID identique mais espèce différente)
                var baseId = GetBaseId(evoId);
                var oldId = store.Entries.Keys.FirstOrDefault(k => GetBaseId(k) == baseId && GetSpeciesFromId(k) != GetSpeciesFromId(evoId));
                if (!string.IsNullOrEmpty(oldId))
                {
                    // Copier les métadonnées
                    store.Entries[evoId] = store.Entries[oldId];
                    store.Entries.Remove(oldId);
                    // Renommer le dossier dans Pokemon Data
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string dataRoot = Path.Combine(exeDir, "Pokemon Data");
                    var monDirOld = FindPokemonDir(dataRoot, oldId);
                    var monDirNew = FindPokemonDir(dataRoot, evoId);
                    if (Directory.Exists(monDirOld))
                    {
                        try
                        {
                            Directory.Move(monDirOld, monDirNew);
                        }
                        catch { }
                    }
                }
            }
            return filteredIds.Concat(confirmedEvos).Distinct().ToList();
        }

        // Utilitaires pour retrouver la base d'ID et l'espèce
        private string GetBaseId(string id)
        {
            var dash = id.IndexOf('-');
            if (dash < 0 || dash + 1 >= id.Length) return "";
            var basePart = id.Substring(dash + 1); // PID_IV32_TID16_SID16_OT
            return basePart;
        }
        private string GetSpeciesFromId(string id)
        {
            var dash = id.IndexOf('-');
            if (dash < 0) return "";
            return id.Substring(0, dash); // 4 chiffres
        }
        // Trouver le dossier du Pokémon dans Pokemon Data
        private string FindPokemonDir(string dataRoot, string id)
        {
            foreach (var dir in Directory.GetDirectories(dataRoot, id, SearchOption.AllDirectories))
                return dir;
            return Path.Combine(dataRoot, id); // fallback
        }
    }
} 