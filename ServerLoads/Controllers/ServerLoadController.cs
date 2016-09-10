
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Collections.Concurrent;
using System.Net.Http.Formatting;
using System.Globalization;
using ServerLoads.Models;

namespace ServerLoads.Controllers
{
    public class ServerLoadsController : ApiController
    {
        DateTime _cutTime;
        public DateTime CutTime { get { return _cutTime; } }
        public ServerLoadsController()
        {
            _cutTime = DateTime.Now;
        }

        public ServerLoadsController(DateTime time)
        {
            _cutTime = time;
        }

        [HttpPost]
        [Route("load")]
        public IHttpActionResult StoreLoad(DataPoint DataPoint)
        {
            if (string.IsNullOrEmpty(DataPoint.ServerName) || DataPoint.CPU == -1 || DataPoint.RAM == -1)
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("input data not correct"),
                    ReasonPhrase = "wrong input"
                });

            DataPointsStore.Store.Enqueue(DataPoint);
            return Ok("DataPoint Saved");
        }

        [HttpGet]
        [Route("averageload")]
        public IHttpActionResult LoadAverages(BreakDownType ByHourMinute, string ServerName)
        {
            DateTime currTime = CutTime;

            if (string.IsNullOrEmpty(ServerName))
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("ServerName is Empty"),
                    ReasonPhrase = "ServerName Required"
                });
            }
            AverageLoads computedLoads = null;
            if (ByHourMinute == BreakDownType.ByMinute)
            {
                DateTime endTime1 = DateTime.Parse(currTime.ToString("g")); //general date short time(without second part, like '8/29/2016 5:33PM')
                DateTime startTime1 = endTime1.AddMinutes(-60);
                computedLoads = new AverageLoads
                {
                    ServerName = ServerName,
                    LoadType = BreakDownType.ByMinute,
                    Loads = DataPointsStore.Store.Where(dp => dp.ServerName == ServerName && dp.Time < endTime1 && dp.Time >= startTime1)
                                    .GroupBy(dp => dp.Time.Minute)
                                    .Select(grp => new LoadDetail
                                    {
                                        Time = grp.First().Time.ToString("g"),   //become 8/29/2016 5:33 PM
                                        CPULoad = (grp.Sum(dp => dp.CPU) / grp.Count()).ToString("#.##"),
                                        RAMLoad = (grp.Sum(dp => dp.RAM) / grp.Count()).ToString("#.##")
                                    })
                };
            }
            else if(ByHourMinute == BreakDownType.ByHour)
            {
                DateTime endTime2 = DateTime.Parse(currTime.ToString("d") + " " + currTime.ToString("hh:00:00 tt"));
                DateTime startTime2 = endTime2.AddHours(-24);
                computedLoads = new AverageLoads
                {
                    ServerName = ServerName,
                    LoadType = BreakDownType.ByHour,
                    Loads = DataPointsStore.Store.Where(dp => dp.ServerName == ServerName && dp.Time < endTime2 && dp.Time >= startTime2)
                                .GroupBy(dp => dp.Time.Hour)
                                .Select(grp => new LoadDetail
                                {
                                    Time = grp.First().Time.ToString("d") + " " + grp.First().Time.Hour + ":00h",
                                    CPULoad = (grp.Sum(dp => dp.CPU) / grp.Count()).ToString("#.##"),
                                    RAMLoad = (grp.Sum(dp => dp.RAM) / grp.Count()).ToString("#.##")
                                })
                };

            }

            if(computedLoads.Loads.Count() == 0)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "server targed not found"));
            }
            else 
            {
                return ResponseMessage(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ObjectContent(computedLoads.GetType(), computedLoads, new JsonMediaTypeFormatter())
                });
            }

        }

    }

}
