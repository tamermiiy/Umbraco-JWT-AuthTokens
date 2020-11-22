﻿using System.Net;
using System.Net.Http;
using System.Web.Http;
using Umbraco.Web.Composing;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using UmbracoAuthTokens.Data;

namespace UmbracoAuthTokens.Controllers
{
    [PluginController("TokenAuth")]
    public class SecureApiController : UmbracoApiController
    {
        /// <summary>
        /// http://localhost:49683/umbraco/TokenAuth/SecureApi/Authorise
        /// </summary>
        /// <returns>A JWT token as a string if auth is valid</returns>
        [HttpPost]
        public string Authorise(AuthCredentials auth)
        {
            //Verify user is valid credentials
            var isValidAuth = Security.ValidateBackOfficeCredentials(auth.Username, auth.Password);

            //Are credentials correct?
            if (isValidAuth)
            {
                //Get the backoffice user from username
                var user = Current.Services.UserService.GetByUsername(auth.Username);


                //Generate AuthToken DB object
                var newToken = new UmbracoAuthToken();
                newToken.IdentityId = user.Id;
                newToken.IdentityType = IdentityAuthType.User.ToString();

                //Generate a new token for the user
                var authToken = UmbracoAuthTokenFactory.GenerateUserAuthToken(newToken);

                //Store in DB (inserts or updates existing)
                UserAuthTokenDbHelper.InsertAuthToken(authToken);

                //Return the JWT token as the response
                //This means valid login & client in our case mobile app stores token in local storage
                return authToken.AuthToken;
            }

            //Throw unauthorised HTTP error
            var httpUnauthorised = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            throw new HttpResponseException(httpUnauthorised);
        }
    }
}
