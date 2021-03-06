﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Persistence.Azure.Documents
{
    internal class CredentialMappingLookupDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Properties
        
        public Guid CredentialMappingId { get; set; }

        public string Method { get; set; }

        public string Subject { get; set; }

        #endregion

    }
}
