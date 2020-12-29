using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    /// <summary>
    /// 暴露一些方法给其它不走流程的控制器使用
    /// </summary>
    public class ShareSv:BillSv
    {
        public override string BillType
        {
            get { throw new NotImplementedException(); }
        }

        public override string BillTypeName
        {
            get { throw new NotImplementedException(); }
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            throw new NotImplementedException();
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            throw new NotImplementedException();
        }

        public override object GetBill()
        {
            throw new NotImplementedException();
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            throw new NotImplementedException();
        }

        public override void SendNotification(FlowResultModel model)
        {
            throw new NotImplementedException();
        }

        public void SendQywxMsgToAuditors(string billTypeName,string sysNo,int step,string stepName,string applierName,DateTime applyTime,string applyContent,List<string> auditorList)
        {
            SendQywxMessageToNextAuditor(
                        billTypeName,
                        sysNo,
                        step,
                        stepName,
                        applierName,
                        applyTime.ToString("yyyy-MM-dd HH:mm"),
                        applyContent,
                        auditorList
                        );
        }

    }
}