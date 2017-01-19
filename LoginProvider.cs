﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using BlackBarLabs.Web;
using BlackBarLabs.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.IdentityModel.Tokens;
using EastFive.Security.SessionServer.Api.Controllers;
using System.Web.Http.Routing;
using System.Web;
using BlackBarLabs.Api;
using System.Web.Http;
using BlackBarLabs;
using BlackBarLabs.Linq;
using EastFive.Api.Services;

namespace EastFive.Security.LoginProvider.AzureADB2C
{
    public class LoginProvider : IIdentityService
    {
        EastFive.AzureADB2C.B2CGraphClient client = new EastFive.AzureADB2C.B2CGraphClient();
        private TokenValidationParameters validationParameters;
        internal string audience;
        private Uri signinConfiguration;
        private Uri signupConfiguration;
        private string loginEndpoint;
        private string signupEndpoint;

        public LoginProvider()
        {
            this.audience = Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "EastFive.Security.LoginProvider.AzureADB2C.Audience");
            this.signinConfiguration = new Uri(Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SigninEndpoint"));
            this.signupConfiguration = new Uri(Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SignupEndpoint"));
        }
        
        public async Task InitializeAsync()
        {
            await EastFive.AzureADB2C.Libary.InitializeAsync(this.signupConfiguration, this.signinConfiguration, this.audience,
                (signupEndpoint, signinEndpoint, validationParams) =>
                {
                    this.signupEndpoint = signupEndpoint;
                    this.loginEndpoint = signinEndpoint;
                    this.validationParameters = validationParams;
                    return true;
                },
                (why) =>
                {
                    return false;
                });
        }

        public Uri GetLoginUrl(string redirect_uri, byte mode, byte[] state, Uri callbackLocation)
        {
            return GetUrl(this.signupEndpoint, redirect_uri, mode, state, callbackLocation);
        }

        public Uri GetSignupUrl(string redirect_uri, byte mode, byte[] state, Uri callbackLocation)
        {
            return GetUrl(this.signupEndpoint, redirect_uri, mode, state, callbackLocation);
        }

        private Uri GetUrl(string longurl, string redirect_uri, byte mode, byte[] state,
            Uri callbackLocation)
        {
            var uriBuilder = new UriBuilder(longurl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["client_id"] = this.audience;
            query["response_type"] = "id_token";
            query["redirect_uri"] = callbackLocation.AbsoluteUri;
            query["response_mode"] = "form_post";
            query["scope"] = "openid";

            var redirBytes = System.Text.Encoding.ASCII.GetBytes(redirect_uri);
            var stateBytes = (new byte[][]
            {
                BitConverter.GetBytes(((short)redirBytes.Length)),
                redirBytes,
                new byte [] {mode},
                state,
            }).SelectMany().ToArray();
            var base64 = Convert.ToBase64String(stateBytes);
            query["state"] = base64; //  redirect_uri.Base64(System.Text.Encoding.ASCII);

            query["nonce"] = Guid.NewGuid().ToString("N");
            // query["p"] = "B2C_1_signin1";
            uriBuilder.Query = query.ToString();
            var redirect = uriBuilder.Uri; // .ToString();
            return redirect;
        }

        public TResult ParseState<TResult>(string state,
            Func<Uri, byte, byte[], TResult> onSuccess,
            Func<string, TResult> invalidState)
        {
            var bytes = Convert.FromBase64String(state);
            var urlLength = BitConverter.ToInt16(bytes, 0);
            if (bytes.Length < urlLength + 3)
                return invalidState("Encoded redirect length is invalid");
            var addr = System.Text.Encoding.ASCII.GetString(bytes, 2, urlLength);
            Uri url;
            if (!Uri.TryCreate(addr, UriKind.RelativeOrAbsolute, out url))
                return invalidState($"Invalid value for redirect url:[{addr}]");
            var mode = bytes.Skip(urlLength + 2).First();
            var data = bytes.Skip(urlLength + 3).ToArray();
            return onSuccess(url, mode, data);
        }

        public async Task<TResult> ValidateToken<TResult>(string idToken, 
            Func<ClaimsPrincipal, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            if (default(TokenValidationParameters) == validationParameters)
                await InitializeAsync();
            var handler = new JwtSecurityTokenHandler();
            Microsoft.IdentityModel.Tokens.SecurityToken validatedToken;
            try
            {
                var claims = handler.ValidateToken(idToken, validationParameters, out validatedToken);
                return onSuccess(claims);
            } catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
            {
                return onFailed(ex.Message);
            }
        }

        public async Task<TResult> CreateLoginAsync<TResult>(string displayName,
            string userId, bool isEmail, string secret, bool forceChange,
            Func<Guid, TResult> onSuccess,
            Func<string, TResult> onFail)
        {
            var user = new EastFive.AzureADB2C.Resources.User()
            {
                DisplayName = displayName,
                AccountEnabled = true,
                SignInNames = new[] {
                    new EastFive.AzureADB2C.Resources.User.SignInName
                    {
                        Type = isEmail? "emailAddress" : "userName",
                        Value = userId,
                    }
                },
                PasswordProfile = new EastFive.AzureADB2C.Resources.User.PasswordProfileResource
                {
                    ForceChangePasswordNextLogin = forceChange,
                    Password = secret,
                },
            };
            return await client.CreateUser(user,
                onSuccess,
                onFail);
        }
    }
}