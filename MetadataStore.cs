using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PokeViewer
{
    public class FileMetadata
    {
        public string FileName { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string Comment { get; set; } = "";
    }

    public class MetadataStore
    {
        public Dictionary<string, FileMetadata> Entries { get; set; } = new();

        public static string MetadataFileName => ".pokeviewer.meta.json";

        public static MetadataStore Load(string folderPath)
        {
            string metaPath = Path.Combine(folderPath, MetadataFileName);
            if (!File.Exists(metaPath))
                return new MetadataStore();

            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<MetadataStore>(json) ?? new MetadataStore();
        }

        public void Save(string folderPath)
        {
            string metaPath = Path.Combine(folderPath, MetadataFileName);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);
        }

        public FileMetadata GetOrCreate(string fileName)
        {
            if (!Entries.TryGetValue(fileName, out var meta))
            {
                meta = new FileMetadata { FileName = fileName };
                Entries[fileName] = meta;
            }
            return meta;
        }

        public void Delete(string fileName)
        {
            if (Entries.ContainsKey(fileName))
                Entries.Remove(fileName);
        }
    }
}
