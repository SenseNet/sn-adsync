using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncAD2Portal
{
    public class ADSearchResult
    {
        public ADSearchResult(Server server)
        {
            Server = server;
        }

        public Server Server { get; private set; }

        public Guid? SyncGuid { get; set; }
        public string Path { get; set; }
        public DateTime WhenChanged { get; set; }

        public DirectoryEntry GetDirectoryEntry()
        {
            return Common.ConnectToAD(Path, Server);
        }
    }
}
