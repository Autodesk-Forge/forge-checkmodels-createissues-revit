/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using Autodesk.Forge;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DesignCheck.Controllers
{
    public class UserController : ControllerBase
    {
        [HttpGet]
        [Route("api/forge/user/profile")]
        public async Task<JObject> GetUserProfileAsync()
        {
            Credentials credentials = await Credentials.FromSessionAsync(Request.Cookies, Response.Cookies);
            if (credentials == null)
            {
                return null;
            }

            // the API SDK
            UserProfileApi userApi = new UserProfileApi();
            userApi.Configuration.AccessToken = credentials.TokenInternal;


            // get the user profile
            dynamic userProfile = await userApi.GetUserProfileAsync();

            // prepare a response with name & picture
            dynamic response = new JObject();
            response.name = string.Format("{0} {1}", userProfile.firstName, userProfile.lastName);
            response.picture = userProfile.profileImages.sizeX40;
            return response;
        }
    }
}