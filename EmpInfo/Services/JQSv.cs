using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.Interfaces;
using EmpInfo.EmpWebSvr;
using EmpInfo.Util;
using EmpInfo.FlowSvr;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class JQSv:BillSv,IBeginAuditOtherInfo
    {
        ei_jqApply bill;

        public JQSv() { }
        public JQSv(string sysNo)
        {
            bill = db.ei_jqApply.Single(j => j.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "JQ"; }
        }

        public override string BillTypeName
        {
            get { return "员工辞职/自离流程"; }
        }

        public override List<ApplyNavigatorModel> GetApplyNavigatorLinks()
        {
            var list = base.GetApplyNavigatorLinks();
            list.Add(
                new ApplyNavigatorModel()
                {
                    text = "无纸化流程",
                    url = "Home/NoPaperProcess"
                }
            );
            return list;
        }
                
        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            //请假流程的统计员权限就能看离职报表
            if (db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo)).Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/JQReport"
                });
            }

            return menus;
        }

        public override string AuditViewName()
        {
            return "BeginAuditJQApply";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return new JQBeforeApplyModel() { applierNum = userInfo.cardNo, sysNum = GetNextSysNum(BillType) };
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_jqApply();
            MyUtils.SetFieldValueToModel(fc, bill);

            if (!bill.dep_name.Contains("CCM")) throw new Exception("当前是测试阶段，只有CCM的才能申请，正式运行后会通知");

            if (bill.quit_reason != null && bill.quit_reason.Length > 1000) throw new Exception("离职原因内容太多，请删减");
            if (bill.quit_suggestion != null && bill.quit_suggestion.Length > 1000) throw new Exception("离职建议内容太多，请删减");

            if ("自离".Equals(bill.quit_type)) {
                if (bill.absent_from == null) throw new Exception("必须填写正确的旷工开始日期");
                if (bill.absent_to == null) throw new Exception("必须填写正确的旷工结束日期");
                if (bill.absent_days == null) throw new Exception("旷工天数必须是数字格式");
                if (userInfo.cardNo.Equals(bill.card_number)) throw new Exception("本人申请时离职类型不能选择自离");
            }
            else {
                if (!userInfo.cardNo.Equals(bill.card_number)) throw new Exception("离职类型为辞职时，必须是本人申请");
            }

            if ("计件".Equals(bill.salary_type)) {
                if (string.IsNullOrWhiteSpace(bill.group_leader_name)) throw new Exception("必须选择组长");
                if (string.IsNullOrWhiteSpace(bill.charger_name)) throw new Exception("必须选择主管");
                if (string.IsNullOrWhiteSpace(bill.produce_minister_name)) throw new Exception("必须选择生产部长");

                bill.group_leader_num = GetUserCardByNameAndCardNum(bill.group_leader_name);
                bill.charger_num = GetUserCardByNameAndCardNum(bill.charger_name);
                bill.produce_minister_num = GetUserCardByNameAndCardNum(bill.produce_minister_name);
            }
            else if ("月薪".Equals(bill.salary_type)) {
                if (string.IsNullOrWhiteSpace(bill.dep_charger_name)) throw new Exception("必须选择部门负责人");
                if (string.IsNullOrWhiteSpace(bill.highest_charger_name)) throw new Exception("必须最高负责人");

                bill.dep_charger_num = GetUserCardByNameAndCardNum(bill.dep_charger_name);
                bill.highest_charger_num = GetUserCardByNameAndCardNum(bill.highest_charger_name);
            }
            else {
                throw new Exception("工资类型只有计件或月薪的才能申请此流程");
            }

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.dep_name, bill.name + bill.quit_type + "申请");
            if (result.suc) {
                try {
                    db.ei_jqApply.Add(bill);
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

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            var jq = db.ei_jqApply.SingleOrDefault(j => j.sys_no == sysNo);
            if (jq == null) throw new Exception("单据不存在");
            return new JQAuditOtherInfoModel()
            {
                work_evaluation = jq.work_evaluation,
                work_comment = jq.work_comment,
                wanna_employ = jq.wanna_employ,
                employ_comment = jq.employ_comment,
                quit_type = jq.quit_type
            };
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string leaveDate = fc.Get("leave_date");
            DateTime leaveDateDt;
            if (isPass && bill.quit_type == "辞职") {
                if (!DateTime.TryParse(leaveDate, out leaveDateDt)) {
                    return new SimpleResultModel() { suc = false, msg = "批准离职时间必须填写" };
                }
                else {
                    bill.leave_date = leaveDateDt;
                }
            }

            bill.work_evaluation = fc.Get("work_evaluation");
            bill.work_comment = fc.Get("work_comment");
            bill.wanna_employ = fc.Get("wanna_employ");
            bill.employ_comment = fc.Get("employ_comment");

            if (bill.work_comment.Length > 500) return new SimpleResultModel() { suc = false, msg = "工作评价描述内容长度太多，请删减" };
            if (bill.employ_comment.Length > 500) return new SimpleResultModel() { suc = false, msg = "是否再录用描述内容长度太多，请删减" };
                        

            string formJson = JsonConvert.SerializeObject(bill);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, "", formJson);
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
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
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("[{2}]{0}的{1}申请", bill.name,bill.quit_type,bill.dep_name),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }
    }
}