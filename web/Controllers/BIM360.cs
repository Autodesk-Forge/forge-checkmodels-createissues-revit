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
    public class BIM360Issues
    {
        private const string BASE_URL = "https://developer.api.autodesk.com";

        public async Task<string> GetContainer(string userAccessToken, string hubId, string projectId)
        {
            ProjectsApi projectsApi = new ProjectsApi();
            projectsApi.Configuration.AccessToken = userAccessToken;
            var project = await projectsApi.GetProjectAsync(hubId, projectId);
            var issues = project.data.relationships.issues.data;
            if (issues.type != "issueContainerId") return null;
            return issues.id.ToString();
        }

        public async Task CreateIssue(string userAccessToken, string containerId, string itemId, int version, string title, string description)
        {
            dynamic body = new JObject();
            body.data = new JObject();
            body.data.type = "issues";
            body.data.attributes = new JObject();
            body.data.attributes.title = title;
            body.data.attributes.description = description;
            body.data.attributes.status = "open";
            body.data.attributes.starting_version = version;
            body.data.attributes.target_urn = itemId;
            //body.data.attributes.pushpin_attributes = new JObject();
            //body.data.attributes.pushpin_attributes.object_id = dbId;
            //body.data.attributes.pushpin_attributes.type = "TwoDVectorPushpin";
            //body.data.attributes.pushpin_attributes.created_doc_version = version;


            RestClient client = new RestClient(BASE_URL);
            RestRequest request = new RestRequest("/issues/v1/containers/{container_id}/issues", RestSharp.Method.POST);
            request.AddParameter("container_id", containerId, ParameterType.UrlSegment);
            request.AddHeader("Authorization", "Bearer " + userAccessToken);
            request.AddHeader("Content-Type", "application/vnd.api+json");
            request.AddParameter("text/json", Newtonsoft.Json.JsonConvert.SerializeObject(body), ParameterType.RequestBody);

            var res = await client.ExecuteTaskAsync(request);
        }
    }
}