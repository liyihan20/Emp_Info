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

    public class DormReportModel
    {
        public string charge_type { get; set; }
        public decimal type_sum { get; set; }
    }

    public class SimpleResultModel
    {
        public SimpleResultModel(){}
        public SimpleResultModel(bool _suc)
        {
            this.suc=_suc;
        }
        public SimpleResultModel(bool _suc, string _msg)
        {
            this.suc = _suc;
            this.msg = _msg;
        }
        public SimpleResultModel(bool _suc, string _msg,string _extra)
        {
            this.suc = _suc;
            this.msg = _msg;
            this.extra = _extra;
        }
        public SimpleResultModel(Exception ex)
        {
            this.suc = false;
            this.msg = ex.Message;
        }

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