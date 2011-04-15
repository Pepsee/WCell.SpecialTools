/*************************************************************************
 *
 *   file		: Program.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2009-04-30 01:12:32 +0800 (Thu, 30 Apr 2009) $
 *   last author	: $LastChangedBy: ralekdev $
 *   revision		: $Rev: 881 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Collections.Generic;
using ClientTools.Extractors;
using ClientTools.UpdateFields;
using WCell.Constants.Spells;
using WCell.RealmServer.Spells;

namespace ClientTools
{
	public static class RalekProgram
	{
		//public static TextWriter Log = new StreamWriter("GameObjectTypes.txt");

		public static void RalekMain()
		{
			SpellHandler.LoadSpells();

			//ss.FindUsedAuraTypes();
			// SpellStudies.FindAllWithAttribute(SpellAttributes.Flag_8_0x100);
			//ss.FindSpellsWithEffectMasks();
			//ss.DisplayInfo(16205);
			//ss.FindSpellsWithFamilyMask(0x2000, 0, 0);
			//ss.FindSpellsWhere((s) => s.Name.Contains("Mana Spring"), (s)=>"");
			//ss.FindSpellsWithEffect(WCell.Constants.Spells.SpellEffectType.ApplyGlyph);

			/*string exeName = "WoW_3.0.3.9138.exe";
            using (WoWFile file = new WoWFile(exeName, "3.0.3", "9138"))
            {
                string version = string.Format("{0}", file.Version);

                new UpdateFieldExtractor(file).CreatePacketParserInfo("");

                SpellFailureExtractor.Extract(file, "SpellFailedReason_3.0.2.9056.cs");
                UpdateFieldExtractor.DumpEnums(file, "UpdateFields_3.0.3.9138.cs");
            }*/

			//TextWriter writer = new StreamWriter("DBCcompare.txt");
			//new DBCTools.Compare.DBCDirComparer(@"C:\WoW\Clients\World of Warcraft\Data\enUS\3.0.2.9056\DBFilesClient", @"C:\Dev\WCell\Run\RealmServer\Content\dbc").Compare(writer);
			//string exeName = "WoW_2.3.3.7799.exe";
			//string exeName = "WoW_0.4.0.7897.exe";
			//string exeName = "WoW_2.4.1.8125.exe";
			//string exeName = "WoW_0.0.2.8970.exe";
			//string exeName = "WoW_3.0.2.9056.exe";
			//WoWFile oldfile = new WoWFile("WoW_2.4.3.8606.exe", "2.4.3", "8606");
			/*using (WoWFile file = new WoWFile(exeName,"3.0.2","9056"))
            {
                string version = string.Format("{0}", file.Version);

                new UpdateFieldExtractor(file).CreatePacketParserInfo("");
                //UpdateFieldWriter.Write(file);

                //SpellFailureExtractor.Extract(file, "SpellFailedReason_3.0.2.9056.cs");
                //UpdateFieldExtractor.Extract(file, String.Format("UpdateFields_{0}.cs", version));
                //UpdateFieldExtractor.Extract(file);
                //UpdateFieldExtractor.DumpEnums(file, String.Format("UpdateFields_{0}.cs", version));
                //UpdateFieldComparer.Dump(oldfile, file, "Compare");
                //SpellFailureExtractor.Extract(file, String.Format("SpellFailedReason_{0}.cs", version));
                //GameObjectTypeExtractor.Extract(file);
            }*/

			//Log.Close();

			//Console.ReadLine();
		}

		private static void ExtractFromExe(string exeName)
		{
			using (WoWFile file = new WoWFile(exeName))
			{
				UpdateFieldExtractor.DumpEnums(file, String.Format("UpdateFields_{0}.cs", file.Version));
				SpellFailureExtractor.Extract(file, String.Format("SpellFailedReason_{0}.cs", file.Version));
			}
		}

		private static void ByteTesting()
		{
			string allBytes = Console.ReadLine();
			if (String.IsNullOrEmpty(allBytes))
				return;

			string[] bytes = allBytes.Split(' ');
			byte[] arr = new byte[bytes.Length];

			for (int i = 0; i < bytes.Length; i++)
			{
				arr[i] = byte.Parse(bytes[i], System.Globalization.NumberStyles.HexNumber);
			}

			Console.WriteLine("Float: {0}", BitConverter.ToSingle(arr, 0));
			Console.WriteLine("UInt32: {0}", BitConverter.ToUInt32(arr, 0));
		}

		public static void WritePackedGUID(ulong guid)
		{
			//8C 0B D8 00 00 00 00 00
			byte mask = 0;
			List<byte> bytes = new List<byte>();

			for (int i = 0; i < 8; i++)
			{
				byte temp = (byte) (guid >> (i*8));

				if (temp != 0)
				{
					mask |= (byte) (1 << i);
					bytes.Add(temp);
				}
			}

			Console.WriteLine("Mask: {0}", mask);
			foreach (byte b in bytes)
			{
				Console.Write("{0} ", b.ToString("X2"));
			}
			Console.WriteLine();
		}
	}
}