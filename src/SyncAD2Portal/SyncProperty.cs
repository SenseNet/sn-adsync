namespace SyncAD2Portal
{
    public class SyncProperty
    {
        public int MaxLength { get; set; }

        /// <summary>
        /// Indicates that the property must be unique in the domain
        /// eg.: email must be unique on the portal.
        /// When deleted, the value of this property has to be renamed to a unique name.
        /// </summary>
        public bool Unique { get; set; }

        public string Name { get; set; }
    }
}
