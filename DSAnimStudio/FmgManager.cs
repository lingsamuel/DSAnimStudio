﻿using Microsoft.Xna.Framework.Input;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsAssetPipeline;

namespace DSAnimStudio
{
    public class FmgManager
    {

        public static string[] EquipmentNamesHD;
        public static string[] EquipmentNamesBD;
        public static string[] EquipmentNamesAM;
        public static string[] EquipmentNamesLG;
        public static string[] EquipmentNamesWP;

        public static int[] EquipmentIDsHD;
        public static int[] EquipmentIDsBD;
        public static int[] EquipmentIDsAM;
        public static int[] EquipmentIDsLG;
        public static int[] EquipmentIDsWP;

        public static Dictionary<int, string> WeaponNames = new Dictionary<int, string>();
        public static Dictionary<int, string> ProtectorNames_HD = new Dictionary<int, string>();
        public static Dictionary<int, string> ProtectorNames_BD = new Dictionary<int, string>();
        public static Dictionary<int, string> ProtectorNames_AM = new Dictionary<int, string>();
        public static Dictionary<int, string> ProtectorNames_LG = new Dictionary<int, string>();

        private static SoulsAssetPipeline.SoulsGames GameTypeCurrentFmgsAreLoadedFrom = SoulsAssetPipeline.SoulsGames.None;

        public static void LoadAllFMG(bool forceReload)
        {
            if (!forceReload && GameTypeCurrentFmgsAreLoadedFrom == GameDataManager.GameType)
                return;

            List<FMG> weaponNameFMGs = new List<FMG>();
            List<FMG> armorNameFMGs = new List<FMG>();

            /*
                [ptde]
                weapon 1
                armor 2

                [ds1r]
                weapon 1, 30
                armor 2, 32

                [ds3]
                weapon 1
                armor 2
                weapon_dlc1 18
                armor_dlc1 19
                weapon_dlc2 33
                armor_dlc2 34

                [bb]
                weapon 1
                armor 2

                [sdt]
                weapon 1
                armor 2
            */

            void TryToLoadFromMSGBND(string language, string msgbndName, int weaponNamesIdx, int armorNamesIdx)
            {
                var msgbndRelativePath = $@"msg\{language}\{msgbndName}";
                var fullMsgbndPath = GameDataManager.GetInterrootPath(msgbndRelativePath);
                IBinder msgbnd = null;
                if (File.Exists(fullMsgbndPath))
                {
                    if (BND3.Is(fullMsgbndPath))
                        msgbnd = BND3.Read(fullMsgbndPath);
                    else if (BND4.Is(fullMsgbndPath))
                        msgbnd = BND4.Read(fullMsgbndPath);

                    weaponNameFMGs.Add(FMG.Read(msgbnd.Files.First(x => x.ID == weaponNamesIdx).Bytes));
                    armorNameFMGs.Add(FMG.Read(msgbnd.Files.First(x => x.ID == armorNamesIdx).Bytes));
                }

                if (msgbnd == null)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Unable to find text file '{msgbndRelativePath}'. Some player equipment may not show names.", 
                        "Unable to find asset", System.Windows.Forms.MessageBoxButtons.OK, 
                        System.Windows.Forms.MessageBoxIcon.Warning);

                    return;
                }
            }

