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
using System.Net.Http;

namespace EastFive.Security.SessionServer.Persistence.Azure
{
    public struct CredentialMapping
    {
        public Guid id;
        public Guid actorId;
        public Guid loginId;
    }

    public class CredentialMappings
    {
        private AzureStorageRepository repository;
        public CredentialMappings(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<TResult> CreatePasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Guid actorId, Guid loginId,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed)
        {
            var rollback = new RollbackAsync<TResult>();

            var lookupDoc = new Documents.LoginActorLookupDocument
            {
                ActorId = actorId,
            };
            rollback.AddTaskCreate(loginId, lookupDoc,
                onLoginAlreadyUsed, this.repository);

            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument actorDoc) =>
                    actorDoc.AddPasswordCredential(passwordCredentialId),
                actorDoc => actorDoc.RemovePasswordCredential(passwordCredentialId),
                onRelationshipAlreadyExists,
                this.repository);

            var passwordCredentialDoc = new Documents.PasswordCredentialDocument
            {
                LoginId = loginId,
            };
            rollback.AddTaskCreate(passwordCredentialId, passwordCredentialDoc,
                    onAlreadyExists, this.repository);

            return await rollback.ExecuteAsync(onSuccess);
        }

        public async Task<TResult> FindPasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Func<Guid, Guid, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(passwordCredentialId,
                (Documents.PasswordCredentialDocument document) =>
                    LookupCredentialMappingAsync(document.LoginId,
                        (actorId) => onSuccess(actorId, document.LoginId),
                        () => onNotFound()),
                () => onNotFound().ToTask());
        }

        public async Task<TResult> FindPasswordCredentialByActorAsync<TResult>(Guid actorId,
            Func<CredentialMapping[], TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindLinkedDocumentsAsync(actorId,
                (document) => document.GetPasswordCredentials(),
                (Documents.AuthorizationDocument authDoc, Documents.PasswordCredentialDocument[] passwordCredentialDocs) =>
                {
                    var invites = passwordCredentialDocs.Select(pcDoc => new CredentialMapping
                    {
                        id = pcDoc.Id,
                        actorId = actorId,
                        loginId = pcDoc.LoginId,
                    }).ToArray();
                    return onSuccess(invites);
                },
                () => onNotFound());
        }
        
        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.LoginActorLookupDocument document) => onSuccess(document.ActorId),
                () => onNotExist());
        }

        //internal Task<TResult> FindCredentialMappingByAuthIdAsync<TResult>(Guid actorId,
        //    Func<CredentialMapping[], TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return repository.FindByIdAsync(actorId,
        //        (Documents.AuthorizationDocument document) =>
        //        {
        //            return onFound(document.GetCredentialMappings()
        //                .Select(kvp => new CredentialMapping
        //                {
        //                    id = kvp.Key,
        //                    actorId = actorId,
        //                    loginId = kvp.Value,
        //                }).ToArray());
        //        },
        //        () => onNotFound());
        //}

        //internal Task<TResult> FindCredentialMappingByIdAsync<TResult>(Guid credentialMappingId,
        //    Func<CredentialMapping, TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return repository.FindByIdAsync(credentialMappingId,
        //        (Documents.CredentialMappingDocument document) =>
        //        {
        //            return onFound(new CredentialMapping
        //            {
        //                id = document.Id,
        //                actorId = document.ActorId,
        //                loginId = document.LoginId,
        //            });
        //        },
        //        () => onNotFound());
        //}

        //internal Task<TResult> CreateTokenAsync<TResult>(Guid inviteId,
        //    Guid actorId, string email, Guid token,
        //    Func<TResult> onSuccess,
        //    Func<TResult> onAlreadyAssociated)
        //{
        //    var rollback = new RollbackAsync<TResult>();
        //    var credentialRedirectDoc = new Documents.InviteDocument()
        //    {
        //        ActorId = actorId,
        //        Email = email,
        //        //IsToken = true,
        //    };
        //    rollback.AddTaskCreate(inviteId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
        //    rollback.AddTaskCreateOrUpdate(actorId,
        //        (Documents.AuthorizationDocument doc) => doc.AddRedirect(inviteId),
        //        (doc) => doc.RemoveRedirect(inviteId),
        //        onAlreadyAssociated,
        //        this.repository);
        //    return rollback.ExecuteAsync(onSuccess);
        //}
        
        internal async Task<TResult> CreateInviteAsync<TResult>(Guid inviteId,
            Guid actorId, string email, Guid token, DateTime lastSent, bool isToken,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var rollback = new RollbackAsync<TResult>();

            var inviteDocument = new Documents.InviteDocument()
            {
                ActorId = actorId,
                Email = email,
                IsToken = isToken,
                LastSent = lastSent,
                Token = token,
            };
            rollback.AddTaskCreate(inviteId, inviteDocument, onAlreadyExists, this.repository);

            var inviteTokenDocument = new Documents.InviteTokenDocument()
            {
                ActorId = actorId,
                InviteId = inviteId,
            };
            rollback.AddTaskCreate(token, inviteTokenDocument, onAlreadyExists, this.repository);

            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument authDoc) => authDoc.AddInviteId(inviteId),
                (authDoc) => authDoc.RemoveInviteId(inviteId),
                onAlreadyExists, // This should fail on the action above as well
                this.repository);

            return await rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> UpdateTokenCredentialAsync<TResult>(Guid tokenCredentialId,
            Func<string, DateTime?, Guid, Func<string, DateTime?, Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return this.repository.UpdateAsync<Documents.InviteDocument, TResult>(tokenCredentialId,
                async (currentDoc, saveAsync) =>
                {
                    return await onFound(currentDoc.Email, currentDoc.LastSent, currentDoc.Token,
                        async (email, lastSent) =>
                        {
                            currentDoc.Email = email;
                            currentDoc.LastSent = lastSent;
                            await saveAsync(currentDoc);
                        });
                },
                () => onNotFound());
        }

        internal Task<TResult> FindInviteAsync<TResult>(Guid inviteId, bool isToken,
            Func<Guid, string, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(inviteId,
                (Documents.InviteDocument document) =>
                {
                    if (isToken != document.IsToken)
                        return onNotFound();
                    return onFound(document.ActorId, document.Email);
                },
                () => onNotFound());
        }
        
        internal async Task<TResult> FindInviteByTokenAsync<TResult>(Guid token, bool isToken,
            Func<Guid, Guid, Guid?, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(token,
                (Documents.InviteTokenDocument document) =>
                    repository.FindByIdAsync(document.InviteId,
                        (Documents.InviteDocument inviteDoc) =>
                        {
                            return onSuccess(document.InviteId, inviteDoc.ActorId, inviteDoc.LoginId);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency
                            return onNotFound();
                        }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> FindTokenCredentialByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(token,
                (Documents.InviteTokenDocument document) =>
                    repository.FindByIdAsync(document.InviteId,
                        (Documents.InviteDocument inviteDoc) =>
                        {
                            if (!inviteDoc.IsToken)
                                return onNotFound();
                            return onSuccess(document.InviteId, inviteDoc.ActorId);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency
                            return onNotFound();
                        }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> FindInviteByActorAsync<TResult>(Guid actorId, bool isToken,
            Func<Invite[], TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindLinkedDocumentsAsync(actorId,
                (document) => document.GetInviteIds(),
                (Documents.AuthorizationDocument authDoc, Documents.InviteDocument[] inviteDocs) =>
                {
                    var invites = inviteDocs.Select(Convert).ToArray();
                    return onSuccess(invites);
                },
                () => onNotFound());
        }
        
        private static Invite Convert(Documents.InviteDocument inviteDoc)
        {
            return new Invite
            {
                id = inviteDoc.Id,
                actorId = inviteDoc.ActorId,
                email = inviteDoc.Email,
            };
        }

        internal async Task<TResult> MarkInviteRedeemedAsync<TResult>(Guid inviteToken, Guid loginId,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Guid, TResult> onAlreadyRedeemed,
            Func<TResult> onAlreadyInUse,
            Func<TResult> onAlreadyConnected)
        {
            var lookupResults = await await repository.FindByIdAsync(inviteToken,
                async (Documents.InviteTokenDocument tokenDocument) =>
                {
                    var rollback = new RollbackAsync<TResult>();

                    rollback.AddTaskUpdate(tokenDocument.InviteId,
                        (Documents.InviteDocument inviteDoc) => { inviteDoc.LoginId = loginId; },
                        (inviteDoc) => { inviteDoc.LoginId = default(Guid?); },
                        onNotFound,
                        this.repository);

                    var loginLookup = new Documents.LoginActorLookupDocument()
                    {
                        ActorId = tokenDocument.ActorId,
                    };
                    rollback.AddTaskCreate(loginId, loginLookup, onAlreadyInUse, this.repository);

                    return await rollback.ExecuteAsync(() => onFound(tokenDocument.ActorId));
                },
                () => onNotFound().ToTask());
            return lookupResults;
        }
    }
}