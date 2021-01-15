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

using Amazon.S3;
using Autodesk.Forge;
using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;



namespace DesignCheck.Controllers
{
    public class DesignAutomation4Revit
    {
        private const string APPNAME = "FindColumnsApp";
        private const string APPBUNBLENAME = "FindColumnsIO.zip";
        private const string ACTIVITY_NAME = "FindColumnsActivity";
        protected string Script { get; set; }
        private const string ENGINE_NAME = "Autodesk.Revit+2019";

        /// NickName.AppBundle+Alias
        private string AppBundleFullName { get { return string.Format("{0}.{1}+{2}", Utils.NickName, APPNAME, Alias); } }
        /// NickName.Activity+Alias
        private string ActivityFullName { get { return string.Format("{0}.{1}+{2}", Utils.NickName, ACTIVITY_NAME, Alias); } }
        /// Prefix for AppBundles and Activities
        public static string NickName { get { return Credentials.GetAppSetting("FORGE_CLIENT_ID"); } }
        /// Alias for the app (e.g. DEV, STG, PROD). This value may come from an environment variable
        public static string Alias { get { return "dev"; } }
        // Design Automation v3 API
        private DesignAutomationClient _designAutomation;

        public DesignAutomation4Revit()
        {
            // need to initialize manually as this class runs in background
            ForgeService service =
                new ForgeService(
                    new HttpClient(
                        new ForgeHandler(Microsoft.Extensions.Options.Options.Create(new ForgeConfiguration()
                        {
                            ClientId = Credentials.GetAppSetting("FORGE_CLIENT_ID"),
                            ClientSecret = Credentials.GetAppSetting("FORGE_CLIENT_SECRET")
                        }))
                        {
                            InnerHandler = new HttpClientHandler()
                        })
                );
            _designAutomation = new DesignAutomationClient(service);
        }

        public async Task EnsureAppBundle(string contentRootPath)
        {
            // get the list and check for the name
            Page<string> appBundles = await _designAutomation.GetAppBundlesAsync();
            bool existAppBundle = false;
            foreach (string appName in appBundles.Data)
            {
                if (appName.Contains(AppBundleFullName))
                {
                    existAppBundle = true;
                    continue;
                }
            }

            if (!existAppBundle)
            {
                // check if ZIP with bundle is here
                string packageZipPath = Path.Combine(contentRootPath + "/bundles/", APPBUNBLENAME);
                if (!File.Exists(packageZipPath)) throw new Exception(APPBUNBLENAME +" not found at " + packageZipPath);

                AppBundle appBundleSpec = new AppBundle()
                {
                    Package = APPNAME,
                    Engine = ENGINE_NAME,
                    Id = APPNAME,
                    Description = string.Format("Description for {0}", APPBUNBLENAME),

                };
                AppBundle newAppVersion = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newAppVersion == null) throw new Exception("Cannot create new app");

                // create alias pointing to v1
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateAppBundleAliasAsync(APPNAME, aliasSpec);

                // upload the zip with .bundle
                RestClient uploadClient = new RestClient(newAppVersion.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, string> x in newAppVersion.UploadParameters.FormData) request.AddParameter(x.Key, x.Value);
                request.AddFile("file", packageZipPath);
                request.AddHeader("Cache-Control", "no-cache");
                await uploadClient.ExecuteTaskAsync(request);
            }
        }

        public async Task EnsureActivity()
        {
            Page<string> activities = await _designAutomation.GetActivitiesAsync();

            bool existActivity = false;
            foreach (string activity in activities.Data)
            {
                if (activity.Contains(ActivityFullName))
                {
                    existActivity = true;
                    continue;
                }
            }

            if (!existActivity)
            {
                string commandLine = string.Format(@"$(engine.path)\\revitcoreconsole.exe /i $(args[inputFile].path) /al $(appbundles[{0}].path)", APPNAME);
                Activity activitySpec = new Activity()
                {
                    Id = ACTIVITY_NAME,
                    Appbundles = new List<string>() { AppBundleFullName },
                    CommandLine = new List<string>() { $"\"{commandLine}\"" },
                    Engine = ENGINE_NAME,
                    Parameters = new Dictionary<string, Parameter>()
                    {
                        { "inputFile", new Parameter() { Description = "Input Revit File", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                        { "result", new Parameter() { Description = "Resulting JSON File", LocalName = "result.txt", Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                    },
                    Settings = new Dictionary<string, ISetting>()
                    {
                        { "script", new StringSetting(){ Value = Script } }
                    }
                };
                Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                // specify the alias for this Activity
                Alias aliasSpec = new Alias() { Id = Alias, Version = 1 };
                Alias newAlias = await _designAutomation.CreateActivityAliasAsync(ACTIVITY_NAME, aliasSpec);
            }
        }

        private async Task<XrefTreeArgument> BuildDownloadURL(string userAccessToken, string projectId, string versionId)
        {
            VersionsApi versionApi = new VersionsApi();
            versionApi.Configuration.AccessToken = userAccessToken;
            dynamic version = await versionApi.GetVersionAsync(projectId, versionId);
            dynamic versionItem = await versionApi.GetVersionItemAsync(projectId, versionId);

            string[] versionItemParams = ((string)version.data.relationships.storage.data.id).Split('/');
            string[] bucketKeyParams = versionItemParams[versionItemParams.Length - 2].Split(':');
            string bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
            string objectName = versionItemParams[versionItemParams.Length - 1];
            string downloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, objectName);

            return new XrefTreeArgument()
            {
                Url = downloadUrl,
                Verb = Verb.Get,
                Headers = new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer " + userAccessToken }
                }
            };
        }

        private async Task<XrefTreeArgument> BuildUploadURL(string resultFilename)
        {
            string bucketName = "revitdesigncheck" + NickName.ToLower();
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(Credentials.GetAppSetting("AWS_ACCESS_KEY"), Credentials.GetAppSetting("AWS_SECRET_KEY"));
            IAmazonS3 client = new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.USWest2);

            if (!await client.DoesS3BucketExistAsync(bucketName))
                await client.EnsureBucketExistsAsync(bucketName);

            Dictionary<string, object> props = new Dictionary<string, object>();
            props.Add("Verb", "PUT");
            Uri uploadToS3 = new Uri(client.GeneratePreSignedURL(bucketName, resultFilename, DateTime.Now.AddMinutes(10), props));

            return new XrefTreeArgument()
            {
                Url = uploadToS3.ToString(),
                Verb = Verb.Put
            };
        }

        public async Task StartDesignCheck(string userId, string hubId, string projectId, string versionId, string contentRootPath)
        {
            // uncomment these lines to clear all appbundles & activities under your account
            //await _designAutomation.DeleteForgeAppAsync("me");

            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            await EnsureAppBundle(contentRootPath);
            await EnsureActivity();

            string resultFilename = versionId.Base64Encode() + ".txt";
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/{1}/{2}/{3}/{4}", Credentials.GetAppSetting("FORGE_WEBHOOK_URL"), userId, hubId, projectId, versionId.Base64Encode());

            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = ActivityFullName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", await BuildDownloadURL(credentials.TokenInternal, projectId, versionId) },
                    { "result",  await BuildUploadURL(resultFilename) },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
        }
    }
}