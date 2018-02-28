using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SyncAD2Portal
{
    public class LogonCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Anonymous { get; set; }
    }

    public class Server
    {
        // ============================================================================== Serialized proprties

        public string Name { get; set; }
        public string LdapServer { get; set; }
        public bool Novell { get; set; }
        public LogonCredentials LogonCredentials { get; set; }

        public bool UseSsl { get; set; }
        public bool UseSasl { get; set; }
        public int Port { get; set; }
        public bool TrustWrongCertification { get; set; } //TODO: authentication: trust invalid certification
        public bool SyncEnabledState { get; set; }
        public bool SyncUserName { get; set; }

        private string _userType;
        public string UserType
        {
            get { return string.IsNullOrEmpty(_userType) ? "User" : _userType; }
            set { _userType = value; }
        }
        public string DeletedPortalObjectsPath { get; set; }

        // ========================================================================== Computed properties

        [JsonIgnore]
        internal string GuidProperty
        {
            get
            {
                if (this.Novell)
                    return "GUID";

                return "objectguid";
            }
        }

        // ========================================================================== Helper methods

        private bool _deletedPathChecked;
        public async Task<string> GetDeletedPortalObjectsPath()
        {
            if (!_deletedPathChecked && !string.IsNullOrEmpty(DeletedPortalObjectsPath))
            {
                await Common.EnsurePath(DeletedPortalObjectsPath, "SystemFolder");
                _deletedPathChecked = true;
            }

            return DeletedPortalObjectsPath;
        }

        public bool VerifyConnection()
        {
            // this is clearly invalid
            if (string.IsNullOrEmpty(this.LdapServer))
                return false;

            // we do not have to verify certification
            if (!this.UseSsl || this.TrustWrongCertification ||  this.LogonCredentials == null || this.LogonCredentials.Anonymous)
                return true;

            try
            {
                // use default port if a port is not provided
                var port = this.Port == 0 ? 389 : this.Port;

                using (var conn = new LdapConnection(new LdapDirectoryIdentifier(this.LdapServer, port)))
                {
                    conn.SessionOptions.SecureSocketLayer = true;
                    conn.SessionOptions.VerifyServerCertificate = (connection, certificate) => { return true; };
                    conn.Credential = new NetworkCredential(this.LogonCredentials.Username, this.LogonCredentials.Password);
                    conn.AuthType = AuthType.Basic;
                    conn.Bind();

                    return true;
                }
            }
            catch (Exception ex)
            {
                AdLog.LogError("Could not connect to server " + this.LdapServer + " " + ex);
            }

            return false;
        }
    }
}
