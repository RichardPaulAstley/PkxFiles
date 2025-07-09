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
        // private List<string> conflictIds; // SUPPRIMÉ
        private Dictionary<string, List<PkxFilesSaveUtil.BoxPokemonInfo>> cloneGroups;

        public StartupPopup(
            List<string> newIds,
            List<string> evolutionIds,
            List<string> removedIds,
            Dictionary<string, List<PkxFilesSaveUtil.BoxPokemonInfo>> cloneGroups,
            Dictionary<string, PkxFilesSaveUtil.BoxPokemonInfo> allBoxMons,
            MetadataStore store)
        {
            // this.conflictIds = conflictIds; // SUPPRIMÉ
            this.cloneGroups = cloneGroups;
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
            // Affichage des clones détectés
            if (cloneGroups.Count > 0)
            {
                foreach (var kv in cloneGroups)
                {
                    var id = kv.Key;
                    var clones = kv.Value;
                    AddLabel($"Clones détectés pour ID {id} :");
                    int cloneIdx = 1;
                    foreach (var mon in clones)
                    {
                        string desc = $"Clone {cloneIdx} : {GetMonDesc(mon)} (Save: {mon?.SaveFileName ?? "?"})";
                        var cb = new CheckBox { Text = desc + "  → Conserver ce clone", Left = 20, Top = y, Width = 650 };
                        panel.Controls.Add(cb);
                        evolutionCheckboxes[$"{id}__clone{cloneIdx}"] = cb;
                        y += 24;
                        cloneIdx++;
                    }
                    y += 8;
                }
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
            // Gestion des clones : pour chaque groupe, si plusieurs sont cochés, créer les dossiers avec suffixe
            foreach (var kv in cloneGroups)
            {
                var id = kv.Key;
                var clones = kv.Value;
                int idx = 1;
                bool auMoinsUnCoche = false;
                foreach (var mon in clones)
                {
                    var key = $"{id}__clone{idx}";
                    if (evolutionCheckboxes.TryGetValue(key, out var cb) && cb.Checked)
                    {
                        auMoinsUnCoche = true;
                        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                        string dataRoot = Path.Combine(exeDir, "Pokemon Data");
                        string baseDir = Path.Combine(dataRoot, id);
                        string finalDir = baseDir;
                        string idIncremente = id;
                        if (idx > 1)
                        {
                            int n = idx;
                            finalDir = baseDir + $"_({n})";
                            idIncremente = id + $"_({n})";
                        }
                        if (!Directory.Exists(finalDir))
                            Directory.CreateDirectory(finalDir);
                        // Ajout dans le store pour l'ID incrémenté
                        store.GetOrCreate(idIncremente);
                    }
                    idx++;
                }
                // Si aucun clone n'est coché, on ne crée qu'une seule entrée ID_(cloned) (pour le premier du groupe)
                if (!auMoinsUnCoche && clones.Count > 0)
                {
                    string idCloned = id + "_(cloned)";
                    store.GetOrCreate(idCloned);
                }
            }
            // On ajoute seulement les évolutions confirmées
            var confirmedEvos = evolutionCheckboxes.Where(kv => kv.Value.Checked && !kv.Key.Contains("__clone")).Select(kv => kv.Key).ToList();
            // Gestion des conflits (copies parfaites) SUPPRIMÉ
            // On suppose que les conflits sont dans conflictIds et affichés avec des CheckBox (à adapter si besoin)
            // Pour chaque conflit, si plusieurs sont cochés, on incrémente le nom du dossier
            // (Ici, on suppose que chaque conflit correspond à un ID unique, mais il peut y avoir plusieurs copies)
            // On va créer les dossiers avec suffixe si besoin
            // foreach (var id in conflictIds) // SUPPRIMÉ
            // {
            //     // Si l'utilisateur a coché ce conflit (à adapter si CheckBox) // SUPPRIMÉ
            //     if (evolutionCheckboxes.TryGetValue(id, out var cb) && cb.Checked) // SUPPRIMÉ
            //     {
            //         // Chercher un nom de dossier libre // SUPPRIMÉ
            //         string exeDir = AppDomain.CurrentDomain.BaseDirectory; // SUPPRIMÉ
            //         string dataRoot = Path.Combine(exeDir, "Pokemon Data"); // SUPPRIMÉ
            //         string baseDir = Path.Combine(dataRoot, id); // SUPPRIMÉ
            //         string finalDir = baseDir; // SUPPRIMÉ
            //         int n = 2; // SUPPRIMÉ
            //         while (Directory.Exists(finalDir)) // SUPPRIMÉ
            //         { // SUPPRIMÉ
            //             finalDir = baseDir + $"_({n})"; // SUPPRIMÉ
            //             n++; // SUPPRIMÉ
            //         } // SUPPRIMÉ
            //         Directory.CreateDirectory(finalDir); // SUPPRIMÉ
            //         selectedConflicts.Add(finalDir); // SUPPRIMÉ
            //     } // SUPPRIMÉ
            // } // SUPPRIMÉ
            // Pour les évolutions, on garde la logique précédente
            foreach (var evoId in confirmedEvos)
            {
                var baseId = GetBaseId(evoId);
                var oldId = store.Entries.Keys.FirstOrDefault(k => GetBaseId(k) == baseId && GetSpeciesFromId(k) != GetSpeciesFromId(evoId));
                if (!string.IsNullOrEmpty(oldId))
                {
                    store.Entries[evoId] = store.Entries[oldId];
                    store.Entries.Remove(oldId);
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string dataRoot = Path.Combine(exeDir, "Pokemon Data");
                    var monDirOld = FindPokemonDir(dataRoot, oldId);
                    var monDirNew = FindPokemonDir(dataRoot, evoId);
                    if (Directory.Exists(monDirOld))
                    {
                        try { Directory.Move(monDirOld, monDirNew); } catch { }
                    }
                }
            }
            // Sauvegarde du store après ajout des clones et évolutions
            // La sauvegarde doit être faite dans le dossier ouvert par l'utilisateur (currentFolder), donc ici on ne fait rien :
            // La sauvegarde sera faite par MainForm après GetFilteredIds()
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