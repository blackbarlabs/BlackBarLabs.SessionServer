﻿using System;
using System.Net.Http;
using System.Threading.Tasks;

using EastFive.Security.SessionServer.Persistence;

using EastFive.Api.Services;

namespace EastFive.Security.SessionServer
{
    public static class RequestExtensions
    {
        public static SessionServer.Context GetSessionServerContext(this HttpRequestMessage request)
        {
            object identityServiceCreateObject;
            request.Properties.TryGetValue(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService, out identityServiceCreateObject);
            var identityServiceCreate = (Func<Task<IIdentityService>>)identityServiceCreateObject;

            object mailServiceObject;
            request.Properties.TryGetValue(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService, out mailServiceObject);
            var mailService = (Func<ISendMessageService>)mailServiceObject;

            var context = new SessionServer.Context(() => new DataContext(Configuration.AppSettings.Storage),
                identityServiceCreate, mailService);
            return context;
        }
    }
}