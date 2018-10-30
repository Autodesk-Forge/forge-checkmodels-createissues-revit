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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DesignCheck.Controllers
{
    // Adapted from https://stackoverflow.com/a/32906946
    public class AWSSignature
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