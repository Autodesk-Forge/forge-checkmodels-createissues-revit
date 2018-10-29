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

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Hangfire;

namespace DesignCheck.Controllers
{
    public class WebhookController : ControllerBase
    {
        /// <summary>
        /// Credentials on this request
        /// </summary>
        private Credentials Credentials { get; set; }

        // with the api/forge/callback/webhook endpoint
        // e.g. local testing with http://1234.ngrok.io/api/forge/callback/webhook
        public string CallbackUrl { get { return Credentials.GetAppSetting("FORGE_WEBHOOK_CALLBACK_URL"); } }

        private string ExtractFolderIdFromHref(string href)
        {
            string[] idParams = href.Split('/');
            string resource = idParams[idParams.Length - 2];
            string folderId = idParams[idParams.Length - 1];
            if (!resource.Equals("folders")) return string.Empty;
            return folderId;
        }

        private string ExtractProjectIdFromHref(string href)
        {
            string[] idParams = href.Split('/');
            string resource = idParams[idParams.Length - 4];
            string folderId = idParams[idParams.Length - 3];
            if (!resource.Equals("projects")) return string.Empty;
            return folderId;
        }

        [HttpGet]
        [Route("api/forge/webhook")]
        public async Task<IList<GetHookData.Hook>> GetHooks(string href)
        {
            string folderId = ExtractFolderIdFromHref(href);
            if (string.IsNullOrWhiteSpace(folderId)) return null;

            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return null; }

            DMWebhook webhooksApi = new DMWebhook(Credentials.TokenInternal, CallbackUrl);
            IList<GetHookData.Hook> hooks = await webhooksApi.Hooks(Event.VersionAdded, folderId);

            return hooks;
        }

        public class HookInputData
        {
            public string href { get; set; }
        }

        [HttpPost]
        [Route("api/forge/webhook")]
        public async Task<IActionResult> CreateHook([FromForm]HookInputData input)
        {
            string folderId = ExtractFolderIdFromHref(input.href);
            if (string.IsNullOrWhiteSpace(folderId)) return BadRequest();

            string projectId = ExtractProjectIdFromHref(input.href);
            if (string.IsNullOrWhiteSpace(projectId)) return BadRequest();

            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return Unauthorized(); }

            DMWebhook webhooksApi = new DMWebhook(Credentials.TokenInternal, CallbackUrl);
            await webhooksApi.CreateHook(Event.VersionAdded, projectId, folderId);

            return Ok();
        }

        [HttpDelete]
        [Route("api/forge/webhook")]
        public async Task<IActionResult> DeleteHook(HookInputData input)
        {
            string folderId = ExtractFolderIdFromHref(input.href);
            if (string.IsNullOrWhiteSpace(folderId)) return BadRequest();

            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, Response.Cookies);
            if (Credentials == null) { return Unauthorized(); }

            DMWebhook webhooksApi = new DMWebhook(Credentials.TokenInternal, CallbackUrl);
            await webhooksApi.DeleteHook(Event.VersionAdded, folderId);

            return Ok();
        }

        [HttpPost]
        [Route("api/forge/callback/webhook")]
        public async Task<IActionResult> WebhookCallback([FromBody]JObject body)
        {
            // catch any errors, we don't want to return 500
            try
            {
                string eventType = body["hook"]["event"].ToString();
                string userId = body["hook"]["createdBy"].ToString();
                string projectId = body["hook"]["hookAttribute"]["projectId"].ToString();
                string versionId = body["resourceUrn"].ToString();

                // do you want to filter events??
                if (eventType != "dm.version.added") return Ok();

                // your webhook should return immediately!
                // so can start a second thread (not good) or use a queueing system (e.g. hangfire)

                // starting a new thread is not an elegant idea, we don't have control if the operation actually complets...
                /*
                new System.Threading.Tasks.Task(async () =>
                  {
                      // your code here
                  }).Start();
                */

                // use Hangfire to schedule a job
                BackgroundJob.Schedule(() => ExtractMetadata(userId, projectId, versionId), TimeSpan.FromSeconds(30));
            }
            catch { }

            // ALWAYS return ok (200)
            return Ok();
        }

        public async static Task ExtractMetadata(string userId, string projectId, string versionId)
        {
            try
            {
                DesignAutomation4Revit daRevit = new DesignAutomation4Revit();
                await daRevit.StartDesignCheck(userId, projectId, versionId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw; // this should force Hangfire to try again 
            }
        }

        /// <summary>
        /// Base64 encode a string (source: http://stackoverflow.com/a/11743162)
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
