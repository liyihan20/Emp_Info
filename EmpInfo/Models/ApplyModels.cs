using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class AuditModel
    {
        public bool isAuditing { get;set; }
        public int step { get; set; }
        public string stepName { get; set; }
        public string cardNumber { get; set; }
        public string userName { get; set; }
    }
}