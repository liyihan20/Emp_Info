using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using EmpInfo.Util;
using Newtonsoft.Json;
using EmpInfo.Interfaces;

namespace EmpInfo.Services
{
    public class DESv:BillSv,IBeginAuditOtherInfo
    {
        private ei_DEApply bill;

        public DESv() { }
        public DESv(string sysNo)
        {
            bill = db.ei_DEApply.Single(d => d.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "DE"; }
        }

        public override string BillTypeName
        {
            get { return "后勤工程费用支出申请"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);            
            menus.Add(new ApplyMenuItemModel()
            {
                text = "项目名称维护",
                iconFont = "fa-file-text-o",
                url = "../ApplyExtra/DENames"
            });

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return new DEBeforeApplyModel()
            {
                sys_no = GetNextSysNum(BillType),
                subjects = db.ei_DESubjects.ToList(),
                names = db.ei_DENames.ToList(),
                applier_name = userInfo.name
            };
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_DEApply>(fc.Get("obj"));
            bill.applier_num = userInfo.cardNo;
            bill.applier_name = userInfo.name;
            bill.tax_rate = 9;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.subject, bill.name);
            if (result.suc) {
                try {
                    db.ei_DEApply.Add(bill);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }

                SendNotification(result);
            }
            else {
                throw new Exception("申请提交失败，原因：" + result.msg);
            }

        }

        public override object GetBill()
        {
            return bill;
        }

        public override string AuditViewName()
        {
            return "BeginAuditDEApply";
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            bool isBack = bool.Parse(fc.Get("isBack"));

            if (stepName.Contains("申请人") && isPass) {
                MyUtils.SetFieldValueToModel(fc, bill);
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result = null;
            if (isBack) {
                string preStepName;
                if (stepName == "后勤经理审批") {
                    preStepName = "申请人";
                }
                else if (stepName == "后勤经理确认") {
                    preStepName = "申请人录入补充信息";
                }
                else {
                    return new SimpleResultModel(false, "不能退回上一步，当前步骤："+stepName);
                }
                result = flow.ReturnToBeforeStep(userInfo.cardNo, bill.sys_no, stepName, preStepName);
                if (result.suc) {
                    result.msg = "成功退回上一步";
                    SendNotification(result);
                }
            }
            else {
                result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, "", "{}");
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    SendNotification(result);
                }
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
                        pushUsers
                        );
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    SendEmailToNextAuditor(
                        bill.sys_no,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no, BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    SendWxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.bill_date).ToString("yyyy-MM-dd"),
                        string.Format("{0}:{1}", bill.subject,bill.name),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            var info = new DEAuditOtherInfoModel();
            bill = db.ei_DEApply.Where(d => d.sys_no == sysNo).FirstOrDefault();
            if (bill == null) {
                return null;
            }
            if (bill.clear_date == null) {
                bill.clear_date = DateTime.Now;
            }
            info.billJson = JsonConvert.SerializeObject(bill);
            info.subjects = db.ei_DESubjects.ToList();
            info.names = db.ei_DENames.ToList();
            
            return info;
        }
    }
}