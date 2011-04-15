/*************************************************************************
 *
 *   file		: SpellFailureExtractor.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2009-03-07 14:58:12 +0800 (Sat, 07 Mar 2009) $
 *   last author	: $LastChangedBy: ralekdev $
 *   revision		: $Rev: 784 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using WCell.Tools.Code;
using WCell.Tools.Ralek;
using WCell.Util;

namespace ClientTools.Extractors
{
	public static class SpellFailureExtractor
	{
		private const string FirstFailure = "SPELL_FAILED_UNKNOWN";
		private const string LastFailure = "SPELL_FAILED_SUCCESS";

		public static readonly List<string> SpellFailures = new List<string>();

		private static readonly Dictionary<int, string> unfindableMap = new Dictionary<int, string>
		{
			{ 76,  "SPELL_FAILED_NO_CHARGES_REMAIN" },
			{ 173, "SPELL_FAILED_CANT_DO_THAT_RIGHT_NOW" }
		};

		public static void Extract(WoWFile wowFile, string outputFileName)
		{
			wowFile.BaseStream.Position = FindStartingOffset(wowFile);
			long endPosition = FindEndingOffset(wowFile);

			while (wowFile.BaseStream.Position < endPosition)
			{
				string name = wowFile.ReadCString();
				SpellFailures.Add(name);

				while ((char)wowFile.PeekByte() != 'S')
				{
					wowFile.BaseStream.Position++;
				}
			}

			SpellFailures.Reverse();
			DumpToFile(outputFileName);

			Console.WriteLine("SpellFailure Enum Extracted Successfully");
		}

		private static void DumpToFile(string outputFileName)
		{
			using (var writer = new CodeFileWriter(outputFileName, "WCell.Constants.Spells", "SpellFailedReason : byte", "enum", ""))
			{
				var len = SpellFailures.Count;
				var val = 0;
				for (var i = 0; i < len; i++, val++)
				{
					if (unfindableMap.ContainsKey(i))
					{
						WriteVal(writer, unfindableMap[i], i);
						val++;
					}

					WriteVal(writer, SpellFailures[i], val);
				}
				writer.WriteLine("Ok = 0xFF");
			}
		}

		static void WriteVal(CodeFileWriter writer, string name, int value)
		{
			name = name.Replace("SPELL_FAILED_", "");
			name = name.ToCamelCase();
			writer.WriteLine("{0} = {1},", name, value);
		}

		private static long FindStartingOffset(WoWFile wowFile)
		{
			return wowFile.FileString.IndexOf(FirstFailure);
		}

		private static long FindEndingOffset(WoWFile wowFile)
		{
			return wowFile.FileString.IndexOf(LastFailure) + LastFailure.Length;
		}
	}
}