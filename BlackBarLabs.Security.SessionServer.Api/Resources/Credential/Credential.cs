﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Authorization;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    [DataContract]
    public class Credential : Resource, ICredential
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AuthorizationId { get; set; }

        [DataMember]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        public Uri Provider { get; set; }

        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string Token { get; set; }
        
        #endregion

    }
}
