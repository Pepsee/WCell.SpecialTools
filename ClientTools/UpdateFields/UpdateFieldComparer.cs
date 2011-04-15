using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using WCell.Tools;
using WCell.Tools.Ralek.UpdateFields;
using WCell.Util;
using WCell.Constants;
using WCell.Constants.Updates;

namespace ClientTools.UpdateFields
{
	public class FieldVariation
	{
		public readonly ExtendedUpdateField Original;

		public FieldVariation(ExtendedUpdateField original)
		{
			Original = original;
		}
	}

	public class ChangedField : FieldVariation
	{
		public readonly ExtendedUpdateField Changed;

		public ChangedField(ExtendedUpdateField original, ExtendedUpdateField mt)
			: base(original)
		{
			Changed = mt;
		}

		public override string ToString()
		{
			List<string> str = new List<string>();
			if (Original.Size != Changed.Size)
			{
				str.Add("Size: " + Original.Size + " -> " + Changed.Size);
			}
			if (Original.Type != Changed.Type)
			{
				str.Add("Type: " + Original.Type + " -> " + Changed.Type);
			}
			if (Original.Flags != Changed.Flags)
			{
				str.Add("Flags: " + Original.Flags + " -> " + Changed.Flags);
			}

			string movedStr;
			if (Original.Offset != Changed.Offset)
			{
				var diff = Changed.Offset - Original.Offset;
				movedStr = string.Format("moved from {0} to {1} ({2}{3})", Original.Offset, Changed.Offset,
				                         diff > 0 ? "+" : "", diff);
				if (str.Count > 0)
				{
					movedStr += " and";
				}
			}
			else
			{
				movedStr = "(at: " + Original.Offset + ")";
			}

			if (str.Count > 0)
			{
				movedStr += " changed -";
			}

			return string.Format((Original.Offset != Changed.Offset ? "MOVED" : "CHANGED") + ": {0} {1} {2}", Original.Name,
			                     movedStr, str.ToString(", "));
		}
	}

	public class DeprecatedField : FieldVariation
	{
		public DeprecatedField(ExtendedUpdateField field)
			: base(field)
		{
		}

		public override string ToString()
		{
			return "DEPRECATED: " + Original;
		}
	}

	public class NewField : FieldVariation
	{
		public NewField(ExtendedUpdateField field)
			: base(field)
		{
		}

		public override string ToString()
		{
			return "NEW: " + Original;
		}
	}

	public class UpdateFieldComparer
	{
		private ExtendedUpdateField[][] origFields;
		private ExtendedUpdateField[][] newVersionFields;

		public readonly List<FieldVariation>[] Changes = new List<FieldVariation>[UpdateField.ObjectTypeCount];

		public static List<FieldVariation>[] Comparer(WoWFile oldFile, WoWFile newFile)
		{
			return new UpdateFieldComparer(oldFile, newFile).Changes;
		}

		public static void Dump(WoWFile oldFile, WoWFile newFile, string dir)
		{
			using (var writer = new StreamWriter(ToolConfig.OutputDir + "UpdateField Changes from " +
			                                     oldFile.Version + " to " + newFile.Version + ".txt"))
			{
				Dump(oldFile, newFile, writer);
			}
		}

		public static void Dump(WoWFile oldFile, WoWFile newFile, TextWriter output)
		{
			var comp = new UpdateFieldComparer(oldFile, newFile);
			comp.Dump(output);
		}

		public UpdateFieldComparer(WoWFile oldFile, WoWFile newFile)
		{
			this.origFields = UpdateFieldExtractor.Extract(oldFile);
			this.newVersionFields = UpdateFieldExtractor.Extract(newFile);

			Compare();
		}

		private void Compare()
		{
			Dictionary<string, ExtendedUpdateField> newFields = new Dictionary<string, ExtendedUpdateField>(100);
			for (var group = (ObjectTypeId) 0; group < (ObjectTypeId) UpdateField.ObjectTypeCount; group++)
			{
				var oldGroup = origFields[(int) group];
				var newGroup = newVersionFields[(int) group];
				var groupChange = Changes[(int) group] = new List<FieldVariation>(20);

				var count = Math.Max(oldGroup.Length, newGroup.Length);

				uint indexDiff = 0;
				for (uint i = 0; i < count; i++)
				{
					var origField = oldGroup.Get(i);
					var newField = newGroup.Get(i + indexDiff);

					if (origField == null && newField == null)
					{
						continue;
					}
					else if (origField == null || newField == null || origField.Name != newField.Name)
					{
						// Field moved or got removed

						ExtendedUpdateField movedTo = null;
						if (origField != null)
						{
							if (!newFields.TryGetValue(origField.Name, out movedTo))
							{
								movedTo = Find(newGroup, origField.Name, i + 1);
							}
							else
							{
								// Field moved to a previous index
								newFields.Remove(origField.Name);
							}
						}

						if (movedTo == null)
						{
							indexDiff = 0;
							if (origField != null)
							{
								// Original Field got deprecated
								groupChange.Add(new DeprecatedField(origField));
							}
						}
						else
						{
							// Field got moved
							indexDiff = movedTo.Offset - origField.Offset;
							groupChange.Add(new ChangedField(origField, movedTo));
						}

						if (newField != null)
						{
							// Field of the new version might be a new addition
							newFields[newField.Name] = newField;
						}
					}
					else if (!origField.Equals(newField))
					{
						groupChange.Add(new ChangedField(origField, newField));
					}
				}

				foreach (var newField in newFields.Values)
				{
					bool found = false;
					for (int i = 0; i < groupChange.Count; i++)
					{
						var change = groupChange[i];
						if (change is ChangedField &&
						    ((ChangedField) change).Changed != null &&
						    ((ChangedField) change).Changed.Offset >= newField.Offset)
						{
							groupChange.Insert(i, new NewField(newField));
							found = true;
							break;
						}
					}

					if (!found)
					{
						groupChange.Add(new NewField(newField));
					}
				}
				newFields.Clear();
			}
		}

		public void Dump(string outputFile)
		{
			using (var output = new StreamWriter(outputFile, false))
			{
				Dump(output);
			}
		}

		public void Dump(TextWriter writer)
		{
			for (ObjectTypeId group = ObjectTypeId.Object; group < (ObjectTypeId) UpdateField.ObjectTypeCount; group++)
			{
				if (Changes[(int) group].Count > 0)
				{
					writer.WriteLine(group.ToString().ToUpper() + ":");
					writer.WriteLine();
					foreach (var change in Changes[(int) group])
					{
						writer.WriteLine("\t" + change);
					}
					writer.WriteLine();
					writer.WriteLine("##########################################################");
					writer.WriteLine();
				}
			}
		}

		public static ExtendedUpdateField Find(ExtendedUpdateField[] arr, string name, uint start)
		{
			for (uint i = start; i < arr.Length; i++)
			{
				var field = arr[i];
				if (field != null && field.Name.Equals(name))
				{
					return field;
				}
			}
			return null;
		}
	}
}