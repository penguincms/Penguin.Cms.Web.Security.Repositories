using Penguin.Cms.Mail.Templating.Repositories;
using Penguin.Cms.Repositories;
using Penguin.Cms.Security;
using System.Linq;
using Penguin.Mail.Abstractions.Attributes;
using Penguin.Mail.Abstractions.Interfaces;
using Penguin.Messaging.Core;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using Penguin.Cms.Security.Repositories;

namespace Penguin.Cms.Web.Security.Repositories
{
    /// <summary>
    /// An IRepository implementation for Email Validation tokens
    /// </summary>
    public class EmailValidationRepository : UserAuditableEntityRepository<EmailValidationToken>, IEmailHandler
    {
        #region Constructors

        /// <summary>
        /// Constructs a new instance of this repository
        /// </summary>
        /// <param name="context">A Persistence context for Email Tokens</param>
        /// <param name="emailTemplateRepository">An EmailTemplate repository</param>
        /// <param name="userRepository">A User repository</param>
        /// <param name="messageBus">An optional message bus for sending persistence messages</param>
        public EmailValidationRepository(IPersistenceContext<EmailValidationToken> context, EmailTemplateRepository emailTemplateRepository, UserRepository userRepository, MessageBus messageBus = null) : base(context, messageBus)
        {
            this.EmailTemplateRepository = emailTemplateRepository;
            this.UserRepository = this.UserRepository;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Generates a validation email and sents it to the user
        /// </summary>
        /// <param name="user">The user to validate</param>
        /// <param name="linkUrl">Passed in value not sent. Only used for templating</param>
        [EmailHandler("Validate Email")]
        public void GenerateEmail(User user, string linkUrl) => this.EmailTemplateRepository.GenerateEmailFromTemplate(new Dictionary<string, object>()
        {
            [nameof(user)] = user,
            [nameof(linkUrl)] = linkUrl
        });

        /// <summary>
        /// Generates and persists a new email token for the given user
        /// </summary>
        /// <param name="u">The user to generate a token for</param>
        /// <param name="LinkUrl">Passed in value not sent. Only used for templating</param>
        public void GenerateToken(User u, string LinkUrl) => this.GenerateToken(u.Guid, LinkUrl);

        /// <summary>
        /// Generates and persists a new email token for the given user
        /// </summary>
        /// <param name="userGuid">The user to generate a token for</param>
        /// <param name="LinkUrl">Passed in value not sent. Only used for templating</param>
        public void GenerateToken(Guid userGuid, string LinkUrl)
        {
            User thisUser = this.UserRepository.Get(userGuid);

            List<EmailValidationToken> existingTokens = this.Where(t => t.Creator == userGuid).ToList();

            foreach (EmailValidationToken thisToken in existingTokens)
            {
                thisToken.DateDeleted = DateTime.Now;
            }

            EmailValidationToken newToken = new EmailValidationToken()
            {
                Creator = userGuid
            };

            this.GenerateEmail(thisUser, string.Format(LinkUrl, newToken.Guid));

            this.Add(newToken);
        }

        /// <summary>
        /// Returns true if a token doesn't exist, or is expired
        /// </summary>
        /// <param name="TokenId">The token to check for</param>
        /// <returns>True if there is a valid, unexpired token</returns>
        public bool IsTokenExpired(Guid TokenId) => this.Where(t => t.Guid == TokenId).FirstOrDefault()?.IsDeleted ?? true;

        /// <summary>
        /// Returns true if the user has validated their email
        /// </summary>
        /// <param name="u">The user to check for</param>
        /// <returns>True if the user has validated their email</returns>
        public bool IsValidated(User u) => this.IsValidated(u.Guid);

        /// <summary>
        /// Returns true if the user has validated their email
        /// </summary>
        /// <param name="userGuid">The user to check for</param>
        /// <returns>True if the user has validated their email</returns>
        public bool IsValidated(Guid userGuid) => this.Where(t => t.IsValidated && t.Creator == userGuid).Any();

        /// <summary>
        /// Checks to see if the token if the Token is valid
        /// </summary>
        /// <param name="TokenId">The Guid of the Token to check</param>
        /// <returns>If the token is found, and valid</returns>
        public bool ValidateToken(Guid TokenId)
        {
            EmailValidationToken thisToken = this.Get(TokenId);

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

        #endregion Methods

        #region Properties

        /// <summary>
        /// Email template repository for sending out validation emails
        /// </summary>
        protected EmailTemplateRepository EmailTemplateRepository { get; set; }

        /// <summary>
        /// User repository for accessing users
        /// </summary>
        protected UserRepository UserRepository { get; set; }

        #endregion Properties
    }
}