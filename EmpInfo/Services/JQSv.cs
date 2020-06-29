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
using System.Configuration;

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
            get { return "员工辞职/自离申请单"; }
        }
                
        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            //离职报表权限
            if (db.ei_flowAuthority.Where(f=>f.bill_type==BillType && f.relate_type=="查询报表" && f.relate_value==userInfo.cardNo).Count()>0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/JQReport"
                });
            }

            var fas = db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_value == userInfo.cardNo).ToList();

            //主管可修改离职日期
            if (fas.Where(f => f.relate_type == "修改离职日期").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "修改离职日期",
                    iconFont = "fa-edit",
                    url = "../ApplyExtra/ChargerUpdateLeaveDay"
                });
            }

            //AH部作废单据
            if (fas.Where(f => f.relate_type == "作废单据").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "作废离职申请",
                    iconFont = "fa-ban",
                    url = "../ApplyExtra/CancelJQApply"
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

            if (bill.quit_reason != null && bill.quit_reason.Length > 1000) throw new Exception("离职原因内容太多，请删减");
            if (bill.quit_suggestion != null && bill.quit_suggestion.Length > 1000) throw new Exception("离职建议内容太多，请删减");
            if (bill.dep_name == null) throw new Exception("离职人的部门不能为空");

            if ("自离".Equals(bill.quit_type)) {
                if (bill.absent_from == null) throw new Exception("必须填写正确的旷工开始日期");
                if (bill.absent_to == null) throw new Exception("必须填写正确的旷工结束日期");
                if (bill.absent_days == null) throw new Exception("旷工天数必须是数字格式");
                if (string.IsNullOrWhiteSpace(bill.auto_quit_comment)) throw new Exception("自离补充说明不能为空");
                if (userInfo.cardNo.Trim().Equals(bill.card_number.Trim())) throw new Exception("本人申请时离职类型不能选择自离");
            }
            else {
                if (!userInfo.cardNo.Trim().Equals(bill.card_number.Trim())) throw new Exception("离职类型为辞职时，必须是本人申请");
            }

            if ("计件".Equals(bill.salary_type)) {
                if (string.IsNullOrWhiteSpace(bill.group_leader_name)) throw new Exception("必须选择组长");
                bill.group_leader_num = GetUserCardByNameAndCardNum(bill.group_leader_name);                                
            }
            else if ("月薪".Equals(bill.salary_type)) {
                if (string.IsNullOrWhiteSpace(bill.dep_charger_name)) throw new Exception("必须选择部门负责人");

                bill.dep_charger_num = GetUserCardByNameAndCardNum(bill.dep_charger_name);
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
            return jq;
            //return new JQAuditOtherInfoModel()
            //{
            //    work_evaluation = jq.work_evaluation,
            //    work_comment = jq.work_comment,
            //    wanna_employ = jq.wanna_employ,
            //    employ_comment = jq.employ_comment,
            //    quit_type = jq.quit_type,
            //    leave_date = jq.leave_date == null ? "" : ((DateTime)jq.leave_date).ToString("yyyy-MM-dd")
            //};
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            MyUtils.SetFieldValueToModel(fc, bill);

            if (isPass) {
                if (bill.leave_date == null) return new SimpleResultModel() { suc = false, msg = "离职时间必须填写" };
                if (string.IsNullOrEmpty(bill.work_evaluation)) return new SimpleResultModel() { suc = false, msg = "工作评价必须选择" };
                if (string.IsNullOrEmpty(bill.wanna_employ)) return new SimpleResultModel() { suc = false, msg = "是否再录用必须选择" };
                if (stepName.Contains("组长")) {
                    if (string.IsNullOrEmpty(bill.charger_name)) return new SimpleResultModel() { suc = false, msg = "请选择主管审核人" };
                    if(string.IsNullOrEmpty(bill.charger_num)) bill.charger_num = GetUserCardByNameAndCardNum(bill.charger_name);
                }
                if (stepName.Contains("主管")) {
                    if (string.IsNullOrEmpty(bill.produce_minister_name)) return new SimpleResultModel() { suc = false, msg = "请选择生产部长审核人" };
                    if (string.IsNullOrEmpty(bill.produce_minister_num)) bill.produce_minister_num = GetUserCardByNameAndCardNum(bill.produce_minister_name);
                    //加入到主管修改离职日期的表
                    if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "修改离职日期" && f.relate_value == userInfo.cardNo).Count() < 1) {
                        db.ei_flowAuthority.Add(new ei_flowAuthority()
                        {
                            bill_type = BillType,
                            relate_type = "修改离职日期",
                            relate_value = userInfo.cardNo
                        });
                    }
                }
                if (stepName.Contains("部门负责人")) {
                    if (string.IsNullOrEmpty(bill.highest_charger_name)) return new SimpleResultModel() { suc = false, msg = "请选择部门最高审核人" };
                    if (string.IsNullOrEmpty(bill.highest_charger_num)) bill.highest_charger_num = GetUserCardByNameAndCardNum(bill.highest_charger_name);
                    //部门负责人加入修改离职日期的表
                    if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "修改离职日期" && f.relate_value == userInfo.cardNo).Count() < 1) {
                        db.ei_flowAuthority.Add(new ei_flowAuthority()
                        {
                            bill_type = BillType,
                            relate_type = "修改离职日期",
                            relate_value = userInfo.cardNo
                        });
                    }
                }
            }

            if (bill.work_comment != null && bill.work_comment.Length > 500) return new SimpleResultModel() { suc = false, msg = "工作评价描述内容长度太多，请删减" };
            if (bill.employ_comment != null && bill.employ_comment.Length > 500) return new SimpleResultModel() { suc = false, msg = "是否再录用描述内容长度太多，请删减" };

            string formJson = JsonConvert.SerializeObject(bill);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        /// <summary>
        /// 获取离职申请内容，可通过流水号或厂牌
        /// </summary>
        /// <param name="searchContent"></param>
        /// <returns></returns>
        public ei_jqApply GetJQApply(string searchContent)
        {
            ei_jqApply jq;
            if (searchContent.StartsWith("JQ")) {
                jq = db.ei_jqApply.Where(j => j.sys_no == searchContent).FirstOrDefault();
            }
            else {
                jq = db.ei_jqApply.Where(j => j.card_number == searchContent).OrderByDescending(j => j.id).FirstOrDefault();
            }

            return jq;
        }

        /// <summary>
        /// 主管可以修改离职日期
        /// </summary>
        /// <param name="newDay"></param>
        public void UpdateLeaveDay(DateTime newDay,string notifyUsers,string cardNumber) {
            string adminNumber = ConfigurationManager.AppSettings["adminNumber"];
            if (!adminNumber.Equals(cardNumber)) {
                if ((bill.charger_num != null && !bill.charger_num.Contains(cardNumber)) || (bill.dep_charger_num != null && !bill.dep_charger_num.Contains(cardNumber))) {
                    throw new Exception("你没有修改此离职单日期的权限");
                }
            }
            var leaveDateBefore = bill.leave_date ?? DateTime.Parse("2019-10-1");
            if (leaveDateBefore.Equals(newDay)) return; //需修改的日期和之前日期一样，就不处理直接返回
            
            var notifyNames = GetUserNameByNameAndCardNum(notifyUsers);
            var notifyCardNumber = GetUserCardByNameAndCardNum(notifyUsers);
            var emails = GetUserEmailByCardNum(notifyCardNumber);

            if (string.IsNullOrWhiteSpace(emails)) {
                throw new Exception("选择的文员没有在E家登记邮箱，不能发送邮件通知。请先通知文员到此系统点击头像登记邮箱后再修改离职日期");
            }

            bill.leave_date = newDay;
            db.SaveChanges();

            //发送通知邮件给文员
            SendEmailForCompleted(
                bill.sys_no,
                "离职日期变更通知",
                notifyNames,
                string.Format("单号为【{0}】的{1}离职日期已被主管修改：离职人【{2}】,离职日期从【{3:yyyy-MM-dd}】变更为【{4:yyyy-MM-dd}】，请知悉。", bill.sys_no, BillTypeName,bill.name,leaveDateBefore,newDay),
                emails,
                GetUserEmailByCardNum(bill.applier_num)
             );
        }


        public void CancelApply(string cardNumber)
        {
            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "作废单据" && f.relate_value == cardNumber).Count() < 1) {
                throw new Exception("没有权限作废离职申请单");
            }
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.CancelFlowAfterFinish(bill.sys_no, cardNumber);
            if (!result.suc) throw new Exception(result.msg);
            
        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    //List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();                                       

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    //SendWxMessageForCompleted(
                    //    BillTypeName,
                    //    bill.sys_no,
                    //    (isSuc ? "批准" : "被拒绝"),
                    //    pushUsers
                    //    );
                    SendQywxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
                        new List<string>() { bill.applier_num }
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
                    //SendWxMessageToNextAuditor(
                    //    BillTypeName,
                    //    bill.sys_no,
                    //    result.step,
                    //    result.stepName,
                    //    bill.applier_name,
                    //    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                    //    string.Format("[{2}]{0}的{1}申请", bill.name,bill.quit_type,bill.dep_name),
                    //    db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                    //    );
                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("[{2}]{0}的{1}申请", bill.name, bill.quit_type, bill.dep_name),
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public void UpdateDepName(string newDepName)
        {
            bill.dep_name = newDepName;
            db.SaveChanges();
        }
    }
}