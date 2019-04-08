using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    public class SVSv:BillSv,IApplyEntryQueue
    {
        ei_SVApply bill;
        public SVSv() { }
        public SVSv(string sysNo)
        {
            bill = db.ei_SVApply.Single(s => s.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "SV"; }
        }

        public override string BillTypeName
        {
            get { return "调休申请单"; }
        }

        public override List<ApplyNavigatorModel> GetApplyNavigatorLinks()
        {
            var list = base.GetApplyNavigatorLinks();
            list.Add(
                new ApplyNavigatorModel()
                {
                    text = "电子公司专用流程",
                    url = "Home/EleProcess"
                }
            );
            return list;
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var list = base.GetApplyMenuItems(userInfo);
            if (db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo)).Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "调休申请报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/SVReport",
                    linkClass = "screen_900"
                });
            }
            return list;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            CRSVBeforeApplyModel m = new CRSVBeforeApplyModel();
            var appliedBills = db.ei_askLeave.Where(a => a.applier_num == userInfo.cardNo).ToList();
            if (appliedBills.Count() > 0) {
                var ab = appliedBills.OrderByDescending(a => a.id).First();
                if (ab.dep_long_name.Equals(GetDepLongNameByNum(ab.dep_no))) {
                    m.depName = ab.dep_long_name;
                    m.depNum = ab.dep_no;
                    m.depId = ab.dep_id;
                }
            }
            m.sysNum = GetNextSysNum(BillType);
            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_SVApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            //处理一下审核队列,将姓名（厂牌）格式更换为厂牌
            if (!string.IsNullOrEmpty(bill.auditor_queues)) {
                var queueList = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(bill.auditor_queues);
                queueList.ForEach(q => q.auditors = GetUserCardByNameAndCardNum(q.auditors));
                if (queueList.Where(q => q.auditors.Contains("离职")).Count() > 0) {
                    throw new Exception("流程审核人中存在已离职的员工，请联系部门管理员处理");
                }
                bill.auditor_queues = JsonConvert.SerializeObject(queueList);
            }


            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, string.Format("值班时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}", bill.duty_date_from, bill.duty_date_to), string.Format("调休时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}", bill.vacation_date_from, bill.vacation_date_to));
            if (result.suc) {
                try {
                    db.ei_SVApply.Add(bill);
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
                throw new Exception("申请提交失败：" + result.msg);
            }
        }

        public override object GetBill()
        {
            return bill;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            var sv = db.ei_SVApply.Single(a => a.sys_no == bill.sys_no);
            string formJson = JsonConvert.SerializeObject(sv);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                SendNotification(result);
            }
            
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num)
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
                        db.vw_push_users.Where(v => v.card_number == bill.applier_num).FirstOrDefault()
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
                        string.Format("调休时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}；值班时间：{2:yyyy-MM-dd HH:mm}~{3:yyyy-MM-dd HH:mm}", bill.vacation_date_from, bill.vacation_date_to,bill.duty_date_from,bill.duty_date_to),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public List<flow_applyEntryQueue> GetApplyEntryQueue(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_SVApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            if (!bill.dep_no.StartsWith("104")) {
                throw new Exception("此流程的部门只支持选择信利电子子部门");
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(bill), BillType);
            List<flow_applyEntryQueue> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(result.msg);
                queue.ForEach(q => q.auditors = GetUserNameAndCardByCardNum(q.auditors));
                return queue;
            }
            else {
                throw new Exception(result.msg);
            }
        }
    }
}