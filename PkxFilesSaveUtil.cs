using System;
using System.Collections.Generic;
using System.IO;
using PKHeX.Core;
using System.Linq; // Added for .Where() and .FirstOrDefault()

namespace PokeViewer
{
    public static class PkxFilesSaveUtil
    {
        public class BoxPokemonInfo
        {
            public int Box { get; set; }
            public int Slot { get; set; }
            public string UniqueID { get; set; } = string.Empty;
            public PKM? Pkm { get; set; }
            public string SaveFileName { get; set; } = string.Empty;
        }

        public static List<BoxPokemonInfo> LoadBoxPokemons(string savePath)
        {
            var result = new List<BoxPokemonInfo>();
            var data = File.ReadAllBytes(savePath);
            var sav = SaveUtil.GetVariantSAV(data);
            if (sav == null)
                throw new Exception("Sauvegarde non reconnue ou non supportée par PKHeX.Core");

            // Lecture des boîtes
            for (int box = 0; box < sav.BoxCount; box++)
            {
                var boxData = sav.GetBoxData(box);
                for (int slot = 0; slot < boxData.Length; slot++)
                {
                    var pkm = boxData[slot];
                    if (pkm == null || pkm.Species == 0)
                        continue;
                    string uniqueId = GenerateUniqueId(pkm, box, slot);
                    result.Add(new BoxPokemonInfo
                    {
                        Box = box,
                        Slot = slot,
                        UniqueID = uniqueId,
                        Pkm = pkm,
                        SaveFileName = Path.GetFileName(savePath)
                    });
                }
            }
            // Lecture de l'équipe (party) : cibler explicitement les propriétés les plus courantes
            PKHeX.Core.PKM[]? partyData = null;
            var partyPropNames = new[] { "Party", "CurrentParty", "PartyData" };
            foreach (var propName in partyPropNames)
            {
                var prop = sav.GetType().GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(sav) as PKHeX.Core.PKM[];
                    if (val != null && val.Length > 0 && val.Any(pkm => pkm != null && pkm.Species > 0))
                    {
                        partyData = val;
                        break;
                    }
                }
            }
            if (partyData != null)
            {
                for (int slot = 0; slot < partyData.Length; slot++)
                {
                    var pkm = partyData[slot];
                    if (pkm == null || pkm.Species == 0)
                        continue;
                    string uniqueId = GenerateUniqueId(pkm, -1, slot);
                    result.Add(new BoxPokemonInfo
                    {
                        Box = -1,
                        Slot = slot,
                        UniqueID = uniqueId,
                        Pkm = pkm,
                        SaveFileName = Path.GetFileName(savePath)
                    });
                }
            }
            return result;
        }

        public static string GenerateUniqueId(PKM pkm, int box, int slot)
        {
            // Espèce sur 4 chiffres
            string species = pkm.Species.ToString("D4");
            // PID
            string pid = pkm.PID.ToString();
            // IVs (IV32)
            string ivs = string.Join("", pkm.IVs);
            // TID
            string tid = pkm.TID16.ToString();
            // SID
            string sid = pkm.SID16.ToString();
            // OT (converti en décimal)
            string ot = "";
            foreach (char c in pkm.OriginalTrainerName)
                ot += ((int)c).ToString();
            // Format final
            return $"{species}-{pid}_{ivs}_{tid}_{sid}_{ot}";
        }
    }
} 