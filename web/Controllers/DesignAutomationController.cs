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

        [HttpGet]
        [Route("api/forge/callback/designautomation/{userId}/{projectId}/{versionId}")]
        public IActionResult OnReadyDesignCheck(string userId, string projectId, string versionId)
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
                BackgroundJob.Schedule(() => CreateIssues(userId, projectId, versionId, _env.ContentRootPath), TimeSpan.FromSeconds(5));
            }
            catch { }

            // ALWAYS return ok (200)
            return Ok();
        }

        public async Task CreateIssues(string userId, string projectId, string versionId, string contentRootPath)
        {
            string bucketName = "revitdesigncheck-" + DesignAutomation4Revit.NickName;
            IAmazonS3 client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2);

            string resultFilename = versionId + ".txt";

            // create AWS Bucket
            if (!await client.DoesS3BucketExistAsync(bucketName)) return;
            Uri downloadFromS3 = new Uri(client.GeneratePreSignedURL(bucketName, resultFilename, DateTime.Now.AddMinutes(90), null));

            // ToDo: is better way?
            string results = Path.Combine(contentRootPath, resultFilename);
            await client.DownloadToFilePathAsync(bucketName, resultFilename, results, null);

            // create issues


            // only delete if it completes
            await client.DeleteObjectAsync(bucketName, resultFilename);
        }
    }
}