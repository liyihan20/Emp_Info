using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class QywxJsConfigParam
    {
        public string beta { get { return "true"; } }
        public string appId { get; set; }
        public string timestamp { get; set; }
        public string nonceStr { get; set; }
        public string signature { get; set; }
        public string debug { get; set; }
        public string actionType { get; set; }
    }
}