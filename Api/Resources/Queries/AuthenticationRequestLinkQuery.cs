﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class AuthenticationRequestLinkQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty(PropertyName = "supports_integration")]
        public BoolQuery SupportsIntegration { get; set; }
    }
}
