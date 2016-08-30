using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections.Concurrent;

namespace ServerLoads.Models
{
    public class DataPoint
    {   
        public DataPoint()
        {
            CPU = -1;
            RAM = -1;
            Time = DateTime.Now;
        }
        public string ServerName { get; set; }
        public double CPU { get; set; }
        public double RAM { get; set; }
        public DateTime Time { get; set; }
    }

    public static class DataPointsStore
    {   
        readonly static ConcurrentQueue<DataPoint> _store;
        static DataPointsStore()
        {
            _store = new ConcurrentQueue<DataPoint>();
        }
        public static ConcurrentQueue<DataPoint> Store { get { return _store; } }
    }

    public enum BreakDownType{
        ByMinute,
        ByHour
    }
}