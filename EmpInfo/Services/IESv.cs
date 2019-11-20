using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.Util;
using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;

namespace EmpInfo.Services
{
    public class IESv:BillSv,IBeginAuditOtherInfo
    {
        ei_ieApply bill;

        public IESv() { }
        public IESv(string sysNo)
        {
            bill = db.ei_ieApply.Single(e => e.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "IE"; }
        }

        public override string BillTypeName
        {
            get { return "IE立项结项申请"; }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            throw new NotImplementedException();
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);

            if (HasGotPower("IEBusAndAuditor", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "基础资料维护",
                    iconFont = "fa-gear",
                    url = "../ApplyExtra/IEBusAndAuditor"
                });
            }

            return menus;
        }

        public override string AuditViewName()
        {
            return "BeginAuditIEApply";
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

        public List<ei_ieAuditors> GetIEAuditorList()
        {
            return db.ei_ieAuditors.ToList();
        }

        public void SaveIEAuditor(int id,string bus_name,string dep_name,string minister,string ieLeader)
        {
            ei_ieAuditors ia;
            //新增
            if (id == 0) {
                if (db.ei_ieAuditors.Where(a => a.bus_name == bus_name).Count() > 0) {
                    throw new Exception("此事业部已存在，不能重复新增");
                }
                ia = new ei_ieAuditors();
                ia.bus_name = bus_name;
                db.ei_ieAuditors.Add(ia);
            }
            else {
                ia = db.ei_ieAuditors.SingleOrDefault(a => a.id == id);
                if (ia == null) {
                    throw new Exception("此行不存在，请刷新后再试");
                }
            }
            ia.dep_names = dep_name;
            ia.bus_minister_name = GetUserNameByNameAndCardNum(minister);
            ia.bus_minister_no = GetUserCardByNameAndCardNum(minister);
            ia.ie_leader_name = GetUserNameByNameAndCardNum(ieLeader);
            ia.ie_leader_no = GetUserCardByNameAndCardNum(ieLeader);

            db.SaveChanges();
        }

        public void ToggleIEAuditor(int id)
        {
            var ia = db.ei_ieAuditors.SingleOrDefault(a => a.id == id);
            if (ia == null) throw new Exception("此行不存在，请刷新后再试");

            ia.is_forbit = !ia.is_forbit;
            db.SaveChanges();
        }

    }
}