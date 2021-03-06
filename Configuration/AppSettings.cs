﻿using EastFive.Web;

namespace EastFive.Security.SessionServer.Configuration
{
    [ConfigAttribute]
    public static class AppSettings
    {
        public const string Storage = "Azure.Authorization.Storage";
        //public const string Storage = "EastFive.Security.SessionServer.Storage";
        public const string TokenExpirationInMinutes = "EastFive.Security.SessionServer.tokenExpirationInMinutes";
        public const string LoginIdClaimType = "EastFive.Security.SessionServer.LoginProvider.LoginIdClaimType";

        [ConfigKey("Identifies this application to an AADB2C application",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            Location = "Azure Portal | Azure Active Directory | App Registrations | Application ID",
            PrivateRepositoryOnly = false)]
        public const string AADB2CAudience = "EastFive.Security.LoginProvider.AzureADB2C.Audience";
        public const string AADB2CSigninConfiguration = "EastFive.Security.LoginProvider.AzureADB2C.SigninEndpoint";
        public const string AADB2CSignupConfiguration = "EastFive.Security.LoginProvider.AzureADB2C.SignupEndpoint";

        public const string PingIdentityAthenaRestApiKey = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestApiKey";
        public const string PingIdentityAthenaRestAuthUsername = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestAuthUsername";

        public const string ApplicationInsightsKey = "EastFive.Security.SessionServer.ApplicationInsightsKey";
        
        [ConfigKey("Link that is sent (emailed) to the user to login to the application",
            DeploymentOverrides.Desireable,
            DeploymentSecurityConcern = false,
            Location = "The URL that the webUI is deployed")]
        public const string LandingPage = "EastFive.Security.SessionServer.RouteDefinitions.LandingPage";
        public const string AppleAppSiteAssociationId = "EastFive.Security.SessionServer.AppleAppSiteAssociation.AppId";
        
        [ConfigAttribute]
        public static class OAuth
        {
            [ConfigAttribute]
            public static class Lightspeed
            {
                [ConfigKey("1/2 of the authorization process." + 
                    "This value is used to identify the connecting client or environment.",
                    DeploymentOverrides.Desireable,
                    DeploymentSecurityConcern = false,
                    Location = "This value is first provided at https://cloud.merchantos.com/oauth/register.php but is used to authenticate so cannot be recovered.",
                    MoreInfo = "Sometimes referred to as the client id by Lightspeed.",
                    PrivateRepositoryOnly = false)]
                public const string ClientKey = "OrderOwl.Integrations.Lightspeed.ClientKey";

                [ConfigKey("Other 1/2 of the authorization process." +
                    "This value is used to authenticate the connecting client or environment.",
                    DeploymentOverrides.Desireable,
                    DeploymentSecurityConcern = false,
                    Location = "This value is first provided at https://cloud.merchantos.com/oauth/register.php but is used to authenticate so cannot be recovered." +
                        "Value can be updated at: https://cloud.merchantos.com/oauth/update.php",
                    MoreInfo = "Sometimes referred to as the client id by Lightspeed.",
                    PrivateRepositoryOnly = true)]
                public const string ClientSecret = "OrderOwl.Integrations.Lightspeed.ClientSecret";
            }
        }

        public static class TokenCredential
        {
            /// <summary>
            /// The email address and name from which a token credential is sent.
            /// </summary>
            public const string FromEmail = "EastFive.Security.SessionServer.TokenCredential.FromEmail";
            public const string FromName = "EastFive.Security.SessionServer.TokenCredential.FromName";
            /// <summary>
            /// Subject for token credntial email.
            /// </summary>
            public const string Subject = "EastFive.Security.SessionServer.TokenCredential.Subject";
        }
    }
}
