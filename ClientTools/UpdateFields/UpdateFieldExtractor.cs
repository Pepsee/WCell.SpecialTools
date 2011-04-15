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
using System.Text;
using WCell.Constants;
using WCell.Constants.Updates;
using WCell.Tools.Ralek;
using WCell.Tools.Ralek.UpdateFields;
using WCell.Util;
using WCell.Util.Toolshed;

namespace ClientTools.UpdateFields
{
	public class UpdateFieldExtractor
	{
		private readonly List<ExtendedUpdateField> m_updateFieldList = new List<ExtendedUpdateField>();
		private readonly ExtendedUpdateField[][] m_updateFieldsByGroup = new ExtendedUpdateField[UpdateField.ObjectTypeCount][];


		private long m_stringStartOffset;
		private long m_stringOffsetDelta;
		private long m_dataStartOffset;
		private int m_fieldCount;

		private int m_objectStart;
		private int m_objectEnd;
		private int m_unitStart;
		private int m_unitEnd;
		private int m_playerStart;
		private int m_playerEnd;
		private int m_gameObjectStart;
		private int m_gameObjectEnd;
		private int m_itemStart;
		private int m_itemEnd;
		private int m_containerStart;
		private int m_containerEnd;
		private int m_corpseStart;
		private int m_corpseEnd;
		private int m_dynamicObjectStart;
		private int m_dynamicObjectEnd;

		private List<string> m_objectExtraFields = new List<string>();
		private List<string> m_itemExtraFields = new List<string>();
		private List<string> m_containerExtraFields = new List<string>();
		private List<string> m_unitExtraFields = new List<string>();
		private List<string> m_playerExtraFields = new List<string>();
		private List<string> m_gameObjectExtraFields = new List<string>();
		private List<string> m_dynamicObjectExtraFields = new List<string>();
		private List<string> m_corpseExtraFields = new List<string>();

		private int m_totalObjectSize;
		private int m_totalItemSize;
		private int m_totalContainerSize;
		private int m_totalUnitSize;
		private int m_totalPlayerSize;
		private int m_totalGameObjectSize;
		private int m_totalDynamicObjectSize;
		private int m_totalCorpseSize;

		private WoWFile wowFile;

		public UpdateFieldExtractor(WoWFile wowFile)
		{
			this.wowFile = wowFile;
		}

		public void Process()
		{
			//if (m_updateFieldList == null)
			if (m_updateFieldList.Count == 0)
			{
				FindStringOffset();
				FindDataOffset();

				FillList(wowFile);

				FillStartAndEndValues();

				FillGroupList();
			}
		}

		[NoTool]
		public static ExtendedUpdateField[][] Extract(WoWFile file)
		{
			return new UpdateFieldExtractor(file).Extract();
		}

		public ExtendedUpdateField[][] Extract()
		{
			Process();

			return m_updateFieldsByGroup;
		}

		public static bool DumpEnums(WoWFile file, string outputFile)
		{
			return new UpdateFieldExtractor(file).DumpEnums(outputFile);
		}

		public bool DumpEnums(string outputFileName)
		{
			Process();

			// not included in release builds apparently :/
			//FillExtraFieldList(wowFile);
			WriteToFile(wowFile, outputFileName);

			Console.WriteLine("UpdateFields Extracted Successfully to: " + outputFileName);

			//FlagFinder(FieldFlag.Flag_0x10);
			//FlagFinder(FieldFlag.Flag_0x20);
			//FieldTypeFinder(FieldType.TwoInt16);

			return true;
		}

		#region Properties

		public List<ExtendedUpdateField> FieldList
		{
			get { return m_updateFieldList; }
		}

		public ExtendedUpdateField[][] FieldByGroups
		{
			get { return m_updateFieldsByGroup; }
		}

		#endregion

		#region Utility

