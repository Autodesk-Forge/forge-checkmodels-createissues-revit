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

using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DesignCheck.Controllers
{
    public class DMWebhook
    {
        private static RestClient client = new RestClient("https://developer.api.autodesk.com");
        private string AccessToken { get; set; }
        private string CallbackURL { get; set; }

        public DMWebhook(string accessToken, string callbackUrl)
        {
            AccessToken = accessToken;
            CallbackURL = callbackUrl;
        }

        /// <summary>
        /// http://developer.autodesk.com/en/docs/webhooks/v1/reference/http/systems-system-events-event-hooks-GET/
        /// </summary>
        /// <param name="eventType"></param>
        /// <returns></returns>
        public async Task<IList<GetHookData.Hook>> Hooks(Event eventType, string folderId)
        {
            RestRequest request = new RestRequest("/webhooks/v1/systems/data/events/{event}/hooks?scopeName=folder&scopeValue={folderId}", Method.GET);
            request.AddParameter("event", EnumToString(eventType), ParameterType.UrlSegment);
            request.AddParameter("folderId", folderId, ParameterType.UrlSegment);
            request.AddHeader("Authorization", "Bearer " + AccessToken);

            IRestResponse<GetHookData> response = await client.ExecuteTaskAsync<GetHookData>(request);

            return response.Data.data;
        }

        /// <summary>
        /// Create hook for a specific event
        /// http://developer.autodesk.com/en/docs/webhooks/v1/reference/http/systems-system-events-event-hooks-POST/
        /// </summary>
        /// <returns></returns>
        public async Task<HttpStatusCode> CreateHook(Event eventType, string hubId, string projectId, string folderId)
        {
            dynamic body = new JObject();
            body.callbackUrl = CallbackURL;
            body.scope = new JObject();
            body.scope.folder = folderId;
            body.hookAttribute = new JObject();
            body.hookAttribute.projectId = projectId;
            body.hookAttribute.hubId = hubId;

            RestRequest request = new RestRequest("/webhooks/v1/systems/data/events/{event}/hooks", Method.POST);
            request.AddParameter("event", EnumToString(eventType), ParameterType.UrlSegment);
            request.AddHeader("Authorization", "Bearer " + AccessToken);
            request.AddParameter("application/json", body.ToString(), ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteTaskAsync(request);

            return response.StatusCode;
        }

        /// <summary>
        /// http://developer.autodesk.com/en/docs/webhooks/v1/reference/http/systems-system-events-event-hooks-hook_id-DELETE/
        /// </summary>
        /// <returns></returns>
        public async Task<IDictionary<string, HttpStatusCode>> DeleteHook(Event eventType, string folderId)
        {
            IList<GetHookData.Hook> hooks = await Hooks(eventType, folderId);
            IDictionary<string, HttpStatusCode> status = new Dictionary<string, HttpStatusCode>();

            foreach (GetHookData.Hook hook in hooks)
            {
                RestRequest request = new RestRequest("/webhooks/v1/systems/data/events/{event}/hooks/{hook_id}", Method.DELETE);
                request.AddParameter("event", EnumToString(eventType), ParameterType.UrlSegment);
                request.AddParameter("hook_id", hook.hookId, ParameterType.UrlSegment);
                request.AddHeader("Authorization", "Bearer " + AccessToken);
                IRestResponse response = await client.ExecuteTaskAsync(request);

                status.Add(hook.hookId, response.StatusCode);
            }

            return status;
        }

        private string EnumToString(Event eventType)
        {
            return "dm." + string.Join(".", Regex.Split(System.Enum.GetName(typeof(Event), eventType), @"(?<!^)(?=[A-Z])")).ToLower();
        }
    }

    // generated with http://json2csharp.com/
    public class GetHookData
    {
        public Links links { get; set; }
        public List<Hook> data { get; set; }

        public class Links
        {
            public object next { get; set; }
        }

        public class Hook
        {
            public string hookId { get; set; }
            public string tenant { get; set; }
            public string callbackUrl { get; set; }
            public string createdBy { get; set; }
            public string @event { get; set; }
            public DateTime createdDate { get; set; }
            public string system { get; set; }
            public string creatorType { get; set; }
            public string status { get; set; }
            public Scope scope { get; set; }
            public string urn { get; set; }
            public string __self__ { get; set; }

            public class Scope
            {
                public string folder { get; set; }
            }
        }
    }

    public enum Event
    {
        VersionAdded,
        VersionModified,
        VersionDeleted,
        VersionMoved,
        VersionCoped
    }
}