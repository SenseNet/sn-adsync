using System.Collections.Generic;
using System.DirectoryServices;

namespace SyncAD2Portal
{
    public class Mapping
    {
        public string Separator { get; set; }
        public List<SyncProperty> AdProperties { get; set; }
        public List<SyncProperty> PortalProperties { get; set; }

        public string ConcatAdPropValues(DirectoryEntry entry)
        {
            var portalValue = string.Empty;
            var first = true;
            foreach (var adProp in this.AdProperties)
            {
                var adValue = Common.GetEntryValue(entry, adProp);

                portalValue = string.Concat(
                    portalValue,
                    first ? string.Empty : this.Separator,
                    adValue);
                if (first)
                    first = false;
            }
            return portalValue;
        }
    }

    public class MappingDefinition
    {
        public string Name { get; set; }
        public List<Mapping> Mappings { get; set; }
    }
}
