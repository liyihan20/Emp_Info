﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class DepModel
    {
        public int id { get; set; }
        public string depNo { get; set; }
        public string depName { get; set; }
    }

    public class ALSearchParam
    {
        public string depId { get; set; }
        public string depName { get; set; }
        public string fromDate { get; set; }
        public string toDate { get; set; }
        public string auditStatus { get; set; }
        public int empLeve { get; set; }
        public string empName { get; set; }
        public string sysNum { get; set; }
        public string salaryNo { get; set; }
    }

    public class CRSearchParam
    {
        public string depId { get; set; }
        public string depName { get; set; }
        public string fromDate { get; set; }
        public string toDate { get; set; }
        public string auditStatus { get; set; }
        public string empName { get; set; }
        public string sysNum { get; set; }
        public string salaryNo { get; set; }
    }

    public class SVSearchParam
    {
        public string depId { get; set; }
        public string depName { get; set; }
        public string vFromDate { get; set; }
        public string vToDate { get; set; }
        public string dFromDate { get; set; }
        public string dToDate { get; set; }
        public string auditStatus { get; set; }
        public string empName { get; set; }
        public string sysNum { get; set; }
        public string salaryNo { get; set; }
    }

    public class AssistantEmpModel
    {
        public string cardNumber { get; set; }
        public string empName { get; set; }
        public string empType { get; set; }
        public string depName { get; set; }
    }

    public class EPSearchParam{
        public string fromDate { get; set; }
        public string toDate { get; set; }
        public string applyStatus { get; set; }
        public string propertyNumber { get; set; }
        public string procDepName { get; set; }
        public string equitmentDepName { get; set; }
    }

}