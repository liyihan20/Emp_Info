using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.Interfaces;
using EmpInfo.EmpWebSvr;

namespace EmpInfo.Services
{
    public class JQSv:BillSv
    {
        ei_jqApply bill;
        public override string BillType
        {
            get { return "JQ"; }
        }

        public override string BillTypeName
        {
            get { return "员工辞职/自离流程"; }
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return userInfo.cardNo;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            throw new NotImplementedException();
        }

        public override object GetBill()
        {
            return bill;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            throw new NotImplementedException();
        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            throw new NotImplementedException();
        }
    }
}