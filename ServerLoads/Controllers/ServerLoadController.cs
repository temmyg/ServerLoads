using System;
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
        public IHttpActionResult LoadAverages(BreakDownType ByHourMinute)
        {
            DateTime currTime = DateTime.Now;

            //test data, please ignore!!
            //DataPointsStore.Store.Enqueue(new DataPoint { CPU = 12.4, RAM = 55.2, Time = new DateTime(2016, 8, 29, 17, 30, 42) });
            //DataPointsStore.Store.Enqueue(new DataPoint { CPU = 22.8, RAM = 11.4, Time = new DateTime(2016, 8, 29, 17, 30, 52) });
            //DataPointsStore.Store.Enqueue(new DataPoint { CPU = 11.8, RAM = 17.4, Time = new DateTime(2016, 8, 29, 17, 42, 52) });

            if (ByHourMinute == BreakDownType.ByMinute)
            {
                DateTime endTime1 = DateTime.Parse(currTime.ToString("g")); //general date short time(without second part, like '8/29/2016 5:33PM')
                DateTime startTime1 = endTime1.AddMinutes(-60);
                var minutesLoad = DataPointsStore.Store.Where(dp => dp.Time < endTime1 && dp.Time >= startTime1)
                                    .GroupBy(dp => dp.Time.Minute)
                                    .Select(grp => new
                                    {
                                        Time = grp.First().Time.ToString("g"),   //become 8/29/2016 5:33 PM
                                        CPULoad = (grp.Sum(dp => dp.CPU) / grp.Count()).ToString("#.##"),
                                        RAMLoad = (grp.Sum(dp => dp.RAM) / grp.Count()).ToString("#.##")
                                    });
                return ResponseMessage(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ObjectContent(minutesLoad.GetType(), minutesLoad, new JsonMediaTypeFormatter())
                });
            }
            else if (ByHourMinute == BreakDownType.ByHour)
            {
                DateTime endTime2 = DateTime.Parse(currTime.ToString("d") + " " + currTime.ToString("hh:00:00 tt"));
                DateTime startTime2 = endTime2.AddHours(-24);
                var hoursLoad = DataPointsStore.Store.Where(dp => dp.Time < endTime2 && dp.Time >= startTime2)
                                .GroupBy(dp => dp.Time.Hour)
                                .Select(grp => new
                                {
                                    Time = grp.First().Time.ToString("d") + " " + grp.First().Time.Hour + ":00h",
                                    CPULoad = (grp.Sum(dp => dp.CPU) / grp.Count()).ToString("#.##"),
                                    RAMLoad = (grp.Sum(dp => dp.RAM) / grp.Count()).ToString("#.##")

                                });
                return ResponseMessage(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ObjectContent(hoursLoad.GetType(), hoursLoad, new JsonMediaTypeFormatter())
                });
            }

            return StatusCode(HttpStatusCode.BadRequest);
        }

    }

}
