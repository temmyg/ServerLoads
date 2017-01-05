using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using ServerLoads.Controllers;
using ServerLoads.Models;
using System.Web.Http;
using System.Net.Http;
using System.Net;
using System.Web.Http.Results;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ServerLoads.Test
{
    [TestClass]
    public class ServerLoadsControllerTests  //From explore-conflicts 8th time
    {
        ServerLoadsController _controller;
        [ClassInitialize]
        public static void PopulateStore(TestContext tc) {
            DataPointsStore.Store.Enqueue(new DataPoint { ServerName = "Srvr01", CPU = 12.4, RAM = 55.8, Time = new DateTime(2016, 8, 29, 17, 30, 42) });
            DataPointsStore.Store.Enqueue(new DataPoint { ServerName = "Srvr01", CPU = 22.8, RAM = 11.4, Time = new DateTime(2016, 8, 29, 17, 30, 52) });
            DataPointsStore.Store.Enqueue(new DataPoint { ServerName = "Srvr01", CPU = 24.6, RAM = 17.2, Time = new DateTime(2016, 8, 29, 17, 23, 12) });
            DataPointsStore.Store.Enqueue(new DataPoint { ServerName = "Srvr01", CPU = 11.6, RAM = 17.4, Time = new DateTime(2016, 8, 29, 15, 42, 52) });
            DataPointsStore.Store.Enqueue(new DataPoint { ServerName = "Srvr02", CPU = 34, RAM = 11, Time = new DateTime(2016, 8, 29, 17, 35, 12) });
        }

        [TestInitialize]
        public void InitalizeController()
        {
            _controller = new ServerLoadsController();
        }

        [TestMethod]
        public void Test_StoreLoad_Normal()
        {
            //arrange            
            DataPoint dp = new DataPoint() { CPU = 12, RAM = 15, ServerName = "srvr4230" };
            IHttpActionResult response = _controller.StoreLoad(dp);

            Assert.IsInstanceOfType(response, typeof(OkNegotiatedContentResult<string>));

            dp = new DataPoint() { CPU = 14, RAM = 34, ServerName = "srvr4230" };
            _controller.StoreLoad(dp);

            Assert.AreEqual(2, DataPointsStore.Store.Where(dataPoint => dataPoint.ServerName == "srvr4230").Count());
            Assert.IsTrue(DataPointsStore.Store.Contains(dp));
        }

        [TestMethod]
        [ExpectedException(typeof(HttpResponseException))]
        public void Test_StoreLoad_Incomplete()
        {
            try
            {
                DataPoint dp = new DataPoint() { ServerName = "srvr2332" };
                _controller.StoreLoad(dp);
            }
            catch (HttpResponseException ex)
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, ex.Response.StatusCode);
                Assert.AreEqual("wrong input", ex.Response.ReasonPhrase);
                throw;
            }

            try
            {
                DataPoint dp = new DataPoint();
                _controller.StoreLoad(dp);
            }
            catch (HttpResponseException ex)
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, ex.Response.StatusCode);
                Assert.AreEqual("wrong input", ex.Response.ReasonPhrase);
                throw;
            }

        }

        [TestMethod]
        public void Test_StoreLoad_Concurrency()
        {   
            Random rndGen = new Random();
            List<Task> dpLoader = new List<Task>();
            //concurrently load data points
            for (int i = 0; i < 4; i++)
            {
                int idx = i + 1; 
                dpLoader.Add(Task.Run(() => _controller.StoreLoad(new DataPoint()
                {
                    ServerName = "Srvr_Concurrent" + string.Format("{0:00}", idx),
                    CPU = rndGen.Next(0, 100),
                    RAM = rndGen.Next(0, 100)
                })));
            }

            Task.WaitAll(dpLoader.ToArray());

            Assert.AreEqual(4, DataPointsStore.Store.Where(dp=>dp.ServerName.IndexOf("Concurrent")!=-1).Count());

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(DataPointsStore.Store
                    .Where(dp => dp.ServerName == "Srvr_Concurrent" + string.Format("{0:00}", i + 1))
                    .Count() == 1);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(HttpResponseException))]
        public void Test_LoadAverages_ServerName_NotExists()
        {
            _controller = new ServerLoadsController(DateTime.Parse("8/29/2016 17:55:23"));
            _controller.Request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/averageload");
            try
            {
                IHttpActionResult result = _controller.LoadAverages(BreakDownType.ByMinute, "SrvrABC");
            }
            catch(HttpResponseException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.Response.StatusCode);
                HttpError error = (ex.Response.Content as ObjectContent).Value as HttpError;
                Assert.AreEqual("server targed not found", error.Message);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(HttpResponseException))]
        public void Test_LoadAverages_ServerName_Empty()
        {
            _controller = new ServerLoadsController(DateTime.Parse("8/29/2016 17:55:23"));
            //empty server name
            try
            {
                IHttpActionResult result = _controller.LoadAverages(BreakDownType.ByMinute, "");
            }
            catch (HttpResponseException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.Response.StatusCode);
                string error = (ex.Response.Content as StringContent).ToString();
                Assert.AreEqual("ServerName Required", ex.Response.ReasonPhrase);
                Assert.AreEqual("ServerName is Empty", ex.Response.Content.ReadAsStringAsync().Result);
                throw;
            }
        }

        [TestMethod]
        public void Test_LoadAverages_ByMinute()
        {
            _controller = new ServerLoadsController(DateTime.Parse("8/29/2016 17:55:23"));

            IHttpActionResult result = _controller.LoadAverages(BreakDownType.ByMinute, "Srvr01");

            Assert.IsInstanceOfType(result, typeof(ResponseMessageResult));
            var responseResult = result as ResponseMessageResult;
            Assert.AreEqual(HttpStatusCode.OK, responseResult.Response.StatusCode);

            //data points average at 17:30
            AverageLoads loadObj = (responseResult.Response.Content as ObjectContent).Value as AverageLoads;
            Assert.AreEqual(2, loadObj.Loads.Count());
            LoadDetail minutedLoad = loadObj.Loads.First(minLoad => minLoad.Time == "8/29/2016 5:30 PM");
            Assert.AreEqual(((12.4 + 22.8) / 2).ToString("#.##"), minutedLoad.CPULoad);
            Assert.AreEqual(((55.8 + 11.4) / 2).ToString("#.##"), minutedLoad.RAMLoad);
        }

        [TestMethod]
        public void Test_LoadAverages_ByHour()
        {
            _controller = new ServerLoadsController(DateTime.Parse("8/29/2016 18:55:23"));

            IHttpActionResult result = _controller.LoadAverages(BreakDownType.ByHour, "Srvr01");

            Assert.IsInstanceOfType(result, typeof(ResponseMessageResult));
            var responseResult = result as ResponseMessageResult;
            Assert.AreEqual(HttpStatusCode.OK, responseResult.Response.StatusCode);

            AverageLoads loadObj = (responseResult.Response.Content as ObjectContent).Value as AverageLoads;
            Assert.AreEqual(2, loadObj.Loads.Count());

            //average at 17:00
            LoadDetail hourLoad = loadObj.Loads.First(hrload => hrload.Time == "8/29/2016 17:00h");
            Assert.AreEqual(((12.4 + 22.8 + 24.6) / 3).ToString("#.##"), hourLoad.CPULoad);
            Assert.AreEqual(((55.8 + 11.4 + 17.2) / 3).ToString("#.##"), hourLoad.RAMLoad);

            //average at 15:00
            hourLoad = loadObj.Loads.First(hrload => hrload.Time == "8/29/2016 15:00h");
            Assert.AreEqual(11.6.ToString("#.##"), hourLoad.CPULoad);
        }
    }

}