		private void WritePublicFields()
		{
			var query = from field in m_updateFieldList
			            where ((field.Flags & UpdateFieldFlags.Public) == UpdateFieldFlags.Public)
			            select field;

			foreach (var field in query)
			{
				Console.WriteLine("SetPublicField({0}, {1}); // {2}", field.Offset, field.Size, field.Name);
			}
		}

		private void FlagFinder(UpdateFieldFlags toSearch)
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

		private void FieldTypeFinder(UpdateFieldType toSearch)
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

		private void FindStringOffset()
		{
			// 3853356 for 2.1.3
			// 4722268 for 2.3?
			int index = wowFile.FileString.IndexOf("OBJECT_FIELD_GUID\0");

			if (index > 0)
			{
				m_stringStartOffset = index;
			}
			else
			{
				Console.WriteLine("String Offset Not Found!");
			}
		}

		private void FindDataOffset()
		{
			byte[] temp = new byte[16]
			              	{0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00};

			StringBuilder sb = new StringBuilder();
			foreach (byte b in temp)
			{
				sb.Append((char) b);
			}
			string toFind = sb.ToString();

			// Should be at 6896020 for 2.1.3
			int index = wowFile.FileString.IndexOf(toFind);

			if (index > 0)
			{
				m_dataStartOffset = index - 4;
			}
			else
			{
				Console.WriteLine("Data Offset Not Found!");
			}
		}

		private void FillList(BinaryReader binReader)
		{
			m_fieldCount = 0;

			string previousField = String.Empty;
			string currentField;

			binReader.BaseStream.Position = m_dataStartOffset;

			while (true)
			{
				var field = new ExtendedUpdateField
				            	{
				            		NameOffset = binReader.ReadUInt32()
				            	};
				// 4A4C90 for GUID (4869264)

				if (field.NameOffset < 0x9999)
				{
					uint oldNameOffset = field.NameOffset;
					field.NameOffset = binReader.ReadUInt32();
				}

				field.Offset = binReader.ReadUInt32();
				field.Size = binReader.ReadUInt32();
				field.Type = (UpdateFieldType) binReader.ReadUInt32();
				field.Flags = (UpdateFieldFlags) binReader.ReadUInt32();

				if (m_fieldCount == 0)
				{
					// 0x401A00
					m_stringOffsetDelta = field.NameOffset - m_stringStartOffset;
				}

				long stringOffset = field.NameOffset - m_stringOffsetDelta;

				long oldpos = binReader.BaseStream.Position;
				binReader.BaseStream.Position = stringOffset;
				currentField = binReader.ReadCString();
				binReader.BaseStream.Position = oldpos;

				var sb = new StringBuilder();
				sb.AppendFormat("Size: {0} - ", field.Size);
				sb.AppendFormat("Type: {0} - ", field.Type);
				sb.AppendFormat("Flags: {0}", field.Flags);
				field.Description = sb.ToString();

				field.Name = currentField;

				m_updateFieldList.Add(field);

				m_fieldCount++;

                if (!previousField.Equals("CORPSE_FIELD_DYNAMIC_FLAGS") && currentField.Equals("CORPSE_FIELD_DYNAMIC_FLAGS"))
				{
					break;
				}

				previousField = currentField;
			}
		}

