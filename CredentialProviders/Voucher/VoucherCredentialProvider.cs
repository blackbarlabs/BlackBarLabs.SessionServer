﻿using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EastFive.Security.SessionServer.Persistence;
using EastFive.Api.Services;
using EastFive.Security.SessionServer;

namespace EastFive.Security.CredentialProvider.Voucher
{
    public class VoucherCredentialProvider : IProvideAuthorization
    {
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideAuthorization(new VoucherCredentialProvider()).ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.SAML;

        public Type CallbackController => typeof(SessionServer.Api.Controllers.TokenController);

        public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            //var trustedProvider = Utilities.GetTrustedProviderId();
            //var trimChars = new char[] { '/' };
            //if (String.Compare(providerId.AbsoluteUri.TrimEnd(trimChars), trustedProvider.AbsoluteUri.TrimEnd(trimChars)) != 0)
            //    return invalidCredentials("ProviderId given does not match trustred ProviderId");

            var token = extraParams["token"]; // TODO: Figure out real value (token is placeholder)
            return Utilities.ValidateToken(token,
                (stateId) =>
                {
                    return onSuccess(stateId.ToString("N"), stateId, default(Guid?), null);
                },
                (errorMessage) => onInvalidCredentials(errorMessage),
                (errorMessage) => onInvalidCredentials(errorMessage),
                (errorMessage) => onInvalidCredentials(errorMessage),
                onUnspecifiedConfiguration).ToTask();
        }
    }
}
