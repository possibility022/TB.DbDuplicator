using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DatabaseCopier.TUI
{
    class CacheFile
    {
        private static CacheFile s_instance;
        public static CacheFile Instance { get => s_instance ?? (s_instance = new CacheFile()); set => s_instance = value; }

        [JsonInclude]
        public HashSet<string> DatabaseSource = new HashSet<string>();

        [JsonInclude]
        public HashSet<string> DatabaseDestination = new HashSet<string>();

        [JsonInclude]
        public HashSet<string> LastIgnoredTables = new HashSet<string>();
    }
}
