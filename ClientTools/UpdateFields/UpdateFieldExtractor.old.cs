/*************************************************************************
 *
 *   file		: UpdateFieldExtractor.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2008-05-04 13:07:05 +0800 (Sun, 04 May 2008) $
 *   last author	: $LastChangedBy: Ralek $
 *   revision		: $Rev: 318 $
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.CodeDom.Compiler;

using WCell.Core;

namespace WCell.Tools.Ralek
{
	public static class UpdateFieldExtractor
	{
		private static List<UpdateField> m_updateFieldList = new List<UpdateField>();
		static List<UpdateField>[] s_updateFieldsByGroup = new List<UpdateField>[(int)UpdateFieldGroup.Count];


		private static long s_stringStartOffset;
		private static long s_stringOffsetDelta;
		private static long s_dataStartOffset;
		private static int s_fieldCount;

		static int m_objectStart;
		static int m_objectEnd;
		static int m_unitStart;
		static int m_unitEnd;
		static int m_playerStart;
		static int m_playerEnd;
		static int m_gameObjectStart;
		static int m_gameObjectEnd;
		static int m_itemStart;
		static int m_itemEnd;
		static int m_containerStart;
		static int m_containerEnd;
		static int m_corpseStart;
		static int m_corpseEnd;
		static int m_dynamicObjectStart;
		static int m_dynamicObjectEnd;

		static List<string> s_objectExtraFields = new List<string>();
		static List<string> s_itemExtraFields = new List<string>();
		static List<string> s_containerExtraFields = new List<string>();
		static List<string> s_unitExtraFields = new List<string>();
		static List<string> s_playerExtraFields = new List<string>();
		static List<string> s_gameObjectExtraFields = new List<string>();
		static List<string> s_dynamicObjectExtraFields = new List<string>();
		static List<string> s_corpseExtraFields = new List<string>();

		static int s_totalObjectSize;
		static int s_totalItemSize;
		static int s_totalContainerSize;
		static int s_totalUnitSize;
		static int s_totalPlayerSize;
		static int s_totalGameObjectSize;
		static int s_totalDynamicObjectSize;
		static int s_totalCorpseSize;

		public static List<UpdateField>[] Extract(WoWFile wowFile)
		{
			FindStringOffset(wowFile);
			FindDataOffset(wowFile);

			FillList(wowFile);

			FillStartAndEndValues();

			AdjustOffsets();

			return s_updateFieldsByGroup;
		}

		public static bool DumpEnums(WoWFile wowFile, string outputFileName)
		{
			FindStringOffset(wowFile);
			FindDataOffset(wowFile);

			FillList(wowFile);

			FillStartAndEndValues();

			// not included in release builds apparently :/
			//FillExtraFieldList(wowFile);

			AdjustOffsets();

			WriteToFile(wowFile, outputFileName);

			Console.WriteLine("UpdateFields Extracted Successfully to: " + outputFileName);

			//FlagFinder(FieldFlag.Flag_0x10);
			//FlagFinder(FieldFlag.Flag_0x20);
			//FieldTypeFinder(FieldType.TwoInt16);

			return true;
		}

		#region Properties
		public static List<UpdateField> Fields
		{
			get
			{
				return m_updateFieldList;
			}
		}
		#endregion

		#region Utility

		static void WritePublicFields()
		{
			var query = from field in m_updateFieldList
						where ((field.Flags & FieldFlag.Public) == FieldFlag.Public)
						select field;

			foreach (var field in query)
			{
				Console.WriteLine("SetPublicField({0}, {1}); // {2}", field.Offset, field.Size, field.Name);
			}
		}

		static void FlagFinder(FieldFlag toSearch)
		{
			var query = from field in m_updateFieldList
						where (field.Flags & toSearch) == toSearch
						select field;

			Console.WriteLine("Flag: {0}", toSearch);
			foreach (var field in query)
			{
				Console.WriteLine("{0}: {1} - {2}", field.Size, field.Name, field.Flags);
			}
		}

		static void FieldTypeFinder(UpdateFieldType toSearch)
		{
			var query = from field in m_updateFieldList
						where field.Type == toSearch
						select field;

			Console.WriteLine("Type: {0}", toSearch);
			foreach (var field in query)
			{
				Console.WriteLine("{0}: {1} - {2}", field.Size, field.Name, field.Flags);
			}
		}

		#endregion

		private static void FindStringOffset(WoWFile wowFile)
		{
			// Should be 3853356 for 2.1.3
			int index = wowFile.FileString.IndexOf("OBJECT_FIELD_GUID\0");

			if (index > 0)
			{
				s_stringStartOffset = index;
			}
			else
			{
				Console.WriteLine("String Offset Not Found!");
			}
		}
		private static void FindDataOffset(WoWFile wowFile)
		{
			byte[] temp = new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };

			StringBuilder sb = new StringBuilder();
			foreach (byte b in temp)
			{
				sb.Append((char)b);
			}
			string toFind = sb.ToString();

			// Should be at 6896020 for 2.1.3
			int index = wowFile.FileString.IndexOf(toFind);

			if (index > 0)
			{
				s_dataStartOffset = index - 4;
			}
			else
			{
				Console.WriteLine("Data Offset Not Found!");
			}
		}
		private static void FillList(BinaryReader binReader)
		{
			s_fieldCount = 0;

			string previousField = String.Empty;
			string currentField;

			binReader.BaseStream.Position = s_dataStartOffset;

			while (true)
			{
				UpdateField field = new UpdateField();
				// 4A4C90 for OBJECT_FIELD_GUID (4869264)
				field.NameOffset = binReader.ReadUInt32();

				if (field.NameOffset < 0x9999)
				{
					uint oldNameOffset = field.NameOffset;
					field.NameOffset = binReader.ReadUInt32();
				}

				field.Offset = binReader.ReadUInt32();
				field.Size = binReader.ReadUInt32();
				field.Type = (UpdateFieldType)binReader.ReadUInt32();
				field.Flags = (FieldFlag)binReader.ReadUInt32();

				if (s_fieldCount == 0)
				{
					// 0x401A00
					s_stringOffsetDelta = field.NameOffset - s_stringStartOffset;
				}

				long stringOffset = field.NameOffset - s_stringOffsetDelta;

				long oldpos = binReader.BaseStream.Position;
				binReader.BaseStream.Position = stringOffset;
				currentField = binReader.ReadCString();
				binReader.BaseStream.Position = oldpos;

				StringBuilder sb = new StringBuilder();
				sb.AppendFormat("Size: {0} - ", field.Size);
				sb.AppendFormat("Type: {0} - ", field.Type);
				sb.AppendFormat("Flags: {0}", field.Flags);
				field.Description = sb.ToString();

				field.Name = currentField;

				m_updateFieldList.Add(field);

				s_fieldCount++;

				if (!previousField.Equals("CORPSE_FIELD_PAD") && currentField.Equals("CORPSE_FIELD_PAD"))
					break;

				previousField = currentField;
			}
		}
		private static void FillStartAndEndValues()
		{
			for (int i = 0; i < s_fieldCount; i++)
			{
				UpdateField field = m_updateFieldList[i];
				if (field.Name.StartsWith("OBJECT"))
				{
					var groupList = s_updateFieldsByGroup.Get((uint)UpdateFieldGroup.Object);
					if (groupList == null)
					{
						s_updateFieldsByGroup[(uint)UpdateFieldGroup.Object] = groupList = new List<UpdateField>(200);
					}
					groupList.Add(field);

					// workaround for GameObjectFields first field being named OBJECT_FIELD_CREATED_BY
					if (field.Name.StartsWith("OBJECT_FIELD_CREATED_BY"))
					{
						AddToGroup(field, UpdateFieldGroup.GameObject);

						if (m_gameObjectStart == 0)
						{
							m_gameObjectStart = i;
							m_gameObjectEnd = m_gameObjectStart;
						}
						m_gameObjectEnd++;
						s_totalGameObjectSize += (int)field.Size;
						continue;
					}
					AddToGroup(field, UpdateFieldGroup.Object);

					if (field.Offset == 0)
					{
						m_objectStart = i;
						m_objectEnd = m_objectStart;
					}
					m_objectEnd++;
					s_totalObjectSize += (int)field.Size;
				}

				if (field.Name.StartsWith("ITEM"))
				{
					AddToGroup(field, UpdateFieldGroup.Item);

					if (field.Offset == 0)
					{
						m_itemStart = i;
						m_itemEnd = m_itemStart;
					}
					m_itemEnd++;
					s_totalItemSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("CONTAINER"))
				{
					AddToGroup(field, UpdateFieldGroup.Container);

					if (field.Offset == 0)
					{
						m_containerStart = i;
						m_containerEnd = m_containerStart;
					}
					m_containerEnd++;
					s_totalContainerSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("GAMEOBJECT"))
				{
					AddToGroup(field, UpdateFieldGroup.GameObject);

					// Dont need to check for first because first GameObject field is OBJECT_FIELD_CREATED_BY
					m_gameObjectEnd++;
					s_totalGameObjectSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("UNIT"))
				{
					AddToGroup(field, UpdateFieldGroup.Unit);

					if (field.Offset == 0)
					{
						m_unitStart = i;
						m_unitEnd = m_unitStart;
					}
					m_unitEnd++;
					s_totalUnitSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("PLAYER"))
				{
					AddToGroup(field, UpdateFieldGroup.Player);

					if (field.Offset == 0)
					{
						m_playerStart = i;
						m_playerEnd = m_playerStart;
					}
					m_playerEnd++;
					s_totalPlayerSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("CORPSE"))
				{
					AddToGroup(field, UpdateFieldGroup.Corpse);

					if (field.Offset == 0)
					{
						m_corpseStart = i;
						m_corpseEnd = m_corpseStart;
					}
					m_corpseEnd++;
					s_totalCorpseSize += (int)field.Size;
					continue;
				}

				if (field.Name.StartsWith("DYNAMIC"))
				{
					AddToGroup(field, UpdateFieldGroup.DynamicObject);

					if (field.Offset == 0)
					{
						m_dynamicObjectStart = i;
						m_dynamicObjectEnd = m_dynamicObjectStart;
					}
					m_dynamicObjectEnd++;
					s_totalDynamicObjectSize += (int)field.Size;
					continue;
				}
			}
		}

		static void AddToGroup(UpdateField field, UpdateFieldGroup group)
		{
			var groupList = s_updateFieldsByGroup.Get((uint)group);
			if (groupList == null)
			{
				s_updateFieldsByGroup[(uint)group] = groupList = new List<UpdateField>(200);
			}
			groupList.Add(field);

			field.Group = group;
		}

		private static void FillExtraFieldList(BinaryReader binReader)
		{
			long startPos = binReader.BaseStream.Position;
			string currentField;

			#region ObjectFields

			//long objectLookupTableOffset = 0x7AEB28;
			//long objectLookupTableOffset = 0x7BB958;
			//long objectLookupTableOffset = 0x7BBB60;

			long objectLookupTableOffset = 0x587D10; // 0.4.0.7897
			binReader.BaseStream.Position = objectLookupTableOffset;
			for (int i = 0; i < s_totalObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_objectExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region ItemFields

			//long itemLookupTableOffset = 0x7AEC00;
			//long itemLookupTableOffset = 0x7BBA30;
			//long itemLookupTableOffset = 0x7BBC38; // 0.3.0.7543

			long itemLookupTableOffset = 0x5884A8;// 0.4.0.7897

			binReader.BaseStream.Position = itemLookupTableOffset;

			for (int i = 0; i < s_totalItemSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_itemExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region ContainerFields

			//long containerLookupTableOffset = 0x7AEE00;
			//long containerLookupTableOffset = 0x7BBC30;
			//long containerLookupTableOffset = 0x7BBE38; // 0.3.0.7543

			long containerLookupTableOffset = 0x8156B8; // 0.4.0.7897

			binReader.BaseStream.Position = containerLookupTableOffset;

			for (int i = 0; i < s_totalContainerSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_containerExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region UnitFields

			//long unitLookupTableOffset = 0x7AF258;
			//long unitLookupTableOffset = 0x7BC088;
			//long unitLookupTableOffset = 0x7BC290; // 0.3.0.7543

			long unitLookupTableOffset = 0x588060;// 0.4.0.7897

			binReader.BaseStream.Position = unitLookupTableOffset;

			for (int i = 0; i < s_totalUnitSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_unitExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region PlayerFields

			//long playerLookupTableOffset = 0x7B06C8;
			//long playerLookupTableOffset = 0x7BD4F8;
			//long playerLookupTableOffset = 0x7BD700; // 0.3.0.7543

			long playerLookupTableOffset = 0x816A00; // 0.4.0.7897

			binReader.BaseStream.Position = playerLookupTableOffset;

			for (int i = 0; i < s_totalPlayerSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_playerExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region GameObjectFields

			//long gameObjectLookupTableOffset = 0x7B19F0;
			//long gameObjectLookupTableOffset = 0x7BE820;
			//long gameObjectLookupTableOffset = 0x7BEA20; // 0.3.0.7543

			long gameObjectLookupTableOffset = 0x5885A0; // 0.4.0.7897

			binReader.BaseStream.Position = gameObjectLookupTableOffset;

			for (int i = 0; i < s_totalGameObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_gameObjectExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region DynamicObjectFields

			//long dynamicObjectLookupTableOffset = 0x7B1A44;
			//long dynamicObjectLookupTableOffset = 0x7BE874;
			//long dynamicObjectLookupTableOffset = 0x7BEA74; // 0.3.0.7543

			long dynamicObjectLookupTableOffset = 0x817E3C; // 0.4.0.7897

			binReader.BaseStream.Position = dynamicObjectLookupTableOffset;

			for (int i = 0; i < s_totalDynamicObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_dynamicObjectExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region CorpseFields

			//long corpseLookupTableOffset = 0x7B1A80;
			//long corpseLookupTableOffset = 0x7BE8B0;
			//long corpseLookupTableOffset = 0x7BEAB0; // 0.3.0.7543

			long corpseLookupTableOffset = 0x817E78; // 0.4.0.7897

			binReader.BaseStream.Position = corpseLookupTableOffset;

			for (int i = 0; i < s_totalCorpseSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long)binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - s_stringOffsetDelta;
					currentField = binReader.ReadCString();
					s_corpseExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			binReader.BaseStream.Position = startPos;
		}

		private static void AdjustOffsets()
		{
			uint objectDelta = m_updateFieldList[m_objectEnd - 1].Offset + m_updateFieldList[m_objectEnd - 1].Size;
			uint unitDelta = m_updateFieldList[m_unitEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;
			uint itemDelta = m_updateFieldList[m_itemEnd - 1].Offset + m_updateFieldList[m_itemEnd - 1].Size;

			UpdateField field;

			for (int i = m_unitStart; i < m_unitEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_playerStart; i < m_playerEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta + unitDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_itemStart; i < m_itemEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_containerStart; i < m_containerEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta + itemDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_dynamicObjectStart; i < m_dynamicObjectEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_gameObjectStart; i < m_gameObjectEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta;
				m_updateFieldList[i] = field;
			}

			for (int i = m_corpseStart; i < m_corpseEnd; i++)
			{
				field = m_updateFieldList[i];
				field.Offset += objectDelta;
				m_updateFieldList[i] = field;
			}
		}

		private static void WriteField(UpdateField field, TextWriter writer)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0} = {1},", field.Name, field.Offset);
		}

		private static void WriteFieldFromArray(List<string> array, TextWriter writer, UpdateField field, int fieldStart, int id)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0} = {1},", array[fieldStart + id], field.Offset + id);
		}

		private static void WriteArrayField(TextWriter writer, UpdateField field, int num)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0}_{1} = {2},", field.Name, num + 1, field.Offset + num);
		}

		private static void WriteToFile(WoWFile wowFile, string outputFile)
		{
			uint objectDelta = m_updateFieldList[m_objectEnd - 1].Offset + m_updateFieldList[m_objectEnd - 1].Size;
			uint unitDelta = m_updateFieldList[m_unitEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;
			uint itemDelta = m_updateFieldList[m_itemEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;

			using (StreamWriter writer = new StreamWriter(outputFile))
			{
				writer.WriteLine();
				writer.WriteLine("// UpdateFields generated for build {0}.{1}", wowFile.Version, wowFile.Build);
				writer.WriteLine();

				writer.WriteLine("namespace WCell.Core");
				writer.WriteLine("{");

				DumpEnum(m_objectStart, m_objectEnd, 0, "ObjectFields", s_objectExtraFields, writer);

				DumpEnum(m_unitStart, m_unitEnd, objectDelta, "UnitFields", s_unitExtraFields, writer);

				DumpEnum(m_playerStart, m_playerEnd, unitDelta, "PlayerFields", s_playerExtraFields, writer);

				DumpEnum(m_itemStart, m_itemEnd, objectDelta, "ItemFields", s_itemExtraFields, writer);

				DumpEnum(m_containerStart, m_containerEnd, itemDelta, "ContainerFields", s_containerExtraFields, writer);

				DumpEnum(m_dynamicObjectStart, m_dynamicObjectEnd, objectDelta, "DynamicObjectFields", s_dynamicObjectExtraFields, writer);

				DumpEnum(m_gameObjectStart, m_gameObjectEnd, objectDelta, "GameObjectFields", s_gameObjectExtraFields, writer);

				DumpEnum(m_corpseStart, m_corpseEnd, objectDelta, "CorpseFields", s_corpseExtraFields, writer);

				writer.WriteLine("}");
			}
		}

		static void DumpEnum(int start, int end, uint enumOffset, string enumName, List<string> extras, TextWriter writer)
		{
			UpdateField field;

			writer.WriteLine("\tpublic enum " + enumName);
			writer.WriteLine("\t{");
			for (int i = start; i < end; i++)
			{
				field = m_updateFieldList[i];

				WriteField(field, writer);

				if (field.Size > 1)
				{
					int pos = (int)(field.Offset - enumOffset);
					if (extras != null && extras.Count > 0)
					{
						for (int size = 1; size < field.Size; size++)
						{
							WriteFieldFromArray(extras, writer, field, pos, size);
						}
					}
					else
					{
						for (int size = 1; size < field.Size; size++)
						{
							WriteArrayField(writer, field, size);
						}
					}
				}

				/*if (i == m_corpseEnd - 1)
				{
					sw.WriteLine("\t\tCORPSE_END = {0}", field.Offset + field.Size);
					break;
				}*/

			}
			writer.WriteLine("\t}");

			writer.WriteLine();
		}

		/// <summary>
		/// Creates necessary information for the Update-PacketParser
		/// </summary>
		public static void CreatePacketParserInfo(string wowFile, string outputFile)
		{
			WoWFile file = new WoWFile(wowFile);
			var fields = UpdateFieldExtractor.Extract(file);

			using (var writer = new StreamWriter(outputFile))
			{
				for (UpdateFieldGroup g = UpdateFieldGroup.Object; g < UpdateFieldGroup.Count; g++)
				{
					foreach (var field in s_updateFieldsByGroup[(int)g]) {
						var size = field.Size;

						if (size > 1)
						{
							writer.WriteLine("SetType({0}, {1}, {2});",
								field.FullTypeName, field.FullName, field.Size);
						}
						else
						{
							writer.WriteLine("FieldTypes[{0}] = {1};", field.FullName, field.FullTypeName);
						}
					}
				}
			}
		}
	}

	public class UpdateField
	{
		public uint NameOffset;
		public uint Offset;
		public uint Size;
		public UpdateFieldType Type;
		public FieldFlag Flags;

		public string Name;
		public string Description;
		public UpdateFieldGroup Group;

		public string FullName
		{
			get
			{
				return Group + "Fields." + Name;
			}
		}

		public string FullTypeName
		{
			get
			{
				return "UpdateFieldType." + Type;
			}
		}
	}

	public enum UpdateFieldGroup : uint
	{
		Object,
		Unit,
		Player,
		GameObject,
		DynamicObject,
		Item,
		Container,
		Corpse,
		Count
	}

	[Flags]
	public enum FieldFlag
	{
		None = 0,
		/// <summary>
		/// Fields with this flag are to be known by all surrounding players
		/// </summary>
		Public = 0x1,
		/// <summary>
		/// Fields with this flag are only meant to be known by the player itself
		/// </summary>
		Private = 0x2,
		/// <summary>
		/// Fields with this flag are to be known by the owner, in the case of pets and a few item fields
		/// </summary>
		OnlyForOwner = 0x4,
		/// <summary>
		/// Unused
		/// </summary>
		Flag_0x8_Unused = 0x8,
		/// <summary>
		/// ITEM_FIELD_STACK_COUNT
		/// ITEM_FIELD_DURATION
		/// ITEM_FIELD_SPELL_CHARGES
		/// ITEM_FIELD_DURABILITY
		/// ITEM_FIELD_MAXDURABILITY
		/// </summary>
		Flag_0x10 = 0x10,
		/// <summary>
		/// UNIT_FIELD_MINDAMAGE
		/// UNIT_FIELD_MAXDAMAGE
		/// UNIT_FIELD_MINOFFHANDDAMAGE
		/// UNIT_FIELD_MAXOFFHANDDAMAGE
		/// UNIT_FIELD_RESISTANCES
		/// </summary>
		Flag_0x20 = 0x20,
		/// <summary>
		/// Fields with this flag are only to be known by party members
		/// </summary>
		GroupOnly = 0x40,
		/// <summary>
		/// Unused
		/// </summary>
		Flag_0x80_Unused = 0x80,
		/// <summary>
		/// UNIT_FIELD_HEALTH - Flag0x0100
		/// UNIT_FIELD_MAXHEALTH - Flag0x0100
		/// UNIT_DYNAMIC_FLAGS - Flag0x0100
		/// GAMEOBJECT_DYN_FLAGS - Flag0x0100
		/// GAMEOBJECT_ANIMPROGRESS - Flag0x0100
		/// CORPSE_FIELD_DYNAMIC_FLAGS - Flag0x0100
		/// Differs from player to player
		/// In the case of health, it sends percents to everyone not in your party instead of the acutal value
		/// </summary>
		Dynamic = 0x100,
	}

	public enum UpdateFieldType
	{
		None = 0,
		UInt32 = 1,
		TwoInt16 = 2,
		Float = 3,
		GUID = 4,
		ByteArray = 5,
	}
}
