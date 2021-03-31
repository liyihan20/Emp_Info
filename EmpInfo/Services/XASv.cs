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
    /// 项目单流程
    /// </summary>
    public class XASv:BillSv,IBeginAuditOtherInfo
    {
        ei_xaApply bill;
        public XASv() { }
        public XASv(string sysNo)
        {
            bill = db.ei_xaApply.Single(x => x.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "XA"; }
        }

        public override string BillTypeName
        {
            get { return "项目单申请流程"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditXAApply";
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
                    url = "../Report/XAReport"
                });
            }

            if (auth.Where(a => a.relate_type == "费用统计").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "费用统计",
                    iconFont = "fa-cny",
                    url = "../Report/XASummary"
                });
            }

            if (auth.Where(a => a.relate_type == "修改审核人").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "总经理与CEO",
                    iconFont = "fa-user",
                    url = "../ApplyExtra/XAAuditorSetting"
                });
            }

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            XABeforeApplyModel m = new XABeforeApplyModel();
            m.sys_no = GetNextSysNum(BillType);
            m.applierName = userInfo.name;
            //m.applierPhone = userInfoDetail.phone ?? "";
            m.depNameList = db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "部门总经理").Select(f => f.relate_text).OrderBy(f => f).Distinct().ToList();

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_xaApply>(fc.Get("head"));
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

            //增加部门分摊功能 2020-12-16
            if (bill.is_share_fee) {
                if (!string.IsNullOrEmpty(bill.share_fee_detail)) {
                    List<XAFeeShareModel> shares = JsonConvert.DeserializeObject<List<XAFeeShareModel>>(bill.share_fee_detail);
                    var depList = shares.Where(s => s.n != bill.dept_name).Select(s => s.n).Distinct().ToList();
                    bill.share_fee_managers = string.Join(";", db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "部门总经理" && depList.Contains(f.relate_text)).Select(f => f.relate_value).ToList());
                }
                else {
                    bill.is_share_fee = false;
                }
            }
            
            bill.applier_num = userInfo.cardNo;
            bill.applier_name = userInfo.name;
            bill.apply_time = DateTime.Now;
            bill.bill_no = GetNextSysNum(bill.dept_name + "-");

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.classification + "_" + bill.project_type, bill.dept_name + "_" + bill.project_name);
            if (result.suc) {
                try {
                    db.ei_xaApply.Add(bill);
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
            XACheckApplyModel m = new XACheckApplyModel();
            m.bill = bill;
            m.suppliers = db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no).ToList();
            return m;
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new XASv(sysNo).GetBill();
        }
        
        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            bool returnBack = bool.Parse(fc.Get("returnBack"));//退回上一步
            string opinion = fc.Get("opinion");
            string bidder = fc.Get("bidder");

            if (isPass && !returnBack) {
                if (stepName.Contains("采购部接单")) {
                    if (db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no).Count() < 1) {
                        throw new Exception("请先录入供应商");
                    }
                }
                if (stepName.Contains("上传报价单")) {
                    bool isPO;
                    if (!bool.TryParse(fc.Get("is_po"), out isPO)) {
                        throw new Exception("必须选择是否为PO单");
                    }
                    bill.is_po = isPO;
                    if (db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no && x.price != null).Count() < 1) {
                        throw new Exception("请至少录入一个供应商的价格再审批");
                    }

                    //如果之前已经有选的，先清空
                    var bidderBefore = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.is_bidder == true).FirstOrDefault();
                    if (bidderBefore != null) bidderBefore.is_bidder = false;
                }
                if (stepName.Contains("审核部最终确认")) {
                    //如果之前已经有选的，先清空
                    var bidderBefore = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.is_bidder == true).FirstOrDefault();
                    if(bidderBefore!=null)  bidderBefore.is_bidder = false;
                    
                    int bidderId = 0;
                    int.TryParse(bidder, out bidderId);
                    var bidderSupplier = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.id == bidderId).FirstOrDefault();
                    if (bidderSupplier == null) {
                        bidderSupplier = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.price != null && s.price > 0).OrderBy(s => s.price).FirstOrDefault();
                        if (bidderSupplier == null) {
                            throw new Exception("所有供应商都未录入价格，审批失败");
                        }
                    }
                    bidderSupplier.is_bidder = true;
                    bill.can_print = true;
                    bill.confirm_date = DateTime.Now;
                }
                if (stepName.Contains("申请人验收")) {
                    bill.check_date = DateTime.Now;
                }
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;
            if (returnBack) {
                int toStep = Int32.Parse(fc.Get("toStep"));
                result = flow.ToPreStep(bill.sys_no, step, userInfo.cardNo, toStep, opinion);
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
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("成功中止")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被撤销"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被撤销")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );
                    SendQywxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被撤销"),
                        new List<string>() { bill.applier_num }
                        );
                }
                else {
                    
                    string isReturn = "";
                    if (model.msg.Contains("退回")) {
                        isReturn = "【下一处理人退回】";
                    }
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    SendEmailToNextAuditor(
                        bill.sys_no + isReturn,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1},项目类别：{1}；项目名称：{2}，请尽快登陆系统处理。", bill.sys_no, BillTypeName, bill.project_type, bill.project_name),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    SendQywxMessageToNextAuditor(
                        BillTypeName + isReturn,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                        nextAuditors.ToList()
                        );
                    
                    if (string.IsNullOrEmpty(isReturn)) {
                        //总经理审批后，推送给采购接单时，需要抄送给部门CEO
                        if (result.stepName.Contains("采购部接单")) {
                            var notifyCeo = db.flow_notifyUsers.Where(f => f.bill_type == BillType && f.cond1 == bill.company && f.cond2 == bill.dept_name).Select(f => f.card_number).ToList();
                            if (notifyCeo.Count() > 0) {
                                SendQywxMessageToCC(
                                    BillTypeName,
                                    bill.sys_no,
                                    bill.applier_name,
                                    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                                    string.Format("项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                                    notifyCeo
                                    );
                            }
                        }
                        //设备部确认后，采购报价前，还要抄送给项目大类负责人
                        if (result.stepName.Contains("采购部上传报价单")) {
                            var notifyUser = db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "项目大类" && f.relate_text == bill.classification).Select(f => f.relate_value).ToList();
                            if (notifyUser.Count() > 0) {
                                SendQywxMessageToCC(
                                    BillTypeName + "（已上传施工清单）",
                                    bill.sys_no,
                                    bill.applier_name,
                                    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                                    string.Format("项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                                    notifyUser
                                    );
                            }
                        }

                        //会签之前，消防项目和环保项目需要抄送给黄志锋
                        if (result.stepName.Contains("管理部会签")) {
                            var notifyUser = db.flow_notifyUsers.Where(f => f.bill_type == BillType && f.cond1 == bill.classification).Select(f => f.card_number).ToList();
                            if (notifyUser.Count() > 0) {
                                SendQywxMessageToCC(
                                    BillTypeName,
                                    bill.sys_no,
                                    bill.applier_name,
                                    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                                    string.Format("项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                                    notifyUser
                                    );
                            }
                        }

                        //审核部最终确认后，申请人验收前，需要抄送给采购员
                        if (result.stepName.Contains("申请人验收")) {
                            var notifyUser = db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "采购部审批" && f.relate_text == bill.company).Select(f => f.relate_value).ToList();
                            if (notifyUser.Count() > 0) {
                                SendQywxMessageToCC(
                                    BillTypeName + "（审核部已确认）",
                                    bill.sys_no,
                                    bill.applier_name,
                                    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                                    string.Format("现可通知中标供应商：项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                                    notifyUser
                                    );
                            }
                        }
                    }

                }
            }
        }

    }
}