using System;
using System.Collections.Generic;
using System.IO;
using PKHeX.Core;

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
            return result;
        }

        public static string GenerateUniqueId(PKM pkm, int box, int slot)
        {
            // PID
            string pid = pkm.PID.ToString();
            // TID
            string tid = pkm.TID16.ToString();
            // SID
            string sid = pkm.SID16.ToString();
            // OT (converti en décimal)
            string ot = "";
            foreach (char c in pkm.OriginalTrainerName)
                ot += ((int)c).ToString();
            // IVs (IV32)
            string ivs = string.Join("", pkm.IVs);
            // Box et Slot
            string boxStr = box.ToString();
            string slotStr = slot.ToString();
            return $"{pid}{tid}{sid}{ot}{boxStr}{slotStr}{ivs}";
        }
    }
} 