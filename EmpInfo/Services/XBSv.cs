using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    /// <summary>
    /// 设备类申请单流程
    /// </summary>
    public class XBSv:BillSv,IBeginAuditOtherInfo
    {
        ei_xbApply bill;
        public XBSv() { }
        public XBSv(string sysNo)
        {
            bill = db.ei_xbApply.Single(x => x.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "XB"; }
        }

        public override string BillTypeName
        {
            get { return "设备类申请单"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditXBApply";
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            var auth = db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_value == userInfo.cardNo).ToList();

            if (auth.Where(a => a.relate_type == "查询报表").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/XBReport"
                });
            }            

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            XBBeforeApplyModel m = new XBBeforeApplyModel();
            m.sys_no = GetNextSysNum(BillType);
            m.applierName = userInfo.name;
            m.depNameList = db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "部门总经理").Select(f => f.relate_text).OrderBy(f => f).Distinct().ToList();

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_xbApply>(fc.Get("head"));
            if (bill.has_profit && !string.IsNullOrEmpty(bill.profit_confirm_people_name)) {
                bill.profit_confirm_people_num = GetUserCardByNameAndCardNum(bill.profit_confirm_people_name);
            }
            else {
                bill.profit_confirm_people_num = "";
            }
            bill.dept_charger_num = GetUserCardByNameAndCardNum(bill.dept_charger_name);
            if (!string.IsNullOrEmpty(bill.equitment_auditor_name)) {
                bill.equitment_auditor_num = GetUserCardByNameAndCardNum(bill.equitment_auditor_name);
            }
            else {
                bill.equitment_auditor_num = "";
            }            
            
            bill.applier_num = userInfo.cardNo;
            bill.applier_name = userInfo.name;
            bill.apply_time = DateTime.Now;
            bill.bill_no = GetNextSysNum(bill.dept_name + "-");

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.deal_type, bill.property_name);
            if (result.suc) {
                try {
                    db.ei_xbApply.Add(bill);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }

                //发送通知邮件到下一环节
                SendNotification(result);
            }
            else {
                throw new Exception("申请提交失败，原因：" + result.msg);
            }
        }

        public override object GetBill()
        {
            XBCheckApplyModel m = new XBCheckApplyModel();
            m.bill = bill;
            m.suppliers = db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no).ToList();
            return m;
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new XBSv(sysNo).GetBill();
        }
        
        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            bool returnBack = bool.Parse(fc.Get("returnBack"));//退回上一步
            string opinion = fc.Get("opinion");

            if (isPass && !returnBack) {
                if (stepName.Contains("设备管理部审批") && bill.deal_type.Equals("设备利旧")) {
                    bool hasMatch;
                    if (!bool.TryParse(fc.Get("has_match_equitment"), out hasMatch)) {
                        throw new Exception("请选择有无闲置的匹配设备");
                    }
                    bill.has_match_equitment = hasMatch;

                    if (hasMatch) {
                        bill.match_equitment_modual = fc.Get("match_equitment_modual");
                        bill.match_equitment_output = fc.Get("match_equitment_output");
                        bill.match_equitment_period = fc.Get("match_equitment_period");
                    }
                    else {
                        bill.match_equitment_none_reason = fc.Get("match_equitment_none_reason");
                    }

                }
                if (stepName.Contains("采购部接单")) {
                    if (db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no).Count() < 1) {
                        throw new Exception("请先录入供应商");
                    }
                }
                if (stepName.Contains("采购部报价")) {
                    //如果之前已经有选的，先清空
                    var bidderBefore = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.is_bidder == true).FirstOrDefault();
                    if (bidderBefore != null) bidderBefore.is_bidder = false;

                    var bidder = db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no && x.price != null && x.price > 0).OrderBy(x => x.price).FirstOrDefault();
                    if (bidder==null) {
                        throw new Exception("请至少录入一个供应商的价格再审批");
                    }
                    bidder.is_bidder = true;
                }
                if (stepName.Contains("审核部最终确认")) {
                    bill.can_print = true;  
                }
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;
            if (returnBack) {
                result = flow.ToPreStep(bill.sys_no, step,stepName, userInfo.cardNo, opinion);
            }
            else {
                string formJson = JsonConvert.SerializeObject(bill);
                result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            }
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }

            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            //if (model.suc) {
            //    if (model.msg.Contains("完成") || model.msg.Contains("成功中止")) {
            //        bool isSuc = model.msg.Contains("NG") ? false : true;
            //        string ccEmails = "";

            //        SendEmailForCompleted(
            //            bill.sys_no,
            //            BillTypeName + "已" + (isSuc ? "批准" : "被撤销"),
            //            bill.applier_name,
            //            string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被撤销")),
            //            GetUserEmailByCardNum(bill.applier_num),
            //            ccEmails
            //            );
            //        SendQywxMessageForCompleted(
            //            BillTypeName,
            //            bill.sys_no,
            //            (isSuc ? "批准" : "被撤销"),
            //            new List<string>() { bill.applier_num }
            //            );
            //    }
            //    else {
            //        string isReturn = "";
            //        if (model.msg.Contains("退回")) {
            //            isReturn = "【下一处理人退回】";
            //        }
            //        FlowSvrSoapClient flow = new FlowSvrSoapClient();
            //        var result = flow.GetCurrentStep(bill.sys_no);

            //        SendEmailToNextAuditor(
            //            bill.sys_no + isReturn,
            //            result.step,
            //            string.Format("你有一张待审批的{0}", BillTypeName),
            //            GetUserNameByCardNum(model.nextAuditors),
            //            string.Format("你有一张待处理的单号为【{0}】的{1},处理类别：{1}；设备名称：{2}，请尽快登陆系统处理。", bill.sys_no, BillTypeName, bill.deal_type, bill.property_name),
            //            GetUserEmailByCardNum(model.nextAuditors)
            //            );

            //        string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            //        SendQywxMessageToNextAuditor(
            //            BillTypeName + isReturn,
            //            bill.sys_no,
            //            result.step,
            //            result.stepName,
            //            bill.applier_name,
            //            ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
            //            string.Format("处理类别：{0}；设备名称：{1}", bill.deal_type, bill.property_name),
            //            nextAuditors.ToList()
            //            );
            //    }
            //}
        }
    }
}