            if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.DS1)
            {
                TryToLoadFromMSGBND("ENGLISH", "item.msgbnd", 11, 12);
                TryToLoadFromMSGBND("ENGLISH", "menu.msgbnd", 115, 117); //Patch
            }
            else if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.DES)
            {
                TryToLoadFromMSGBND("na_english", "item.msgbnd", 11, 12);
            }
            else if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.DS1R)
            {
                TryToLoadFromMSGBND("ENGLISH", "item.msgbnd.dcx", 11, 12);
                TryToLoadFromMSGBND("ENGLISH", "item.msgbnd.dcx", 115, 117); //Patch
            }
            else if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.DS3)
            {
                TryToLoadFromMSGBND("engus", "item_dlc1.msgbnd.dcx", 11, 12);
                TryToLoadFromMSGBND("engus", "item_dlc1.msgbnd.dcx", 211, 212); //DLC1

                TryToLoadFromMSGBND("engus", "item_dlc2.msgbnd.dcx", 11, 12);
                TryToLoadFromMSGBND("engus", "item_dlc2.msgbnd.dcx", 211, 212); //DLC1
                TryToLoadFromMSGBND("engus", "item_dlc2.msgbnd.dcx", 251, 252); //DLC2
            }
            else if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.SDT || GameDataManager.GameType == SoulsGames.ER)
            {
                TryToLoadFromMSGBND("engus", "item.msgbnd.dcx", 11, 12);
            }
            else if (GameDataManager.GameType == SoulsAssetPipeline.SoulsGames.BB)
            {
                TryToLoadFromMSGBND("engus", "item.msgbnd.dcx", 11, 12);
            }

            WeaponNames.Clear();

            void DoWeaponEntry(FMG.Entry entry)
            {
                if (string.IsNullOrWhiteSpace(entry.Text) ||
                    !ParamManager.EquipParamWeapon.ContainsKey(entry.ID))
                    return;

                //if (GameDataManager.GameType == GameDataManager.GameTypes.DS3 && (entry.ID % 10000) != 0)
                //    return;
                //else if ((entry.ID % 1000) != 0)
                //    return;
                string val = entry.Text + $" <{entry.ID}>";
                if (WeaponNames.ContainsKey(entry.ID))
                    WeaponNames[entry.ID] = val;
                else
                    WeaponNames.Add(entry.ID, val);
            }

            foreach (var wpnNameFmg in weaponNameFMGs)
                foreach (var entry in wpnNameFmg.Entries)
                    DoWeaponEntry(entry);

            ProtectorNames_HD.Clear();
            ProtectorNames_BD.Clear();
            ProtectorNames_AM.Clear();
            ProtectorNames_LG.Clear();

            void DoProtectorParamEntry(FMG.Entry entry)
            {
                if (string.IsNullOrWhiteSpace(entry.Text))
                    return;
                //if (entry.ID < 1000000 && GameDataManager.GameType == GameDataManager.GameTypes.DS3)
                //    return;
                if (ParamManager.EquipParamProtector.ContainsKey(entry.ID))
                {
                    string val = entry.Text + $" <{entry.ID}>";
                    var protectorParam = ParamManager.EquipParamProtector[entry.ID];
                    if (protectorParam.HeadEquip)
                    {
                        if (ProtectorNames_HD.ContainsKey(entry.ID))
                            ProtectorNames_HD[entry.ID] = val;
                        else
                            ProtectorNames_HD.Add(entry.ID, val);
                    }
                    else if (protectorParam.BodyEquip)
                    {
                        if (ProtectorNames_BD.ContainsKey(entry.ID))
                            ProtectorNames_BD[entry.ID] = val;
                        else
                            ProtectorNames_BD.Add(entry.ID, entry.Text + $" <{entry.ID}>");
                    }
                    else if (protectorParam.ArmEquip)
                    {
                        if (ProtectorNames_AM.ContainsKey(entry.ID))
                            ProtectorNames_AM[entry.ID] = val;
                        else
                            ProtectorNames_AM.Add(entry.ID, entry.Text + $" <{entry.ID}>");
                    }
                    else if (protectorParam.LegEquip)
                    {
                        if (ProtectorNames_LG.ContainsKey(entry.ID))
                            ProtectorNames_LG[entry.ID] = val;
                        else
                            ProtectorNames_LG.Add(entry.ID, entry.Text + $" <{entry.ID}>");
                    }
                }
            }

            foreach (var armorNameFmg in armorNameFMGs)
                foreach (var entry in armorNameFmg.Entries)
                    DoProtectorParamEntry(entry);

            WeaponNames = WeaponNames.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_HD = ProtectorNames_HD.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_BD = ProtectorNames_BD.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_AM = ProtectorNames_AM.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_LG = ProtectorNames_LG.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (var protector in ParamManager.EquipParamProtector)
            {
                if (protector.Value.HeadEquip && !ProtectorNames_HD.ContainsKey((int)protector.Key))
                {
                    ProtectorNames_HD.Add((int)protector.Key, $"<{protector.Key}> {protector.Value.Name ?? ""}");
                }
                else if (protector.Value.BodyEquip && !ProtectorNames_BD.ContainsKey((int)protector.Key))
                {
                    ProtectorNames_BD.Add((int)protector.Key, $"<{protector.Key}> {protector.Value.Name ?? ""}");
                }
                else if (protector.Value.ArmEquip && !ProtectorNames_AM.ContainsKey((int)protector.Key))
                {
                    ProtectorNames_AM.Add((int)protector.Key, $"<{protector.Key}> {protector.Value.Name ?? ""}");
                }
                else if (protector.Value.LegEquip && !ProtectorNames_LG.ContainsKey((int)protector.Key))
                {
                    ProtectorNames_LG.Add((int)protector.Key, $"<{protector.Key}> {protector.Value.Name ?? ""}");
                }
            }

            foreach (var weapon in ParamManager.EquipParamWeapon)
            {
                if (!WeaponNames.ContainsKey((int)weapon.Key))
                {
                    WeaponNames.Add((int)weapon.Key, $"<{weapon.Key}> {weapon.Value.Name ?? ""}");
                }
            }

            ProtectorNames_HD = ProtectorNames_HD.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_BD = ProtectorNames_BD.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_AM = ProtectorNames_AM.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ProtectorNames_LG = ProtectorNames_LG.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            WeaponNames = WeaponNames.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            EquipmentNamesHD = ProtectorNames_HD.Select(kvp => kvp.Value).ToArray();
            EquipmentIDsHD = ProtectorNames_HD.Select(kvp => kvp.Key).ToArray();

            EquipmentNamesBD = ProtectorNames_BD.Select(kvp => kvp.Value).ToArray();
            EquipmentIDsBD = ProtectorNames_BD.Select(kvp => kvp.Key).ToArray();

            EquipmentNamesAM = ProtectorNames_AM.Select(kvp => kvp.Value).ToArray();
            EquipmentIDsAM = ProtectorNames_AM.Select(kvp => kvp.Key).ToArray();

            EquipmentNamesLG = ProtectorNames_LG.Select(kvp => kvp.Value).ToArray();
            EquipmentIDsLG = ProtectorNames_LG.Select(kvp => kvp.Key).ToArray();

            EquipmentNamesWP = WeaponNames.Select(kvp => kvp.Value).ToArray();
            EquipmentIDsWP = WeaponNames.Select(kvp => kvp.Key).ToArray();

            GameTypeCurrentFmgsAreLoadedFrom = GameDataManager.GameType;
        }
    }
}
