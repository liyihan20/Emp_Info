using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class SelectModel
    {
        public string text { get; set; }
        public int intValue { get; set; }
        public string stringValue { get; set; }

    }

    public class SimpleResultModel
    {
        public bool suc { get; set; }
        public string msg { get; set; }
        public string extra { get; set; }
    }

    public class RedirectModel
    {
        public string actionName { get; set; }
        public string controllerName { get; set; }
        public object routetValues { get; set; }
    }

}