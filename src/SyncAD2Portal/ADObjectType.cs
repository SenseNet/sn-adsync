using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncAD2Portal
{
    public enum ADObjectType
    {
        None,
        OrgUnit,
        Group,
        User,
        Container,
        Domain,
        Organization,
        AllContainers
    }
}
