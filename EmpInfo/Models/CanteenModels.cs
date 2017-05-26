using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class DinningCardStatusModel
    {
        public string username { get; set; }
        public string cardNo { get; set; }
        public string lastConsumeTime { get; set; }
        public string status { get; set; }
        public decimal? remainingSum { get; set; }
    }

    public class DiningCardConsumeRecords
    {
        public string consumeTime { get; set; }
        public string place { get; set; }
        public string money { get; set; }
        public string diningType { get; set; }
    }

    public class DiningCardRechargeRecords
    {
        public string beforeSum { get; set; }
        public string rechargeSum { get; set; }
        public string afterSum { get; set; }
        public string place { get; set; }
        public string rechargeTime { get; set; }
    }

}