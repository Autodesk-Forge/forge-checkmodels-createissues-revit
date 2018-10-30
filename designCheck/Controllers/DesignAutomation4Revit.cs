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
using Autodesk.Forge.DesignAutomation.v3;
using Autodesk.Forge.Model;
using Autodesk.Forge.Model.DesignAutomation.v3;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ActivitiesApi = Autodesk.Forge.DesignAutomation.v3.ActivitiesApi;
using Activity = Autodesk.Forge.Model.DesignAutomation.v3.Activity;
using WorkItem = Autodesk.Forge.Model.DesignAutomation.v3.WorkItem;
using WorkItemsApi = Autodesk.Forge.DesignAutomation.v3.WorkItemsApi;

namespace DesignCheck.Controllers
{
    public class DesignAutomation4Revit
    {
        private const string APPNAME = "DesignCheckApp";
        private const string APPBUNBLENAME = "DesignCheckAppBundle.zip";
        private const string ACTIVITY_NAME = "DesignCheckActivity";
        private const string ALIAS = "v1";

        public static string NickName
        {
            get
            {
                return Credentials.GetAppSetting("FORGE_CLIENT_ID");
            }
        }

        public async Task EnsureAppBundle(string appAccessToken, string contentRootPath)
        {
            //List<string> apps = await da.GetAppBundles(nickName);
            AppBundlesApi appBundlesApi = new AppBundlesApi();
            appBundlesApi.Configuration.AccessToken = appAccessToken;

            // at this point we can either call get by alias/id and catch or get a list and check
            //dynamic appBundle = await appBundlesApi.AppbundlesByIdAliasesByAliasIdGetAsync(APPNAME, ALIAS);

            // or get the list and check for the name
            PageString appBundles = await appBundlesApi.AppBundlesGetItemsAsync();
            bool existAppBundle = false;
            foreach (string appName in appBundles.Data)
            {
                if (appName.Contains(string.Format("{0}.{1}+{2}", NickName, APPNAME, ALIAS)))
                {
                    existAppBundle = true;
                    continue;
                }
            }

            if (!existAppBundle)
            {
                // check if ZIP with bundle is here
                string packageZipPath = Path.Combine(contentRootPath, APPBUNBLENAME);
                if (!System.IO.File.Exists(packageZipPath)) throw new Exception("Change Parameter bundle not found at " + packageZipPath);

                // create bundle
                AppBundle appBundleSpec = new AppBundle(APPNAME, null, "Autodesk.Revit+2018", null, null, APPNAME, null, APPNAME);
                AppBundle newApp = await appBundlesApi.AppBundlesCreateItemAsync(appBundleSpec);
                if (newApp == null) throw new Exception("Cannot create new app");

                // create alias
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await appBundlesApi.AppBundlesCreateAliasAsync(APPNAME, aliasSpec);

                // upload the zip with .bundle
                RestClient uploadClient = new RestClient(newApp.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, object> x in newApp.UploadParameters.FormData)
                    request.AddParameter(x.Key, x.Value);
                request.AddFile("file", packageZipPath);
                request.AddHeader("Cache-Control", "no-cache");
                var res = await uploadClient.ExecuteTaskAsync(request);
            }
        }