		private void FillStartAndEndValues()
		{
			for (int i = 0; i < m_fieldCount; i++)
			{
				ExtendedUpdateField field = m_updateFieldList[i];
				if (field.Name.StartsWith("OBJECT"))
				{
					// workaround for GameObjectFields first field being named CREATED_BY
					if (field.Name.StartsWith("OBJECT_FIELD_CREATED_BY"))
					{
						if (m_gameObjectStart == 0)
						{
							m_gameObjectStart = i;
							m_gameObjectEnd = m_gameObjectStart;
						}
						m_gameObjectEnd++;
						m_totalGameObjectSize += (int) field.Size;

						field.Group = ObjectTypeId.GameObject;
					}
					else
					{
						if (field.Offset == 0)
						{
							m_objectStart = i;
							m_objectEnd = m_objectStart;
						}
						m_objectEnd++;
						m_totalObjectSize += (int) field.Size;

						field.Group = ObjectTypeId.Object;
					}
				}
				else if (field.Name.StartsWith("ITEM"))
				{
					if (field.Offset == 0)
					{
						m_itemStart = i;
						m_itemEnd = m_itemStart;
					}
					m_itemEnd++;
					m_totalItemSize += (int) field.Size;

					field.Group = ObjectTypeId.Item;
				}
				else if (field.Name.StartsWith("CONTAINER"))
				{
					if (field.Offset == 0)
					{
						m_containerStart = i;
						m_containerEnd = m_containerStart;
					}
					m_containerEnd++;
					m_totalContainerSize += (int) field.Size;

					field.Group = ObjectTypeId.Container;
				}
				else if (field.Name.StartsWith("GAMEOBJECT"))
				{
					// Dont need to check for first because first GameObject field is CREATED_BY
					m_gameObjectEnd++;
					m_totalGameObjectSize += (int) field.Size;

					field.Group = ObjectTypeId.GameObject;
				}
				else if (field.Name.StartsWith("UNIT"))
				{
					if (field.Offset == 0)
					{
						m_unitStart = i;
						m_unitEnd = m_unitStart;
					}
					m_unitEnd++;
					m_totalUnitSize += (int) field.Size;

					field.Group = ObjectTypeId.Unit;
				}
				else if (field.Name.StartsWith("PLAYER"))
				{
					if (field.Offset == 0)
					{
						m_playerStart = i;
						m_playerEnd = m_playerStart;
					}
					m_playerEnd++;
					m_totalPlayerSize += (int) field.Size;

					field.Group = ObjectTypeId.Player;
				}
				else if (field.Name.StartsWith("CORPSE"))
				{
					if (field.Offset == 0)
					{
						m_corpseStart = i;
						m_corpseEnd = m_corpseStart;
					}
					m_corpseEnd++;
					m_totalCorpseSize += (int) field.Size;

					field.Group = ObjectTypeId.Corpse;
				}
				else if (field.Name.StartsWith("DYNAMIC"))
				{
					if (field.Offset == 0)
					{
						m_dynamicObjectStart = i;
						m_dynamicObjectEnd = m_dynamicObjectStart;
					}
					m_dynamicObjectEnd++;
					m_totalDynamicObjectSize += (int) field.Size;

					field.Group = ObjectTypeId.DynamicObject;
				}
				else
				{
					Console.WriteLine("WARNING: Field was ignored - " + field.Name);
				}
			}
		}

		private void FillGroupList()
		{
			var sizes = SetupGroups();

			for (ObjectTypeId group = ObjectTypeId.Object; group < (ObjectTypeId) UpdateField.ObjectTypeCount; group++)
			{
				var size = sizes[(int) group];
				Array.Resize(ref m_updateFieldsByGroup[(int) group], (int) size);
			}
		}

		private uint[] SetupGroups()
		{
			uint[] sizes = new uint[UpdateField.ObjectTypeCount];

			// get the correct order
			var fields = new List<ExtendedUpdateField>[UpdateField.ObjectTypeCount];
			for (int i = 0; i < fields.Length; i++)
			{
				fields[i] = new List<ExtendedUpdateField>(500);
			}
			foreach (var field in m_updateFieldList)
			{
				fields[(int) field.Group].Add(field);
			}

			foreach (var fieldArr in fields)
			{
				foreach (var field in fieldArr)
				{
					field.Offset += GetGroupOffset(field.Group);

					if (!field.Name.StartsWith("PLAYER_FIELD_BYTES"))
					{
						var str = field.Group.ToString().ToUpper() + "_";
						if (field.Name.StartsWith(str))
						{
							field.Name = field.Name.Substring(str.Length);
						}
						str = "FIELD_";
						if (field.Name.StartsWith(str))
						{
							field.Name = field.Name.Substring(str.Length);
						}
					}

					if (m_updateFieldsByGroup.Get((uint) field.Group) == null)
					{
						m_updateFieldsByGroup[(uint) field.Group] = new ExtendedUpdateField[500];
					}
					ArrayUtil.Set(ref m_updateFieldsByGroup[(uint) field.Group], field.Offset, field);

					var size = field.Offset + field.Size;
					if (sizes[(int) field.Group] < size)
					{
						sizes[(int) field.Group] = size;
					}
				}
			}
			return sizes;
		}

