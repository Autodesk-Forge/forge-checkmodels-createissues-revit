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
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DesignCheck.Controllers
{
    public class DesignAutomationController : ControllerBase
    {
        private IHostingEnvironment _env;
        public DesignAutomationController(IHostingEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        [Route("api/forge/callback/designautomation/{userId}/{hubId}/{projectId}/{versionId}")]
        public IActionResult OnReadyDesignCheck(string userId, string hubId, string projectId, string versionId, [FromBody]dynamic body)
        {
            // catch any errors, we don't want to return 500
            try
            {
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
                BackgroundJob.Schedule(() => CreateIssues(userId, hubId, projectId, versionId, _env.WebRootPath, Request.Host.ToString()), TimeSpan.FromSeconds(1));
            }
            catch { }

            // ALWAYS return ok (200)
            return Ok();
        }

        public async Task CreateIssues(string userId, string hubId, string projectId, string versionId, string contentRootPath, string host)
        {
            string bucketName = "revitdesigncheck" + DesignAutomation4Revit.NickName.ToLower();
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(Credentials.GetAppSetting("AWS_ACCESS_KEY"), Credentials.GetAppSetting("AWS_SECRET_KEY"));
            IAmazonS3 client = new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.USWest2);

            string resultFilename = versionId + ".txt";

            // create AWS Bucket
            if (!await client.DoesS3BucketExistAsync(bucketName)) return;
            Uri downloadFromS3 = new Uri(client.GeneratePreSignedURL(bucketName, resultFilename, DateTime.Now.AddMinutes(10), null));


            // ToDo: is there a better way?
            string results = Path.Combine(contentRootPath, resultFilename);
            var keys = await client.GetAllObjectKeysAsync(bucketName, null, null);
            if (!keys.Contains(resultFilename)) return; // file is not there
            await client.DownloadToFilePathAsync(bucketName, resultFilename, results, null);
            string contents = System.IO.File.ReadAllText(results);

            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            VersionsApi versionApi = new VersionsApi();
            versionApi.Configuration.AccessToken = credentials.TokenInternal;
            dynamic versionItem = await versionApi.GetVersionItemAsync(projectId, versionId.Base64Decode());
            string itemId = versionItem.data.id;
            int version = Int32.Parse(versionId.Split("_")[1].Base64Decode().Split("=")[1]);

            string title = string.Format("Column clash report for version {0}", version);
            string description = string.Format("<a href=\"http://{0}/issues/?urn={1}&id={2}\" target=\"_blank\">Click to view issues</a>", host, versionId, contents.Base64Encode());

            // create issues
            BIM360Issues issues = new BIM360Issues();
            string containerId = await issues.GetContainer(credentials.TokenInternal, hubId, projectId);
            await issues.CreateIssue(credentials.TokenInternal, containerId, itemId, version, title, description);

            // only delete if it completes
            System.IO.File.Delete(resultFilename);
            await client.DeleteObjectAsync(bucketName, resultFilename);
        }
    }
}