using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PokeViewer
{
    public class FileMetadata
    {
        public List<string> Tags { get; set; } = new();
        public string Comment { get; set; } = "";
    }

    public class MetadataStore
    {
        public Dictionary<string, FileMetadata> Entries { get; set; } = new();

        public static string MetadataFileName => ".pokeviewer.meta.json";

        public static MetadataStore Load(string rootFolderPath)
        {
            string metaPath = Path.Combine(rootFolderPath, MetadataFileName);
            if (!File.Exists(metaPath))
                return new MetadataStore();

            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<MetadataStore>(json) ?? new MetadataStore();
        }

        public void Save(string rootFolderPath)
        {
            string metaPath = Path.Combine(rootFolderPath, MetadataFileName);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        public FileMetadata GetOrCreate(string relativePath)
        {
            if (!Entries.TryGetValue(relativePath, out var meta))
            {
                meta = new FileMetadata();
                Entries[relativePath] = meta;
            }
            return meta;
        }

        public void Delete(string relativePath)
        {
            if (Entries.ContainsKey(relativePath))
                Entries.Remove(relativePath);
        }
    }
}