		private void FillExtraFieldList(BinaryReader binReader)
		{
			long startPos = binReader.BaseStream.Position;
			string currentField;

			#region ObjectFields

			//long objectLookupTableOffset = 0x7AEB28;
			//long objectLookupTableOffset = 0x7BB958;
			//long objectLookupTableOffset = 0x7BBB60;

			long objectLookupTableOffset = 0x587D10; // 0.4.0.7897
			binReader.BaseStream.Position = objectLookupTableOffset;
			for (int i = 0; i < m_totalObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_objectExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region ItemFields

			//long itemLookupTableOffset = 0x7AEC00;
			//long itemLookupTableOffset = 0x7BBA30;
			//long itemLookupTableOffset = 0x7BBC38; // 0.3.0.7543

			long itemLookupTableOffset = 0x5884A8; // 0.4.0.7897

			binReader.BaseStream.Position = itemLookupTableOffset;

			for (int i = 0; i < m_totalItemSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_itemExtraFields.Add(currentField);
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

			for (int i = 0; i < m_totalContainerSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_containerExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			#region UnitFields

			//long unitLookupTableOffset = 0x7AF258;
			//long unitLookupTableOffset = 0x7BC088;
			//long unitLookupTableOffset = 0x7BC290; // 0.3.0.7543

			long unitLookupTableOffset = 0x588060; // 0.4.0.7897

			binReader.BaseStream.Position = unitLookupTableOffset;

			for (int i = 0; i < m_totalUnitSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_unitExtraFields.Add(currentField);
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

			for (int i = 0; i < m_totalPlayerSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_playerExtraFields.Add(currentField);
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

			for (int i = 0; i < m_totalGameObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_gameObjectExtraFields.Add(currentField);
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

			for (int i = 0; i < m_totalDynamicObjectSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_dynamicObjectExtraFields.Add(currentField);
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

			for (int i = 0; i < m_totalCorpseSize; i++)
			{
				long pos = binReader.BaseStream.Position;
				long nameOffset = (long) binReader.ReadUInt32();
				if (nameOffset > 0)
				{
					binReader.BaseStream.Position = nameOffset - m_stringOffsetDelta;
					currentField = binReader.ReadCString();
					m_corpseExtraFields.Add(currentField);
					binReader.BaseStream.Position = pos + 4;
				}
			}

			#endregion

			binReader.BaseStream.Position = startPos;
		}

		public uint ObjectLength
		{
			get { return m_updateFieldList[m_objectEnd - 1].Offset + m_updateFieldList[m_objectEnd - 1].Size; }
		}

		public uint GetGroupOffset(ObjectTypeId group)
		{
			if (group == ObjectTypeId.Unit ||
			    group == ObjectTypeId.GameObject ||
			    group == ObjectTypeId.DynamicObject ||
			    group == ObjectTypeId.Corpse ||
			    group == ObjectTypeId.Item)
			{
				return ObjectLength;
			}
			else if (group == ObjectTypeId.Player)
			{
				return m_updateFieldList[m_unitEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;
			}
			else if (group == ObjectTypeId.Container)
			{
				return m_updateFieldList[m_itemEnd - 1].Offset + m_updateFieldList[m_itemEnd - 1].Size;
			}
			return 0;
		}

		private void WriteField(ExtendedUpdateField field, TextWriter writer)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0} = {1},", field.Name, field.Offset);
		}

		private void WriteFieldFromArray(List<string> array, TextWriter writer, ExtendedUpdateField field, int fieldStart,
		                                 int id)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0} = {1},", array[fieldStart + id], field.Offset + id);
		}

		private void WriteArrayField(TextWriter writer, ExtendedUpdateField field, int num)
		{
			writer.WriteLine("\t\t/// <summary>");
			writer.WriteLine("\t\t/// {0}", field.Description);
			writer.WriteLine("\t\t/// </summary>");
			writer.WriteLine("\t\t{0}_{1} = {2},", field.Name, num + 1, field.Offset + num);
		}

		private void WriteToFile(WoWFile wowFile, string outputFile)
		{
			uint objectDelta = m_updateFieldList[m_objectEnd - 1].Offset + m_updateFieldList[m_objectEnd - 1].Size;
			uint unitDelta = m_updateFieldList[m_unitEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;
			uint itemDelta = m_updateFieldList[m_itemEnd - 1].Offset + m_updateFieldList[m_unitEnd - 1].Size;

			using (StreamWriter writer = new StreamWriter(outputFile))
			{
				writer.WriteLine();
				writer.WriteLine("///");
				writer.WriteLine("/// UpdateFields generated for Client-Version: " + wowFile.Version);
				writer.WriteLine("///");
				writer.WriteLine();

				writer.WriteLine("namespace WCell.Constants.Updates");
				writer.WriteLine("{");

				DumpEnum(m_objectStart, m_objectEnd, 0, "ObjectFields", m_objectExtraFields, writer);

				DumpEnum(m_unitStart, m_unitEnd, objectDelta, "UnitFields", m_unitExtraFields, writer);

				DumpEnum(m_playerStart, m_playerEnd, unitDelta, "PlayerFields", m_playerExtraFields, writer);

				DumpEnum(m_itemStart, m_itemEnd, objectDelta, "ItemFields", m_itemExtraFields, writer);

				DumpEnum(m_containerStart, m_containerEnd, itemDelta, "ContainerFields", m_containerExtraFields, writer);

				DumpEnum(m_dynamicObjectStart, m_dynamicObjectEnd, objectDelta, "DynamicObjectFields", m_dynamicObjectExtraFields,
				         writer);

				DumpEnum(m_gameObjectStart, m_gameObjectEnd, objectDelta, "GameObjectFields", m_gameObjectExtraFields, writer);

				DumpEnum(m_corpseStart, m_corpseEnd, objectDelta, "CorpseFields", m_corpseExtraFields, writer);

				writer.WriteLine("}");
			}
		}

		private void DumpEnum(int start, int end, uint enumOffset, string enumName, List<string> extras, TextWriter writer)
		{
			ExtendedUpdateField field;

			writer.WriteLine("\tpublic enum " + enumName);
			writer.WriteLine("\t{");
			for (int i = start; i < end; i++)
			{
				field = m_updateFieldList[i];

				WriteField(field, writer);

				if (field.Size > 1)
				{
					int pos = (int) (field.Offset - enumOffset);
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
		public void CreatePacketParserInfo(string outputFile)
		{
			var items = Extract();

			//throw new NotImplementedException("Deprecated");
			//using (var writer = new CodeFileWriter(new StreamWriter(Path.Combine(Tools.WCellConstantsUpdates, "WCellInfo.cs")),
			//"WCell.Constants", "WCellInfo", "static class"))
			//{
			//writer.WriteSummary("The version of the WoW Client that is currently supported.");
			//writer.WriteLine("public static readonly ClientVersion RequiredVersion = new ClientVersion({0}, {1}, {2}, {3}))",
			//  WowFile.VersionString);
			//writer.WriteLine();

			StreamWriter writer = new StreamWriter("temp.txT");
			for (ObjectTypeId g = ObjectTypeId.Object; g < (ObjectTypeId) UpdateField.ObjectTypeCount; g++)
			{
				foreach (var field in items[(int) g])
				{
					if (field == null) continue;

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
			writer.Close();
			//}
		}
	}
}