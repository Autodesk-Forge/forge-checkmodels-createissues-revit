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

using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DesignCheck.Controllers
{
    public static class OAuthDB
    {
        private static MongoClient _client = null;
        private static IMongoDatabase _database = null;

        private static string OAuthDatabase { get { return Credentials.GetAppSetting("OAUTH_DATABASE"); } }

        private static MongoClient Client
        {
            get
            {
                if (_client == null) _client = new MongoClient(OAuthDatabase);
                return _client;
            }
        }


        private static IMongoDatabase Database
        {
            get
            {
                if (_database == null) _database = Client.GetDatabase(OAuthDatabase.Split('/').Last().Split('?').First());
                return _database;
            }
        }

        public async static Task<bool> Register(string userId, string credentials)
        {
            var users = Database.GetCollection<BsonDocument>("users");
            if (await IsRegistered(userId))
            {
                JObject credentialsJson = JObject.Parse(credentials);
                var filterBuilder = Builders<BsonDocument>.Filter;
                var filter = filterBuilder.Eq("_id", userId);
                var update = Builders<BsonDocument>.Update
                                    .Set("TokenInternal", (string)credentialsJson["TokenInternal"])
                                    .Set("TokenPublic", (string)credentialsJson["TokenPublic"])
                                    .Set("RefreshToken", (string)credentialsJson["RefreshToken"])
                                    .Set("ExpiresAt", ((DateTime)credentialsJson["ExpiresAt"]).ToString("O"));

                try { var result = await users.UpdateOneAsync(filter, update); }
                catch (Exception e) { Console.WriteLine(e); return false; }
            }
            else
            {
                var document = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(credentials);
                document["_id"] = userId;
                try { await users.InsertOneAsync(document); }
                catch (Exception e) { Console.WriteLine(e); return false; }
            }

            return true;
        }

        public static async Task<bool> IsRegistered(string userId)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("_id", userId);
            var users = Database.GetCollection<BsonDocument>("users");
            try { long count = await users.CountAsync(filter); return (count == 1); }
            catch (Exception e) { Console.WriteLine(e); return false; }
        }

        public static async Task<BsonDocument> GetCredentials(string userId)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("_id", userId);
            var users = Database.GetCollection<BsonDocument>("users");
            try
            {
                var docs = await users.FindAsync(filter);
                var doc = docs.First();
                return doc;
            }
            catch (Exception e) { Console.WriteLine(e); return null; }
        }
    }
}