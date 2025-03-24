using System;
using System.Runtime.CompilerServices;
using Npgsql;

namespace Database.Main;

public static class NpgsqlHelpers
{
	public static int InsertsPerTxLimit = 500;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? GetField<T>(ref readonly NpgsqlDataReader dr, int index)
	{
		var obj = dr[index];

		if (obj is DBNull)
		{
			return default;
		}

		return (T)obj;
	}
}
