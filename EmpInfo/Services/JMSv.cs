using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Interfaces;
using EmpInfo.FlowSvr;

namespace EmpInfo.Services
{
    /// <summary>
    /// 一个外壳，将新旧离职流程（JQ和MQ）封装在一起，方便用户操作，避免错乱。
    /// </summary>
    public class JMSv:BillSv,IRealBillType
    {
        public string sysNo;
        public JMSv() { }
        public JMSv(string sysNo)
        {
            this.sysNo = sysNo;
        }

        public override string BillType
        {
            get { return "JM"; }
        }

        public override string BillTypeName
        {
            get { return "员工离职申请单"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var salaryType = new HRDBSv().GetHREmpInfo(userInfo.cardNo).salary_type;
            var list = new List<ApplyMenuItemModel>();
            list.Add(new ApplyMenuItemModel()
            {
                url = "BeginApply?billType=" + ("月薪".Equals(salaryType) ? "JQ&quitType=1" : "MQ"),
                text = "辞职申请",
                iconFont = "fa-pencil",
                colorClass = "text-danger"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "BeginApply?billType=JQ&quitType=2",
                text = "自离申请",
                iconFont = "fa-pencil",
                colorClass = "text-warning"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyApplyList?billType=" + BillType,
                text = "我申请的",
                iconFont = "fa-th"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditingList?billType=" + BillType,
                text = "我的待办",
                iconFont = "fa-th-list"
            });
            list.Add(new ApplyMenuItemModel()
            {
                url = "GetMyAuditedList?billType=" + BillType,
                text = "我的已办",
                iconFont = "fa-th-large"
            });

            var fas = db.ei_flowAuthority.Where(f => f.bill_type == "JQ" && f.relate_value == userInfo.cardNo).ToList();

            //离职报表权限
            if (fas.Where(f => f.relate_type == "查询报表").Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/JMReport"
                });
            }

            //主管可修改离职日期
            if (fas.Where(f => f.relate_type == "修改离职日期").Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "修改离职日期",
                    iconFont = "fa-edit",
                    url = "../ApplyExtra/ChargerUpdateJMLeaveDay"
                });
            }

            //AH部作废单据
            if (fas.Where(f => f.relate_type == "作废单据").Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "作废离职申请",
                    iconFont = "fa-ban",
                    url = "../ApplyExtra/CancelJMApply"
                });
            }

            //人事面谈报表
            if (fas.Where(f => f.relate_type == "面谈报表").Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "人事面谈报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/MQHRTalkReport"
                });
            }

            return list;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return null;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            
        }

        public override object GetBill()
        {
            return null;
        }

        public override Models.SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            return null;
        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            
        }

        public ArrayOfString GetRealBillTypes()
        {
            return new ArrayOfString() { "JQ", "MQ" };
        }
    }
}