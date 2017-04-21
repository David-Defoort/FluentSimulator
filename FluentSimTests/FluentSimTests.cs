﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentSim;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;
using RestSharp;
using RestSharp.Serializers;
using Should;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace FluentSimTests
{
    public class FluentSimTests
    {
        private FluentSimulator Sim;
        private const string BaseAddress = "http://localhost:8019/";

        [SetUp]
        public void SetUp()
        {
            Sim = new FluentSimulator(BaseAddress);
            Sim.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Sim.Stop();
        }

        [Test]
        public void CanMakeGetRequest()
        {
            Sim.Get("/test")
                .Responds("TEST");

            var resp = MakeGetRequest("/test");
            resp.Content.ShouldEqual("TEST");
            resp.StatusCode.ShouldEqual(HttpStatusCode.OK);
        }

        private static IRestResponse MakeGetRequest(string path)
        {
            var request = new RestRequest(path, Method.GET);
            var client = new RestClient(BaseAddress);
            var resp = client.Execute(request);
            return resp;
        }

        private static IRestResponse MakePostRequest(string path, object body)
        {
            var verb = Method.POST;
            return MakeRequest(path, verb, body);
        }

        private static IRestResponse MakeRequest(string path, Method verb, object body = null)
        {
            var request = new RestRequest(path, verb);
            if (body != null) request.AddParameter("text/json", body, ParameterType.RequestBody);
            var client = new RestClient(BaseAddress);
            var resp = client.Execute(request);
            return resp;
        }

        [Test]
        public void CanMakeGetRequestsOnDifferentUrls()
        {
            Sim.Get("/test1").Responds("output1");
            Sim.Get("/test2").Responds("output2");

            MakeGetRequest("/test1").Content.ShouldEqual("output1");
            MakeGetRequest("/test2").Content.ShouldEqual("output2");
        }

        [Test]
        public void RequestingAUrlThatDoesNotExistReturns501()
        {
            MakeGetRequest("/testUrl").StatusCode.ShouldEqual(HttpStatusCode.NotImplemented);
        }

        [Test]
        public void CanMakeRequestAfterRequestingAUrlThatIsNotConfigured()
        {
            Sim.Get("/test").Responds("out");
            MakeGetRequest("/nonexistent");
            MakeGetRequest("/test").Content.ShouldEqual("out");
        }

        [Test]
        public void WhenARouteEndsInASlashTheUrlIsRecognised()
        {
            Sim.Get("/test").Responds("out");
            MakeGetRequest("/test/").Content.ShouldEqual("out");
        }

        [Test]
        public void WhenTheTargetEndsInSometingDifferentItDoesNotMatchTheConfiguredRoute()
        {
            Sim.Get("/test").Responds("out");
            MakeGetRequest("/test/somethingelse").StatusCode.ShouldEqual(HttpStatusCode.NotImplemented);
        }

        [Test]
        public void WhenTargetStartsWithSometingDifferentItDoesNotMatch()
        {
            Sim.Get("/test").Responds("out");
            MakeGetRequest("something/test").StatusCode.ShouldEqual(HttpStatusCode.NotImplemented);
        }

        [Test]
        public void WhenTheRouteIsADifferentVerbItIsNotMatched()
        {
            Sim.Get("/test").Responds("outhere");
            MakePostRequest("/test", "").StatusCode.ShouldEqual(HttpStatusCode.NotImplemented);
        }

        [Test]
        public void CanMakePostRequest()
        {
            Sim.Post("/test").Responds("outhere");
            MakePostRequest("/test", "").Content.ShouldEqual("outhere");
        }

        [Test]
        public void ThePauseAndResumeWorks()
        {
            var route = Sim.Get("/path").Responds("SOMEOUTPUT");
            route.Pause();
            var timer = new Stopwatch();
            timer.Start();
            ResumeTheRouteInHalfASecond(route);
            MakeGetRequest("/path").Content.ShouldEqual("SOMEOUTPUT");
            timer.Stop();
            timer.ElapsedMilliseconds.ShouldBeGreaterThan(500);
        }

        [Test]
        public void TheTimeDelayWorks()
        {
            Sim.Get("/path").Delay(TimeSpan.FromMilliseconds(500))
                .Responds("delayed");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            MakeGetRequest("/path").Content.ShouldEqual("delayed");
            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(500);
        }

        private static void ResumeTheRouteInHalfASecond(RouteConfigurer route)
        {
            new Thread(() =>
            {
                Thread.Sleep(500);
                route.Resume();
            }).Start();
        }

        [Test]
        public void CanRespondWithCodes()
        {
            Sim.Post("/test").Responds().WithCode(300);
            MakePostRequest("/test", "").StatusCode.ShouldEqual(HttpStatusCode.Ambiguous);
        }

        [Test]
        public void CanRespondWithHeaders()
        {
            Sim.Post("/test").Responds().WithHeader("ThisHeader", "ThisValue");
            var resp = MakePostRequest("/test", "");
            resp.Headers[0].Name.ShouldEqual("ThisHeader");
            resp.Headers[0].Value.ShouldEqual("ThisValue");
        }

        [Test]
        public void CanRespondWithCookies()
        {
            Sim.Get("/test").Responds().WithCookie(new Cookie("name", "VALTEST"));
            var resp = MakeGetRequest("/test");
            var cookie = resp.Cookies[0];
            cookie.Name.ShouldEqual("name");
            cookie.Value.ShouldEqual("VALTEST");
        }

        [Test]
        public void CanReturnSerialisedObjects()
        {
            Sim.Post("/test")
                .Responds(new TestObject());

            MakePostRequest("/test", "").Content.ShouldEqual(@"{""TestField"":""ThisValue""}");
        }

        [Test]
        public void CanUseCustomSerializer()
        {
            Sim.Stop();
            Sim = new FluentSimulator(BaseAddress, new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() }
            });
            Sim.Start();

            Sim.Post("/test")
                .Responds(new TestEnumClass());

            MakePostRequest("/test", "").Content.ShouldEqual(@"{""TestEnumField"":""V2""}");
        }

        [Test]
        public void CanMatchRequestCaseInsensitively()
        {
            Sim.Get("/test").Responds("out");
            MakeGetRequest("/TEST").Content.ShouldEqual("out");
        }

        [Test]
        public void CanMakeOtherVerbRequests()
        {
            MakeVerbRequest(Sim.Post("/test"), Method.POST);
            MakeVerbRequest(Sim.Get("/test"), Method.GET);
            MakeVerbRequest(Sim.Delete("/test"), Method.DELETE);
            MakeVerbRequest(Sim.Head("/test"), Method.HEAD);
            MakeVerbRequest(Sim.Merge("/test"), Method.MERGE);
            MakeVerbRequest(Sim.Options("/test"), Method.OPTIONS);
            MakeVerbRequest(Sim.Patch("/test"), Method.PATCH);
            MakeVerbRequest(Sim.Put("/test"), Method.PUT);
        }

        private void MakeVerbRequest(RouteConfigurer configurer, Method verb)
        {
            if (verb == Method.HEAD)
            {
                configurer.Responds("");
                MakeRequest("/test", verb).StatusCode.ShouldEqual(HttpStatusCode.OK);
                return;
            }

            configurer.Responds(verb + "output");
            MakeRequest("/test", verb).Content.ShouldEqual(verb + "output");
        }

        [Test]
        public void CanGetPreviousRequests()
        {
            Sim.Post("/post").Responds("OK");
            MakePostRequest("/post", "BODY");

            var requests = Sim.ReceivedRequests;
            requests.Count.ShouldEqual(1);
            var firstRequest = requests[0];
            firstRequest.AcceptTypes.Length.ShouldEqual(6);
            firstRequest.Url.AbsoluteUri.ShouldEqual("http://localhost:8019/post");
            firstRequest.UserAgent.ShouldStartWith("RestSharp");
        }

        [Test]
        public void CanGetPreviousRequestBodyAsString()
        {
            Sim.Post("/post").Responds("OK");
            MakePostRequest("/post", "BODY");

            var requests = Sim.ReceivedRequests;
            var firstRequest = requests[0];
            firstRequest.RequestBody.ShouldEqual("BODY");
        }

        [Test]
        public void CanGetPreviousRequestBodyAsObject()
        {
            Sim.Post("/post").Responds("OK");
            MakePostRequest("/post", @"{""TestField"":""TESTHERE""}");

            var requests = Sim.ReceivedRequests;
            var firstRequest = requests[0];
            firstRequest.BodyAs<TestObject>().TestField.ShouldEqual("TESTHERE");
        }

        [Test]
        public void CanGetPreviousBodyWithCustomSerializer()
        {
            Sim.Stop();
            Sim = new FluentSimulator(BaseAddress, new JsonSerializerSettings
            {
                Converters = { new AllFieldsReplacementConverter() }
            });
            Sim.Start();

            Sim.Post("/test");

            MakePostRequest("/test", @"{""TestField"":""original""}");

            Sim.ReceivedRequests[0].BodyAs<TestObject>().TestField.ShouldEqual("REPLACEMENT");
        }

        private class AllFieldsReplacementConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return "REPLACEMENT";
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }
        }

        private class TestObject
        {
            public string TestField = "ThisValue";
        }

        private class TestEnumClass
        {
            public TestEnum TestEnumField = TestEnum.V2;

            public enum TestEnum
            {
                V1,
                V2
            }
        }

    }
}
