using System.Collections.Generic;

namespace DatabaseCopier
{
    class CacheFile
    {
        private static CacheFile s_instance;
        public static CacheFile Instance { get => s_instance ?? (s_instance = new CacheFile()); set => s_instance = value; }

        public HashSet<string> DatabaseSource = new HashSet<string>();
        public HashSet<string> DatabaseDestination = new HashSet<string>();
        public HashSet<string> LastIgnoredTables = new HashSet<string>();
        
    }
}
