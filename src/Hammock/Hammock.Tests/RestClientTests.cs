﻿using System;
using System.Configuration;
using System.Net;
using Hammock.Authentication;
using Hammock.Caching;
using Hammock.OAuth;
using Hammock.Serialization;
using Hammock.Tests.Converters;
using Hammock.Tests.Helpers;
using Hammock.Tests.Postmark;
using Hammock.Web;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Hammock.Tests
{
    [TestFixture]
    public partial class RestClientTests
    {
        private string _twitterUsername;
        private string _twitterPassword;
        
        private string _postmarkServerToken;
        private string _postmarkFromAddress;
        private string _postmarkToAddress;

        private string _consumerKey;
        private string _consumerSecret;

        [SetUp]
        public void SetUp()
        {
            _twitterUsername = ConfigurationManager.AppSettings["TwitterUsername"];
            _twitterPassword = ConfigurationManager.AppSettings["TwitterPassword"];

            _postmarkServerToken = ConfigurationManager.AppSettings["PostmarkServerToken"];
            _postmarkFromAddress = ConfigurationManager.AppSettings["PostmarkFromAddress"];
            _postmarkToAddress = ConfigurationManager.AppSettings["PostmarkToAddress"];

            _consumerKey = ConfigurationManager.AppSettings["OAuthConsumerKey"];
            _consumerSecret = ConfigurationManager.AppSettings["OAuthConsumerSecret"];
        }

        public IWebCredentials BasicAuthForTwitter
        {
            get
            {
                var credentials = new BasicAuthCredentials
                                      {
                                          Username = _twitterUsername,
                                          Password = _twitterPassword
                                      };
                return credentials;
            }
        }

        public IWebCredentials OAuthForTwitterRequestToken
        {
            get
            {
                var credentials = new OAuthCredentials
                                      {
                                          Type = OAuthType.RequestToken,
                                          SignatureMethod = OAuthSignatureMethod.HmacSha1,
                                          ParameterHandling = OAuthParameterHandling.HttpAuthorizationHeader,
                                          ConsumerKey = _consumerKey,
                                          ConsumerSecret = _consumerSecret,
                                      };
                return credentials;
            }
        }
        
        [Test]
        public void Can_make_basic_auth_request_synchronously()
        {
            var client = new RestClient
                             {
                                 Authority = "http://api.twitter.com",
                                 VersionPath = "1"
                             };

            var request = new RestRequest
                              {
                                  Credentials = BasicAuthForTwitter,
                                  Path = "statuses/home_timeline.json"
                              };

            var response = client.Request(request);

            Assert.IsNotNull(response);
            Console.WriteLine(response);
        }

        [Test]
        public void Can_make_basic_auth_request_with_caching_synchronously()
        {
            var client = new RestClient
                             {
                                 Authority = "http://api.twitter.com",
                                 VersionPath = "1",
                                 Cache = CacheFactory.AspNetCache,
                                 CacheKeyFunction = () => _twitterUsername,
                                 CacheOptions = new CacheOptions
                                                    {
                                                        Duration = 10.Minutes(),
                                                        Mode = CacheMode.AbsoluteExpiration
                                                    }
                             };

            var request = new RestRequest
                              {
                                  Credentials = BasicAuthForTwitter,
                                  Path = "statuses/home_timeline.json",
                              };

            var first = client.Request(request);
            Assert.IsNotNull(first);
            Assert.IsFalse(first.IsFromCache, "First request was not served from the web.");

            var second = client.Request(request);
            Assert.IsNotNull(second);
            Assert.IsTrue(second.IsFromCache, "Second request was not served from cache.");
        }

        [Test]
        public void Can_make_basic_auth_request_with_headers_synchronously()
        {
            var client = new RestClient
                             {
                                 Authority = "http://api.twitter.com",
                                 VersionPath = "1",
                                 UserAgent = "Hammock"
                             };

            client.AddHeader("Always", "on every request");

            var request = new RestRequest
            {
                Credentials = BasicAuthForTwitter,
                Path = "/statuses/home_timeline.json"
            };

            request.AddHeader("Only", "on this request");

            var response = client.Request(request);

            Assert.IsNotNull(response);
            Console.WriteLine(response.Content);
        }

        [Test]
        public void Can_make_basic_auth_request_get_with_url_parameters_synchronously()
        {
            var client = new RestClient
            {
                Authority = "http://api.twitter.com",
                VersionPath = "1",
                UserAgent = "Hammock"
            };

            var request = new RestRequest
            {
                Credentials = BasicAuthForTwitter,
                Path = "/statuses/home_timeline.json"
            };

            client.AddParameter("client", "true");
            request.AddParameter("request", "true");

            var response = client.Request(request);

            Assert.IsNotNull(response);
            Console.WriteLine(response.Content);
        }

        [Test]
        [Ignore("This test makes a live update")]
        public void Can_make_basic_auth_request_post_with_post_parameters_synchronously()
        {
            ServicePointManager.Expect100Continue = false;

            var client = new RestClient
            {
                Authority = "http://api.twitter.com",
                VersionPath = "1",
                UserAgent = "Hammock"
            };

            var request = new RestRequest
            {
                Credentials = BasicAuthForTwitter,
                Path = "/statuses/update.json",
                Method = WebMethod.Post
            };

            client.AddParameter("status", "testing something new and awesome");
            
            var response = client.Request(request);

            Assert.IsNotNull(response);
            Console.WriteLine(response.Content);
        }

        [Test]
        [Ignore("This test requires Postmark access and costs money")]
        public void Can_make_basic_auth_request_post_with_json_entity_synchronously()
        {
            var settings = GetSerializerSettings();
            
            var message = new PostmarkMessage
            {
                From = _postmarkFromAddress,
                To = _postmarkToAddress,
                Subject = "Test passed!",
                TextBody = "Hello from the Hammock unit tests!"
            };

            var serializer = new HammockJsonDotNetSerializer(settings);

            var client = new RestClient
            {
                Authority = "http://api.postmarkapp.com",
                Serializer = serializer,
                Deserializer = serializer
            };

            client.AddHeader("Accept", "application/json");
            client.AddHeader("Content-Type", "application/json; charset=utf-8");
            client.AddHeader("X-Postmark-Server-Token", _postmarkServerToken);
            client.AddHeader("User-Agent", "Hammock");

            var request = new RestRequest
                              {
                                  Path = "email",
                                  Entity = message
                              };

            var response = client.Request<PostmarkResponse>(request);
            var result = response.ContentEntity;

            Assert.IsNotNull(response);
            Assert.IsNotNull(result);
            Console.WriteLine(response.Content);
        }

        [Test]
        public void Can_get_oauth_request_token_sequentially()
        {
            var client = new RestClient
                             {
                                 Authority = "http://twitter.com/oauth"
                             };

            var request = new RestRequest
                              {
                                  Credentials = OAuthForTwitterRequestToken,
                                  Path="request_token"
                              };

            var response = client.Request(request);
            Assert.IsNotNull(response);
        }

        private static JsonSerializerSettings GetSerializerSettings()
        {
            var settings = new JsonSerializerSettings
                               {
                                   MissingMemberHandling = MissingMemberHandling.Ignore,
                                   NullValueHandling = NullValueHandling.Include,
                                   DefaultValueHandling = DefaultValueHandling.Include
                               };

            settings.Converters.Add(new UnicodeJsonStringConverter());
            settings.Converters.Add(new NameValueCollectionConverter());
            return settings;
        }
    }
}

