using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class fieldModel
    {
        public fieldModel() { }
        public fieldModel(string _filedName,string _fieldText, string _tip)
        {
            this.fieldType = "input";
            this.fieldName = _filedName;
            this.fieldText = _fieldText;
            this.tip = _tip;
            this.required = true;
        }
        public fieldModel(string _fieldType, string _filedName, string _fieldText, string _tip, bool _required = true)
        {
            this.fieldType = _fieldType;
            this.fieldText = _fieldText;
            this.fieldName = _filedName;
            this.tip = _tip;
            this.required = _required;
        }
        public string fieldType { get; set; }
        public string fieldText { get; set; }
        public string fieldName { get; set; }
        public string tip { get; set; }
        public bool required { get; set; }
    }
    public class SelectModel
    {
        public string text { get; set; }
        public int intValue { get; set; }
        public string stringValue { get; set; }
        public string extraValue { get; set; }
    }

    public class stringDecimalModel
    {
        public string name { get; set; }
        public decimal? value { get; set; }
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

    public class NV
    {
        public string n { get; set; }
        public string v { get; set; }
    }

}