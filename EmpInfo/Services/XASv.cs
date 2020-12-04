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

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            XABeforeApplyModel m = new XABeforeApplyModel();
            m.sys_no = GetNextSysNum(BillType);
            m.applierName = userInfo.name;
            m.depNameList = db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "部门总经理").Select(f => f.relate_text).OrderBy(f => f).Distinct().ToList();

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {            
            bill = JsonConvert.DeserializeObject<ei_xaApply>(fc.Get("head"));
            if (bill.has_profit && !string.IsNullOrEmpty(bill.profit_confirm_people_name)) {
                bill.profit_confirm_people_num = GetUserCardByNameAndCardNum(bill.profit_confirm_people_name);
            }
            bill.dept_charger_num = GetUserCardByNameAndCardNum(bill.dept_charger_name);
            bill.equitment_auditor_num = GetUserCardByNameAndCardNum(bill.equitment_auditor_name);
            bill.applier_num = userInfo.cardNo;
            bill.applier_name = userInfo.name;
            bill.apply_time = DateTime.Now;
            bill.bill_no = GetNextSysNum(bill.dept_name+"-");

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.classification+"_"+bill.project_type, bill.project_name);
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
                if (stepName.Contains("上传报价单")) {
                    if (db.ei_xaApplySupplier.Where(x => x.sys_no == bill.sys_no && x.price == null).Count() > 0) {
                        throw new Exception("存在未录入价钱的供应商，请全部录入后再审批通过");
                    }
                }
                if (stepName.Contains("确认中标供应商")) {
                    int bidderId;
                    if (!int.TryParse(bidder, out bidderId)) {
                        throw new Exception("请先选择中标的供应商");
                    }
                    var bidderSupplier = db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.id == bidderId).FirstOrDefault();
                    if (bidderSupplier == null) {
                        throw new Exception("选择的供应商不存在");
                    }
                    bidderSupplier.is_bidder = true;
                    bill.can_print = true;
                    db.SaveChanges();
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
                        BillTypeName,
                        bill.sys_no + isReturn,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("项目类别：{0}；项目名称：{1}", bill.project_type, bill.project_name),
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public ei_xaApplySupplier AddSupplier(string supplierName)
        {
            if (db.ei_xaApplySupplier.Where(s => s.sys_no == bill.sys_no && s.supplier_name == supplierName).Count() > 0) {
                throw new Exception("此供应商已存在，不能再新增"); 
            }
            var supplier = db.ei_xaApplySupplier.Add(new ei_xaApplySupplier()
            {
                sys_no = bill.sys_no,
                supplier_name = supplierName
            });
            db.SaveChanges();

            return supplier;
        }

        public void UpdateSupplierName(int id, string supplierName)
        {
            var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
            if (supplier != null) {
                supplier.supplier_name = supplierName;
                db.SaveChanges();
            }
        }

        public void UpdateSupplierPrice(int id, decimal price)
        {
            var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
            if (supplier != null) {
                supplier.price = price;
                db.SaveChanges();
            }
        }

        public void RemoveSupplier(int id)
        {
            var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
            if (supplier != null) {
                db.ei_xaApplySupplier.Remove(supplier);
                db.SaveChanges();
            }
        }

    }
}