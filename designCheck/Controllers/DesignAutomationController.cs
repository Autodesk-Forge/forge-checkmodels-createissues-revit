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
using Autodesk.Forge.DesignAutomation.v3;
using Autodesk.Forge.Model.DesignAutomation.v3;
using ActivitiesApi = Autodesk.Forge.DesignAutomation.v3.ActivitiesApi;
using Activity = Autodesk.Forge.Model.DesignAutomation.v3.Activity;
using WorkItemsApi = Autodesk.Forge.DesignAutomation.v3.WorkItemsApi;
using WorkItem = Autodesk.Forge.Model.DesignAutomation.v3.WorkItem;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Autodesk.Forge.Model;

namespace DesignCheck.Controllers
{
    public class DesignAutomation4RevitController : ControllerBase
    {
        [HttpGet]
        [Route("api/forge/callback/designautomation/{userId}/{projectId}/{versionId}")]
        public void OnReadyDesignCheck(string userId, string projectId, string versionId)
        {

        }

    }

    public class DesignAutomation4Revit
    {
        const string APPNAME = "DesignCheckApp";
        const string APPBUNBLENAME = "DesignCheckAppBundle.zip";
        const string ACTIVITY_NAME = "DesignCheckActivity";
        const string ALIAS = "v1";

        private string NickName
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

            return new JObject{
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
            var collection = Signature.SignatureHeader(
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

        public async Task StartDesignCheck(string userId, string projectId, string versionId)
        {
            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            TwoLeggedApi oauth = new TwoLeggedApi();
            string appAccessToken = (await oauth.AuthenticateAsync(Credentials.GetAppSetting("FORGE_CLIENT_ID"), Credentials.GetAppSetting("FORGE_CLIENT_SECRET"), oAuthConstants.CLIENT_CREDENTIALS, new Scope[] { Scope.CodeAll })).ToObject<Bearer>().AccessToken;

            await EnsureAppBundle(appAccessToken, "");
            await EnsureActivity(appAccessToken);

            string resultFilename = versionId + ".txt";
            string callbackUrl = string.Format("{0}/{1}/{2}/{3}", Credentials.GetAppSetting("FORGE_DESIGNAUTOMATION_CALLBACK_URL"), userId, projectId, versionId);
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

        // Adapted from https://stackoverflow.com/a/32906946
        private class Signature
        {
            private const string Algorithm = "AWS4-HMAC-SHA256";
            private const string SignedHeaders = "host;x-amz-date";
            private const string ServiceName = "s3";

            /// <summary>
            /// Generate the headers to download (GET) and upload (PUT) to AWS S3 bucket
            /// </summary>
            /// <param name="RegionName">A valid AWS Region</param>
            /// <param name="Host">e.g https://bucketName.s3-us-west-2.amazonaws.com</param>
            /// <param name="verb">Only GET and PUT are implemented on this function</param>
            /// <param name="absolutePath">e.g. /fileName.dwg</param>
            /// <returns>Dictionary with header names and values</returns>
            public static SortedDictionary<string, string> SignatureHeader(
              Amazon.RegionEndpoint RegionName, string Host,
              string verb, string absolutePath)
            {
                var hashedRequestPayload = (verb == WebRequestMethods.Http.Put ? "UNSIGNED-PAYLOAD" : /*Get*/ CreateRequestPayload(string.Empty));
                var currentDateTime = DateTime.UtcNow;
                var dateStamp = currentDateTime.ToString("yyyyMMdd");
                var requestDate = currentDateTime.ToString("yyyyMMddTHHmmss") + "Z";
                var credentialScope = string.Format("{0}/{1}/{2}/aws4_request", dateStamp, RegionName.SystemName, ServiceName);

                var headers = new SortedDictionary<string, string> {
                { "host", Host  },
                { "x-amz-date", requestDate }
                };

                string canonicalHeaders = string.Join("\n", headers.Select(x => x.Key.ToLowerInvariant() + ":" + x.Value.Trim())) + "\n";

                // Task 1: Create a Canonical Request For Signature Version 4
                string canonicalRequest = verb + "\n" + absolutePath + "\n" + "\n" + canonicalHeaders + "\n" + SignedHeaders + "\n" + hashedRequestPayload;
                string hashedCanonicalRequest = HexEncode(Hash(ToBytes(canonicalRequest)));

                // Task 2: Create a String to Sign for Signature Version 4
                string stringToSign = Algorithm + "\n" + requestDate + "\n" + credentialScope + "\n" + hashedCanonicalRequest;

                // Task 3: Calculate the AWS Signature Version 4
                byte[] signingKey = GetSignatureKey(Credentials.GetAppSetting("AWSSecretKey"), dateStamp, RegionName.SystemName, ServiceName);
                string signature = HexEncode(HmacSha256(stringToSign, signingKey));

                // Task 4: Prepare a signed request
                // Authorization: algorithm Credential=access key ID/credential scope, SignedHeadaers=SignedHeaders, Signature=signature
                string authorization = string.Format("{0} Credential={1}/{2}/{3}/{4}/aws4_request, SignedHeaders={5}, Signature={6}",
                  Algorithm, Credentials.GetAppSetting("AWSAccessKey"), dateStamp, RegionName.SystemName, ServiceName, SignedHeaders, signature);

                // Authorization ready, add to the header collection
                headers.Add("Authorization", authorization);

                // for PUT, the content header cannot be defined in adavance, so make as unsigned payload
                headers.Add("x-amz-content-sha256", (verb == WebRequestMethods.Http.Put ? "UNSIGNED-PAYLOAD" : hashedRequestPayload));

                return headers;
            }

            private static string CreateRequestPayload(string jsonString)
            {
                return HexEncode(Hash(ToBytes(jsonString)));
            }

            /// <summary>
            /// From AWS Help http://docs.aws.amazon.com/general/latest/gr/signature-v4-examples.html
            /// </summary>
            private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
            {
                byte[] kDate = HmacSha256(dateStamp, ToBytes("AWS4" + key));
                byte[] kRegion = HmacSha256(regionName, kDate);
                byte[] kService = HmacSha256(serviceName, kRegion);
                return HmacSha256("aws4_request", kService);
            }

            private static byte[] ToBytes(string str)
            {
                return Encoding.UTF8.GetBytes(str.ToCharArray());
            }

            private static string HexEncode(byte[] bytes)
            {
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }

            private static byte[] Hash(byte[] bytes)
            {
                return SHA256.Create().ComputeHash(bytes);
            }

            private static byte[] HmacSha256(string data, byte[] key)
            {
                return new HMACSHA256(key).ComputeHash(ToBytes(data));
            }
        }
    }
}