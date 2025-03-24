using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace Database.Main;

public static class NpgsqlCommandExtensions
{
	public delegate void DataReadCallback(ref NpgsqlDataReader dr);

	public static void ExecuteReaderEx(this NpgsqlCommand cmd, DataReadCallback dataReadCallback, [CallerMemberName] string? callerName = default)
	{
		DateTime executeStartTime = DateTime.Now;
		DateTime resultStartTime;

		NpgsqlDataReader? dr = null;
		try
		{
			try
			{
				dr = cmd.ExecuteReader();
			}
			catch (Exception e)
			{
				throw new Exception($"DB: {callerName}(): Error executing '{cmd.CommandText}' statement:\n{e.Message}");
			}

			TimeSpan executeTime = DateTime.Now - executeStartTime;
			Log.Debug($"DB: {callerName}(): Query executed in {Math.Round(executeTime.TotalMilliseconds, 3)} ms");

			resultStartTime = DateTime.Now;

			while (dr.Read())
			{
				dataReadCallback(ref dr);
			}
		}
		finally
		{
			if (dr != null)
			{
				dr.Close();
				dr.Dispose();
			}
		}

		TimeSpan resultTime = DateTime.Now - resultStartTime;
		Log.Debug($"DB: {callerName}(): Result generated in {Math.Round(resultTime.TotalMilliseconds, 3)} ms");
	}

	public static void ExecuteNonQueryEx(this NpgsqlCommand cmd, [CallerMemberName] string? callerName = default)
	{
		DateTime executeStartTime = DateTime.Now;

		try
		{
			cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			throw new Exception($"DB: {callerName}(): Error executing '{cmd.CommandText}' statement:\n{e.Message}");
		}

		TimeSpan executeTime = DateTime.Now - executeStartTime;
		Log.Debug($"DB: {callerName}(): NonQuery executed in {Math.Round(executeTime.TotalMilliseconds, 3)} ms");
	}

	public static void AppendValues<T>(this NpgsqlCommand cmd, NpgsqlDbType type, string valuesSet, string valueFieldName, ICollection<T> values)
	{
		for (var i = 0; i < values.Count; i++)
		{
			if (i != 0)
			{
				cmd.CommandText += ",";
			}
			cmd.CommandText += string.Format(valuesSet, i) + " ";

			cmd.Parameters.Add(string.Format(valueFieldName, i), type).Value = values.ElementAt(i);
		}
	}
}
