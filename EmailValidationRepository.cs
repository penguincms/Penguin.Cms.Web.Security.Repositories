﻿using Penguin.Cms.Repositories;
using Penguin.Cms.Repositories.Interfaces;
using Penguin.Cms.Security;
using Penguin.Mail.Abstractions.Attributes;
using Penguin.Mail.Abstractions.Interfaces;
using Penguin.Messaging.Core;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

namespace Penguin.Cms.Web.Security.Repositories
{
    /// <summary>
    /// An IRepository implementation for Email Validation tokens
    /// </summary>
    public class EmailValidationRepository : AuditableEntityRepository<EmailValidationToken>, IEmailHandler
    {
        /// <summary>
        /// Email template repository for sending out validation emails
        /// </summary>
        protected ISendTemplates EmailTemplateRepository { get; set; }

        /// <summary>
        /// User repository for accessing users
        /// </summary>
        protected IEntityRepository<User> UserRepository { get; set; }

        /// <summary>
        /// Constructs a new instance of this repository
        /// </summary>
        /// <param name="context">A Persistence context for Email Tokens</param>
        /// <param name="emailTemplateRepository">An EmailTemplate repository</param>
        /// <param name="userRepository">A User repository</param>
        /// <param name="messageBus">An optional message bus for sending persistence messages</param>
        public EmailValidationRepository(IPersistenceContext<EmailValidationToken> context, ISendTemplates emailTemplateRepository, IEntityRepository<User> userRepository, MessageBus messageBus = null) : base(context, messageBus)
        {
            EmailTemplateRepository = emailTemplateRepository;
            UserRepository = userRepository;
        }

        /// <summary>
        /// Generates a validation email and sents it to the user
        /// </summary>
        /// <param name="user">The user to validate</param>
        /// <param name="linkUrl">Passed in value not sent. Only used for templating</param>
        [EmailHandler("Validate Email")]
        public void GenerateEmail(User user, string linkUrl)
        {
            Contract.Requires(linkUrl != null);

            EmailTemplateRepository.GenerateEmailFromTemplate(new Dictionary<string, object>()
            {
                [nameof(user)] = user,
                [nameof(linkUrl)] = linkUrl
            });
        }

        /// <summary>
        /// Generates and persists a new email token for the given user
        /// </summary>
        /// <param name="u">The user to generate a token for</param>
        /// <param name="LinkUrl">Passed in value not sent. Only used for templating</param>
        public void GenerateToken(User u, string LinkUrl)
        {
            Contract.Requires(u != null);

            GenerateToken(u.Guid, LinkUrl);
        }

        /// <summary>
        /// Generates and persists a new email token for the given user
        /// </summary>
        /// <param name="userGuid">The user to generate a token for</param>
        /// <param name="LinkUrl">Passed in value not sent. Only used for templating</param>
        public void GenerateToken(Guid userGuid, string LinkUrl)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(LinkUrl));

            User thisUser = UserRepository.Find(userGuid);

            List<EmailValidationToken> existingTokens = this.Where(t => t.Creator == userGuid).ToList();

            foreach (EmailValidationToken thisToken in existingTokens)
            {
                thisToken.DateDeleted = DateTime.Now;
            }

            EmailValidationToken newToken = new()
            {
                Creator = userGuid
            };

            GenerateEmail(thisUser, string.Format(CultureInfo.CurrentCulture, LinkUrl, newToken.Guid));

            Add(newToken);
        }

        /// <summary>
        /// Returns true if a token doesn't exist, or is expired
        /// </summary>
        /// <param name="TokenId">The token to check for</param>
        /// <returns>True if there is a valid, unexpired token</returns>
        public bool IsTokenExpired(Guid TokenId)
        {
            return this.Where(t => t.Guid == TokenId).FirstOrDefault()?.IsDeleted ?? true;
        }

        /// <summary>
        /// Returns true if the user has validated their email
        /// </summary>
        /// <param name="u">The user to check for</param>
        /// <returns>True if the user has validated their email</returns>
        public bool IsValidated(User u)
        {
            Contract.Requires(u != null);

            return IsValidated(u.Guid);
        }

        /// <summary>
        /// Returns true if the user has validated their email
        /// </summary>
        /// <param name="userGuid">The user to check for</param>
        /// <returns>True if the user has validated their email</returns>
        public bool IsValidated(Guid userGuid)
        {
            return this.Where(t => t.IsValidated && t.Creator == userGuid).Any();
        }

        /// <summary>
        /// Checks to see if the token if the Token is valid
        /// </summary>
        /// <param name="TokenId">The Guid of the Token to check</param>
        /// <returns>If the token is found, and valid</returns>
        public bool ValidateToken(Guid TokenId)
        {
            EmailValidationToken thisToken = Find(TokenId);

            if (thisToken is null)
            {
                return false;
            }
            else
            {
                thisToken.IsValidated = true;
                return true;
            }
        }
    }
}