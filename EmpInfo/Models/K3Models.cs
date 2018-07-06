using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class K3AccountModel
    {
        public string number { get; set; }
        public string name { get; set; }
    }

    public class StBillSearchParamModel
    {
        public string account { get; set; }
        public string fromDate { get; set; }
        public string toDate { get; set; }
    }

    public class StBillResultModel
    {
        public int stId { get; set; }
        public string stNumber { get; set; }
        public string stDate { get; set; }
        public string customerNumber { get; set; }
        public string customerName { get; set; }
        public string saleStyle { get; set; }
        public string srNumber { get; set; }
        public string entryJson { get; set; }
    }

}