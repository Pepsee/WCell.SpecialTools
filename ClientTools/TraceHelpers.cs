/*************************************************************************
 *
 *   file		: TraceHelpers.cs
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
using System.Linq;
using System.Text;
using System.IO;

namespace WCell.Tools.Ralek
{
	public static class TraceHelpers
	{
		internal static void TraceError(string msg, params object[] args)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(msg, args);
			Console.ResetColor();
		}

		internal static void TraceInfo(string msg, params object[] args)
		{
			//return;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("[{0}] {1}", Date(), string.Format(msg, args));
			Console.ResetColor();
		}

		private static string Date()
		{
			DateTime now = DateTime.Now;
			return String.Format("{0}:{1}:{2}.{3}", now.Hour, now.Minute, now.Second, now.Millisecond);
		}

		internal static void TraceWarning(string msg, params object[] args)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(msg, args);
			Console.ResetColor();
		}
	}
}
