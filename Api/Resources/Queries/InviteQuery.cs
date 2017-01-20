﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class InviteQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties

        public WebIdQuery Secret { get; set; }

        #endregion
    }
}
