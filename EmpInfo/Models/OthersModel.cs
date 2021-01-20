using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class AssistantEmpModel
    {
        public string cardNumber { get; set; }
        public string empName { get; set; }
        public string empType { get; set; }
        public string depName { get; set; }
    }

    public class XAAuditorsModel
    {
        public int id { get; set; }
        public string company { get; set; }
        public string deptName { get; set; }
        public string position { get; set; }
        public string userName { get; set; }
        public string userNumber { get; set; }
    }
}