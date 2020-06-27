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
    /// <summary>
    /// 开源节流建议
    /// </summary>
    public class KSSv:BillSv,IBeginAuditOtherInfo
    {
        ei_ksApply bill;

        public KSSv(){}
        public KSSv(string sysNo)
        {
            bill = db.ei_ksApply.Single(k => k.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "KS"; }
        }

        public override string BillTypeName
        {
            get { return "开源节流建议申请"; }
        }

        public override bool CanAccessApply(UserInfo userInfo)
        {
            return db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Count() > 0;
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/KSReport"
                });
            }
            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            KSBeforeApplyModel m = new KSBeforeApplyModel();
            m.applier_number = userInfo.cardNo;
            m.applier_name = userInfo.name;
            m.dep_name = new HRDBSv().GetHREmpDetailInfo(userInfo.cardNo).dep_name;
            m.sys_no = GetNextSysNum(BillType);
            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_ksApply>(fc.Get("obj"));
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.dep_name, BillTypeName);
            if (result.suc) {
                try {
                    db.ei_ksApply.Add(bill);
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

        public override string AuditViewName()
        {
            return "BeginAuditKSApply";
        }

        public override object GetBill()
        {
            return bill;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));

            MyUtils.SetFieldValueToModel(fc, bill);

            if (stepName.Contains("营运部审批")) {
                if (isPass) {
                    if (bill.applier_reward == null) throw new Exception("请填写合法的建议采纳奖励");
                    if (string.IsNullOrWhiteSpace(bill.level_name)) throw new Exception("请选择评级");
                    if (bill.level_reward == null) throw new Exception("请填写合法的评级奖励金额");
                    if (string.IsNullOrWhiteSpace(bill.executor_name)) throw new Exception("同意时必须选择执行人");

                    bill.executor_number = GetUserCardByNameAndCardNum(bill.executor_name);
                    bill.executor_name = GetUserNameByNameAndCardNum(bill.executor_name);
                }
                else {
                    if (string.IsNullOrWhiteSpace(bill.operation_dep_opinion)) throw new Exception("拒绝时请在意见处写明拒绝原因");
                    bill.applier_reward = 0;
                }
            }
            else if (stepName.Contains("执行人处理")) {
                if (string.IsNullOrWhiteSpace(bill.group_members)) throw new Exception("请选择组员");
                if (string.IsNullOrWhiteSpace(bill.result_description)) throw new Exception("请填写成果说明");
                if (!bill.has_attachment) throw new Exception("请上传成果文件");
            }
            else if (stepName.Contains("营运部确认")) {
                if (string.IsNullOrWhiteSpace(bill.level_name)) throw new Exception("请选择评级");
                if (bill.level_reward == null) throw new Exception("请填写合法的评级奖励金额");
            }

            string formJson = JsonConvert.SerializeObject(bill);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, "", formJson);
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel(result.suc, result.msg);

        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_number).ToList();

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_number),
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
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("{0}的{1}", bill.applier_name, BillTypeName),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            var ks = db.ei_ksApply.Where(k=>k.sys_no==sysNo).Select(k=>new {k.level_name, k.level_reward}).FirstOrDefault();
            return new KSAuditOtherInfoModel()
            {
                level_name = ks.level_name ?? "",
                level_reward = ks.level_reward ?? 0
            };
        }
    }
}