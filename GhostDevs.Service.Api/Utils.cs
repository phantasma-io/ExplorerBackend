using System.Text;
using System.Text.Json;

namespace GhostDevs.Service
{
    public static class Utils
    {
        public static string ToJsonString(JsonDocument jdoc)
        {
            if (jdoc == null)
                return null;

            using (var stream = new System.IO.MemoryStream())
            {
                Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
                jdoc.WriteTo(writer);
                writer.Flush();
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
