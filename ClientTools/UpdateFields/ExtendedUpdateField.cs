using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WCell.Constants.Updates;

namespace WCell.Tools.Ralek.UpdateFields
{
	public class ExtendedUpdateField : UpdateField
	{
		public uint NameOffset;
		public string Description;

		public override bool Equals(object obj)
		{
			if (obj is UpdateField)
			{
				var field = ((UpdateField) obj);
				return FullName.Equals(field.FullName) &&
				       Size.Equals(field.Size) &&
				       Group == field.Group &&
				       Type == field.Type &&
				       Flags == field.Flags;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (int) Group | ((int) Offset << 3); // guaranteed to be unique
		}
	}
}