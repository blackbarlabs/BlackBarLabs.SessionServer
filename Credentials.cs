﻿using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Security.Claims;
using BlackBarLabs;
using BlackBarLabs.Linq.Async;

namespace EastFive.Security.SessionServer
{
    public struct Invite
    {
        public Guid id;
        public Guid actorId;
        public string email;
        internal bool isToken;
        internal DateTime? lastSent;
    }

    public struct PasswordCredential
    {
        public Guid id;
        public Guid actorId;
        public string userId;
        public bool isEmail;
        internal bool forceChangePassword;
    }

    public class Credentials
    {
        private Context context;
        private Persistence.Azure.DataContext dataContext;

        internal Credentials(Context context, Persistence.Azure.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        #region Password Credential

        public async Task<TResult> CreatePasswordCredentialsAsync<TResult>(Guid passwordCredentialId, Guid actorId,
            string username, bool isEmail, string token, bool forceChange,
            System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<string, TResult> authenticationFailed,
            Func<TResult> credentialAlreadyExists,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed,
            Func<TResult> serviceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var loginProvider = await this.context.LoginProvider;

            var createLoginResult = await await loginProvider.CreateLoginAsync("User",
                username, isEmail, token, forceChange,
                async loginId =>
                {
                    var result = await await dataContext.CredentialMappings.CreatePasswordCredentialAsync(
                        passwordCredentialId, actorId, loginId,
                        () => onSuccess().ToTask(),
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return credentialAlreadyExists();
                        },
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return onRelationshipAlreadyExists();
                        },
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return onLoginAlreadyUsed();
                        });

                    return result;
                },
                (why) => authenticationFailed(why).ToTask());
            return createLoginResult;
        }

        internal async Task<TResult> GetPasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Func<PasswordCredential, TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.CredentialMappings.FindPasswordCredentialAsync(passwordCredentialId,
                async (actorId, loginId) =>
                {
                    var loginProvider = await this.context.LoginProvider;
                    return await loginProvider.GetLoginAsync(loginId,
                        (userId, isEmail, forceChangePassword) =>
                        {
                            return success(new PasswordCredential
                            {
                                id = passwordCredentialId,
                                actorId = actorId,
                                userId = userId,
                                isEmail = isEmail,
                                forceChangePassword = forceChangePassword,
                            });
                        },
                        () => notFound(), // TODO: Log this
                        (why) => onServiceNotAvailable(why));
                },
                () => notFound().ToTask());
        }

        internal async Task<TResult> GetPasswordCredentialByActorAsync<TResult>(Guid actorId,
            Func<PasswordCredential[], TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.CredentialMappings.FindPasswordCredentialByActorAsync(actorId,
                async (credentials) =>
                {
                    var loginProvider = await this.context.LoginProvider;
                    var pwCreds = await credentials.Select(
                        async credential =>
                        {
                            return await loginProvider.GetLoginAsync(credential.loginId,
                                (userId, isEmail, forceChangePassword) =>
                                {
                                    return new PasswordCredential
                                    {
                                        id = credential.id,
                                        actorId = actorId,
                                        userId = userId,
                                        isEmail = isEmail,
                                        forceChangePassword = forceChangePassword,
                                    };
                                },
                                () => default(PasswordCredential?), // TODO: Log this
                                (why) => default(PasswordCredential?));
                        })
                        .WhenAllAsync()
                        .SelectWhereHasValueAsync()
                        .ToArrayAsync();
                    return success(pwCreds);
                },
                () => notFound().ToTask());
        }

        #endregion

        #region InviteCredential

        public async Task<TResult> SendEmailInviteAsync<TResult>(Guid inviteId, Guid actorId, string email,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<TResult> inviteAlreadyExists,
            Func<TResult> onCredentialMappingDoesNotExists,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailed)
        {
            var token = BlackBarLabs.Security.SecureGuid.Generate();
            var result = await await this.dataContext.CredentialMappings.CreateInviteAsync(inviteId,
                actorId, email, token, DateTime.UtcNow, false,
                async () =>
                {
                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        "newaccount",
                        new Dictionary<string, string>()
                        {
                            { "subject", "New Order Owl Account" },
                            { "create_account_link", getRedirectLink(inviteId, token).AbsoluteUri }
                        },
                        null,
                        (sentCode) => success(),
                        () => onServiceNotAvailable(),
                        (why) => onFailed(why));
                    return resultMail;
                },
                () => inviteAlreadyExists().ToTask());
            return result;
        }

        internal Task<TResult> GetInviteAsync<TResult>(Guid inviteId,
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId, false,
                (actorId, email) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
                        email = email,
                    });
                },
                () => notFound());
        }

        internal Task<TResult> GetInviteByTokenAsync<TResult>(Guid token,
            Func<byte[], TResult> redirect,
            Func<Guid, TResult> success,
            Func<TResult> onAlreadyUsed,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByTokenAsync(token, false,
                (inviteId, actorId, loginId) =>
                {
                    if (loginId.HasValue)
                        return success(actorId);

                    var state = token.ToByteArray();
                    return redirect(state);
                },
                () => notFound());
        }

        internal Task<TResult> GetInvitesByActorAsync<TResult>(Guid actorId,
            Func<Invite[], TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId, false,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }

        #endregion

        #region Tokens

        public async Task<TResult> CreateTokenCredentialAsync<TResult>(Guid inviteId, Guid actorId, string email,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<TResult> inviteAlreadyExists,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailed)
        {
            var token = BlackBarLabs.Security.SecureGuid.Generate();
            var result = await await this.dataContext.CredentialMappings.CreateInviteAsync(inviteId,
                actorId, email, token, DateTime.UtcNow, true,
                async () =>
                {
                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        "tokenlink",
                        new Dictionary<string, string>()
                        {
                            { "subject", "New Order Owl Account" },
                            { "token_login_link", getRedirectLink(inviteId, token).AbsoluteUri }
                        },
                        null,
                        (sentCode) => success(),
                        () => onServiceNotAvailable(),
                        (why) => onFailed(why));
                    return resultMail;
                },
                () => inviteAlreadyExists().ToTask());
            return result;
        }

        internal async Task<TResult> UpdateTokenCredentialAsync<TResult>(Guid tokenCredentialId,
                string email, DateTime? lastSent,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> onSuccess,
            Func<TResult> onNoChange,
            Func<TResult> onNotFound,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var result = await this.dataContext.CredentialMappings.UpdateTokenCredentialAsync(tokenCredentialId,
                async (emailCurrent, lastSentCurrent, token, saveAsync) =>
                {
                    if (
                        (lastSent.HasValue && lastSentCurrent.HasValue && lastSent.Value > lastSentCurrent.Value) ||
                        (lastSent.HasValue && (!lastSentCurrent.HasValue)) ||
                        String.Compare(emailCurrent, email) != 0)
                    {
                        if (String.IsNullOrWhiteSpace(email))
                            email = emailCurrent;
                        var mailService = this.context.MailService;
                        var resultMail = await await mailService.SendEmailMessageAsync(email, string.Empty,
                            "newaccounts@orderowl.com", "New Account Services",
                            "tokenlink",
                            new Dictionary<string, string>()
                            {
                                { "subject", "New Order Owl Account" },
                                { "token_login_link", getRedirectLink(tokenCredentialId, token).AbsoluteUri }
                            },
                            null,
                            async (sentCode) =>
                            {
                                if (!lastSent.HasValue)
                                    lastSent = DateTime.UtcNow;
                                await saveAsync(email, lastSent);
                                return onSuccess();
                            },
                            () => onServiceNotAvailable().ToTask(),
                            (why) => onFailure(why).ToTask());
                        return resultMail;
                    }
                    return onNoChange();
                },
                () => onNotFound());
            return result;
        }

        internal Task<TResult> GetTokenCredentialAsync<TResult>(Guid inviteId,
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId, true,
                (actorId, email) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
                        email = email,
                        isToken = true,
                    });
                },
                () => notFound());
        }

        internal async Task<TResult> GetTokenCredentialByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, string, string, TResult> success,
            Func<TResult> notFound)
        {
            return await await this.dataContext.CredentialMappings.FindTokenCredentialByTokenAsync(token,
                async (inviteId, actorId) =>
                {
                    var sessionId = Guid.NewGuid();
                    var result = await this.context.Sessions.CreateAsync(sessionId, actorId,
                        new System.Security.Claims.Claim[] { },
                        (jwtToken, refreshToken) =>
                        {
                            return success(sessionId, actorId, jwtToken, refreshToken);
                        },
                        () => default(TResult)); // Should only happen if generated Guid is not unique ;-O
                    return result;
                },
                () => notFound().ToTask());
        }

        internal Task<TResult> GetTokenCredentialByActorAsync<TResult>(Guid actorId,
            Func<Invite[], TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId, true,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }

        #endregion

    }
}