        public async Task EnsureActivity(string appAccessToken)
        {
            ActivitiesApi activitiesApi = new ActivitiesApi();
            activitiesApi.Configuration.AccessToken = appAccessToken;
            PageString activities = await activitiesApi.ActivitiesGetItemsAsync();

            bool existActivity = false;
            foreach (string activity in activities.Data)
            {
                if (activity.Contains(string.Format("{0}.{1}+{2}", NickName, ACTIVITY_NAME, ALIAS)))
                {
                    existActivity = true;
                    continue;
                }
            }

            if (!existActivity)
            {
                // create activity
                string commandLine = string.Format(@"$(engine.path)\\revitcoreconsole.exe /i $(args[rvtFile].path) /al $(appbundles[{0}].path)", APPNAME);
                ModelParameter rvtFile = new ModelParameter(false, false, ModelParameter.VerbEnum.Get, "Input Revit File", true, "$(rvtFile)");
                //ModelParameter parameterInput = new ModelParameter(false, false, ModelParameter.VerbEnum.Get, "JSON input", false, "params.json");
                ModelParameter result = new ModelParameter(false, false, ModelParameter.VerbEnum.Put, "Resulting JSON File", true, "result.txt");
                Activity activitySpec = new Activity(
                  new List<string>() { commandLine },
                  new Dictionary<string, ModelParameter>() {
                    { "rvtFile", rvtFile },
                    //{ "parameterInput", parameterInput },
                    { "result", result }
                  },
                  "Autodesk.Revit+2019",
                  new List<string>() { string.Format("{0}.{1}+{2}", NickName, APPNAME, ALIAS) },
                  null,
                  ACTIVITY_NAME,
                  null,
                  ACTIVITY_NAME);
                Activity newActivity = await activitiesApi.ActivitiesCreateItemAsync(activitySpec);

                // create alias
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await activitiesApi.ActivitiesCreateAliasAsync(ACTIVITY_NAME, aliasSpec);
            }
        }

        private async Task<JObject> BuildDownloadURL(string userAccessToken, string projectId, string versionId)
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

            return new JObject
            {
                new JProperty("url", downloadUrl),
                new JProperty("headers",
                new JObject{
                    new JProperty("Authorization", "Bearer " + userAccessToken)
                })
            };
        }

        private JObject BuildUploadURL(string resultFilename)
        {
            string bucketName = "revitdesigncheck-" + NickName;
            IAmazonS3 client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2);

            Dictionary<string, object> props = new Dictionary<string, object>();
            props.Add("Verb", "PUT");
            Uri uploadToS3 = new Uri(client.GeneratePreSignedURL(bucketName, resultFilename, DateTime.Now.AddMinutes(10), props));

            return new JObject
            {
                new JProperty("verb", "PUT"),
                new JProperty("url", uploadToS3.GetLeftPart(UriPartial.Path)),
                new JProperty("headers",MakeHeaders(WebRequestMethods.Http.Put, uploadToS3))
            };
        }

        private JObject MakeHeaders(string verb, Uri query)
        {
            // Prepare headers
            var collection = AWSSignature.SignatureHeader(
              Amazon.RegionEndpoint.USWest2, query.Host,
              verb, query.AbsolutePath);

            // organize headers for Design Automation
            JObject headers = new JObject();
            foreach (KeyValuePair<string, string> item in collection)
            {
                headers.Add(new JProperty(item.Key, item.Value));
            }

            return headers;
        }

        public async Task StartDesignCheck(string userId, string projectId, string versionId, string contentRootPath)
        {
            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            TwoLeggedApi oauth = new TwoLeggedApi();
            string appAccessToken = (await oauth.AuthenticateAsync(Credentials.GetAppSetting("FORGE_CLIENT_ID"), Credentials.GetAppSetting("FORGE_CLIENT_SECRET"), oAuthConstants.CLIENT_CREDENTIALS, new Scope[] { Scope.CodeAll })).ToObject<Bearer>().AccessToken;

            await EnsureAppBundle(appAccessToken, contentRootPath);
            await EnsureActivity(appAccessToken);

            string resultFilename = versionId + ".txt";
            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation/{1}/{2}/{3}", Credentials.GetAppSetting("FORGE_CALLBACK_URL"), userId, projectId, versionId);
            WorkItem workItemSpec = new WorkItem(
              null,
              string.Format("{0}.{1}+{2}", NickName, ACTIVITY_NAME, ALIAS),
              new Dictionary<string, JObject>()
              {
                  { "rvtFile", await BuildDownloadURL(credentials.TokenInternal, projectId, versionId) },
                  { "result", BuildUploadURL(resultFilename)  },
                  { "onComplete", new JObject { new JProperty("verb", "GET"), new JProperty("URL", callbackUrl) }}
              },
              null);
            WorkItemsApi workItemApi = new WorkItemsApi();
            workItemApi.Configuration.AccessToken = appAccessToken;
            WorkItemStatus newWorkItem = await workItemApi.WorkItemsCreateWorkItemsAsync(null, null, workItemSpec);
        }
    }
}