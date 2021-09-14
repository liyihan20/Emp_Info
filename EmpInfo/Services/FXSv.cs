using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using Newtonsoft.Json;
using EmpInfo.QywxWebSrv;

namespace EmpInfo.Services
{
    /// <summary>
    /// 新放行条流程，整合自提与非自提，2021-09
    /// </summary>
    public class FXSv:BillSv
    {
        public override string BillType
        {
            get { return "FX"; }
        }

        public override string BillTypeName
        {
            get { return "放行条申请单"; }
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            var types = db.ei_fxType.Where(f => !f.is_deleted).OrderBy(f => f.type_no).ToList();
            
            return JsonConvert.SerializeObject(types);
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

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            throw new NotImplementedException();
        }
    }
}