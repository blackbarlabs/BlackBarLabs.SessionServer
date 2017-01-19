﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Extensions;
using BlackBarLabs.Persistence;
using BlackBarLabs.Persistence.Azure.Extensions;
using System.Linq;
using BlackBarLabs.Linq;
using BlackBarLabs;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.Persistence.Azure
{
    public struct AuthorizationProvider
    {
        public string userId;
        public CredentialValidationMethodTypes method;
        public Uri provider;
    }

    public class Authorizations
    {
        private AzureStorageRepository repository;
        public Authorizations(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        private Guid GetRowKey(Uri providerId, string username)
        {
            var concatination = providerId.AbsoluteUri + username;
            var md5 = MD5.Create();
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(concatination));

            var rowId = new Guid(data);
            var md5Hash = GetMd5Hash(md5, concatination);
            return md5Hash;
        }
        
        public Task<TResult> UpdateCredentialTokenAsync<TResult>(Guid authorizationId, Uri providerId, string username, Uri[] claimsProviders,
            Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated)
        {
            throw new NotImplementedException();
        }

        public async Task<T> CreateAuthorizationAsync<T>(Guid authorizationId,
                AuthorizationProvider [] authorizationProviders,
            Func<T> onSuccess,
            Func<T> onAlreadyExist)
        {
            var authorizationDocument = new Documents.AuthorizationDocument()
            {

            };
            authorizationDocument.AddProviders(authorizationProviders);

            return await repository.CreateAsync(authorizationId, authorizationDocument,
                () => onSuccess(),
                () => onAlreadyExist());
        }

        static Guid GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            return new Guid(data);
        }

        public async Task<TResult> CreateCredentialAsync<TResult>(Guid loginId, Guid actorId,
            Func<TResult> success,
            Func<TResult> onAlreadyExists)
        {
            var document = new Documents.CredentialMappingDocument
            {
                AuthId = actorId,
            };
            return await repository.CreateAsync(loginId, document,
                () => success(),
                () => onAlreadyExists());
        }

        internal Task<TResult> CreateCredentialRedirectAsync<TResult>(Guid redirectId,
            Guid actorId, string email,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyAssociated)
        {
            var rollback = new RollbackAsync<TResult>();
            var credentialRedirectDoc = new Documents.CredentialRedirectDocument()
            {
                ActorId = actorId,
                Email = email,
            };
            rollback.AddTaskCreate(redirectId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument doc) =>
                {
                    var associatedEmailsStorage = doc.AssociatedEmails;
                    var associatedEmails = String.IsNullOrWhiteSpace(associatedEmailsStorage)?
                        new string[] { }
                        :
                        associatedEmailsStorage.Split(new[] { ',' });
                    if (associatedEmails.Contains(email))
                        return false;
                    doc.AssociatedEmails = associatedEmails.Append(email).Join(",");
                    return true;
                },
                (doc) =>
                {
                    doc.AssociatedEmails = doc.AssociatedEmails.Split(new[] { ',' })
                        .Where(e => e.CompareTo(email) != 0)
                        .Join(",");
                    return true;
                },
                onAlreadyAssociated,
                this.repository);
            return rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> FindCredentialRedirectAsync<TResult>(Guid redirectId,
            Func<bool, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(redirectId,
                (Documents.CredentialRedirectDocument document) => onFound(document.Redeemed),
                () => onNotFound());
        }

        internal async Task<TResult> MarkCredentialRedirectAsync<TResult>(Guid redirectId,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Guid, TResult> onAlreadyRedeemed)
        {
            var lookupResults = await repository.UpdateAsync<Documents.CredentialRedirectDocument, KeyValuePair<Guid, bool>?>(redirectId,
                async (document, update) =>
                {
                    if (document.Redeemed)
                        return document.ActorId.PairWithValue(true);
                    document.Redeemed = true;
                    await update(document);
                    return document.ActorId.PairWithValue(false);
                },
                () => default(KeyValuePair<Guid, bool>?));
            if (!lookupResults.HasValue)
                return onNotFound();
            if (lookupResults.Value.Value)
                return onAlreadyRedeemed(lookupResults.Value.Key);
            return onFound(lookupResults.Value.Key);
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.CredentialMappingDocument document) => onSuccess(document.AuthId),
                () => onNotExist());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TResultAdded"></typeparam>
        /// <param name="authorizationId"></param>
        /// <param name="onSuccess">claims, (claimId,issuer,type,value)</param>
        /// <param name="addedSuccess"></param>
        /// <param name="addedFailure"></param>
        /// <param name="notFound"></param>
        /// <param name="failure"></param>
        /// <returns></returns>
        public async Task<TResult> UpdateClaims<TResult, TResultAdded>(Guid authorizationId,
            Func<Claim[], Func<Guid, Uri, Uri, string, Task<TResultAdded>>, Task<TResult>> onSuccess,
            Func<TResultAdded> addedSuccess,
            Func<TResultAdded> addedFailure,
            Func<TResult> notFound,
            Func<string, TResult> failure)
        {
            return await repository.UpdateAsync<Documents.AuthorizationDocument, TResult>(authorizationId,
                async (authorizationDocument, save) =>
                {
                    var claims = await authorizationDocument.GetClaims(repository);
                    var result = await onSuccess(claims,
                        async (claimId, issuer, type, value) =>
                        {
                            var claimDoc = new Documents.ClaimDocument()
                            {
                                ClaimId = claimId,
                                Issuer = issuer == default(Uri) ? default(string) : issuer.AbsoluteUri,
                                Type = type == default(Uri) ? default(string) : type.AbsoluteUri,
                                Value = value,
                            };

                            return await await authorizationDocument.AddClaimsAsync(claimDoc, repository,
                                async () =>
                                {
                                    await save(authorizationDocument);
                                    return addedSuccess();
                                },
                                () => Task.FromResult(addedFailure()));
                        });
                    return result;
                },
                () => notFound());
        }
    }
}