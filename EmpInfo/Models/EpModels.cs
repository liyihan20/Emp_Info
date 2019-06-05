using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class PrDepUserModel
    {
        public int id { get; set; }
        public string name { get; set; }
        public string number { get; set; }
        public string jobPosition { get; set; }
    }

    public class AuditTimeExceedModel
    {
        public string sysNo { get; set; }
        public string name { get; set; }
        public string depName { get; set; }
        public double exceedHours { get; set; }
        public DateTime? bTime { get; set; }
        public DateTime? eTime { get; set; }
    }
    
}