using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpInfo.Models
{
    public class DormLivingInfo
    {
        public string livingStatus { get; set; }
        public string inDate { get; set; }
        public string areaNumber { get; set; }
        public string dormNumber { get; set; }
    }

    public class DormFeeModel
    {
        public string yearMonth { get; set; }
        public string dormNumber { get; set; }
        public string rent { get; set; }
        public string management { get; set; }
        public string elec { get; set; }
        public string water { get; set; }
        public string repair { get; set; }
        public string fine { get; set; }
        public string others { get; set; }
        public string comment { get; set; }
        public string total { get; set; }
    }

    public class DormRepairBeginApplyModel
    {
        public string sysNo { get; set; }
        public string areaName { get; set; }
        public string dormNumber { get; set; }
        public string roomMaleList { get; set; }
        public string phoneNumber { get; set; }
        public string shortPhoneNumber { get; set; }
        public string contactName { get; set; }
    }

    

}