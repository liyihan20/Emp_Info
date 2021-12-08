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

    public class POInfoModel
    {
        public int? entry_id { get; set; }
        public string item_no { get; set; }
        public string item_name { get; set; }
        public string item_modual { get; set; }
        public int? item_id { get; set; }
        public decimal? qty { get; set; }
        public string unit_name { get; set; }
        public string usage { get; set; }
        public DateTime? latest_arrive_date { get; set; }
        public string brand { get; set; }
    }

    public class K3Product
    {
        public int item_id { get; set; }
        public string item_no { get; set; }
        public string item_name { get; set; }
        public string item_model { get; set; }
        public string unit_name { get; set; }
    }
    public class K3BomInfo
    {
        public string sour { get; set; }
        public int item_id { get; set; }
        public string item_no { get; set; }
        public string item_name { get; set; }
        public string item_model { get; set; }
        public string unit_name { get; set; }
        public decimal per_qty { get; set; }
    }

    public class K3OutStock
    {
        public string item_no { get; set; }
        public string item_name { get; set; }
        public string item_model { get; set; }
        public decimal item_qty { get; set; }
        public string item_unit { get; set; }
    }

}