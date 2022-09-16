using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Phantasma.Business.VM;
using Phantasma.Core.Numerics;

namespace Backend.Service.Api;

public static class Utils
{
    public static string ToJsonString(JsonDocument jdoc)
    {
        if ( jdoc == null ) return null;

        using var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream, new JsonWriterOptions {Indented = false});
        jdoc.WriteTo(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }


    public static List<string> GetInstructionsFromScript(string scriptRaw)
    {
        var disassembler = new Disassembler(scriptRaw.Decode());
        var instructions = disassembler.Instructions.ToList();
        return instructions.Select(instruction => instruction.ToString()).ToList();
    }
}
