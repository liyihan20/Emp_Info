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
    public class XCSv:BillSv,IBeginAuditOtherInfo
    {
        private ei_xcApply bill;

        public XCSv() { }
        public XCSv(string sysNo)
        {
            bill = db.ei_xcApply.Single(x => x.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "XC"; }
        }

        public override string BillTypeName
        {
            get { return "委外加工流程"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditXCApply";
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
                    url = "../Report/XCReport"
                });
            }

            if (auth.Where(a => a.relate_type == "费用统计").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "费用统计",
                    iconFont = "fa-cny",
                    url = "../Report/XCSummary"
                });
            }

            if (auth.Where(a => a.relate_type == "部门总经理与目标").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "部门总经理与目标维护",
                    iconFont = "fa-user",
                    url = "../ApplyExtra/XCAuditorSetting"
                });
            }

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            string yearMonth = DateTime.Now.ToString("yyyy-MM");
            var m = new XCBeforeApplyModel();
            m.sys_no = GetNextSysNum(BillType);
            m.applierName = userInfo.name;
            m.depList = db.ei_xcDepTarget.Where(x => x.year_month == yearMonth).ToList();

            if (m.depList.Count() < 1) throw new Exception("本月委外目标额度未维护，请联系营运部处理");

            m.k3Accounts = db.k3_database.Where(k => !new string[] { "光电", "半导体", "香港光电科技" }.Contains(k.account_name))
                .OrderBy(k => k.account_name).Select(k => k.account_name).ToList();

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_xcApply>(fc.Get("head"));
            List<ei_xcMatOutDetail> entrys = JsonConvert.DeserializeObject<List<ei_xcMatOutDetail>>(fc.Get("entrys"));

            if (entrys.Count() < 1) {
                throw new Exception("发出明细至少必须录入一行记录");
            }

            foreach (var e in entrys) {
                if (e.out_qty == null) {
                    throw new Exception("发出数量不能为空");
                }
                e.sys_no = bill.sys_no;

                db.ei_xcMatOutDetail.Add(e);
            }

            var currentMonth = DateTime.Now.ToString("yyyy-MM");
            var target = db.ei_xcDepTarget.Where(x =>x.company==bill.company && x.dep_name == bill.dep_name && x.year_month == currentMonth).FirstOrDefault();
            if (target == null) throw new Exception("当前部门目标额度没有维护");

            bill.current_month_target = target.month_target;
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;
            bill.buyer_auditor_num = GetUserCardByNameAndCardNum(bill.buyer_auditor);
            bill.dep_charger_num = GetUserCardByNameAndCardNum(bill.dep_charger);
            bill.planner_auditor_num = GetUserCardByNameAndCardNum(bill.planner_auditor);
            bill.mat_group_num = GetUserCardByNameAndCardNum(bill.mat_group);

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.dep_name + "的委外加工", "申请"+bill.product_model + " 委外加工 " + bill.qty + bill.unit_name);
            if (result.suc) {
                try {
                    db.ei_xcApply.Add(bill);
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
            XCCheckApplyModel m = new XCCheckApplyModel();
            m.bill = bill;
            m.mats = db.ei_xcMatOutDetail.Where(x => x.sys_no == bill.sys_no).ToList();
            m.pros = db.ei_xcProductInDetail.Where(x => x.sys_no == bill.sys_no).ToList();
            m.k3StockInfo = new List<POInfoModel>();
            if (bill.bus_stock_no != null && bill.k3_account != null) {
                m.k3StockInfo = new K3Sv(bill.k3_account).GetK3BusStockBill(bill.bus_stock_no);
            }
            return m;
        }

        

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            
            //else if (stepName.Contains("物料组")) {
            //    bill.bus_stock_no = fc.Get("bus_stock_no");
            //    if (string.IsNullOrEmpty(bill.bus_stock_no)) {
            //        throw new Exception("必须录入委外加工出库单号");
            //    }
            //    if (new K3Sv(bill.k3_account).GetK3BusStockBill(bill.bus_stock_no).Count() < 1) {
            //        throw new Exception("在K3委外加工出库单查询不到此单号的数据");
            //    }


            //}
            //else if (stepName.Contains("物流盘点发出")) {
            //    bill.stock_out_comment = fc.Get("stock_out_comment");
            //}
            //else if (stepName.Contains("仓库接收成品")) {
            //    bill.stock_in_comment = fc.Get("stock_in_comment");
            //}
            //else if (stepName.Contains("营运部抽检")) {
            //    bill.check_in_comment = fc.Get("check_in_comment");
            //}

            if (isPass) {                
                if (stepName.Contains("采购询价")) {
                    string supplierName = fc.Get("supplier_name");
                    string unitPrice = fc.Get("unit_price");
                    decimal price;

                    if (string.IsNullOrEmpty(supplierName)) {
                        throw new Exception("请录入供应商名称");
                    }
                    if (!decimal.TryParse(unitPrice, out price)) {
                        throw new Exception("请输入单价，必须是数字");
                    }
                    bill.supplier_name = supplierName;
                    bill.unit_price = price;
                    bill.total_price = price * bill.qty;

                    DateTime currentMonth = DateTime.Parse(bill.apply_time.ToString("yyyy-MM-01"));
                    DateTime nextMonth = currentMonth.AddMonths(1);
                    bill.current_month_total = (int)Math.Round(db.ei_xcApply.Where(x =>x.company==bill.company && x.dep_name==bill.dep_name && x.apply_time >= currentMonth && x.apply_time <= nextMonth && x.total_price != null).Sum(x => x.total_price) ?? 0, 0) + (int)Math.Round(bill.total_price ?? 0, 0);

                }
                else if (stepName.Contains("营运部审批")) {
                    //bill.check_out_comment = fc.Get("check_out_comment");
                    bill.need_ceo_confirm = bool.Parse(fc.Get("need_ceo_confirm"));
                }
                else if (stepName.Contains("采购下单")) {
                    bill.po_date = DateTime.Now;
                }
                //else if (stepName.Contains("仓库接收成品")) {
                //    var hasInQty = db.ei_xcProductInDetail.Where(x => x.sys_no == bill.sys_no).Sum(x => x.in_qty) ?? 0;
                //    if (Math.Abs(hasInQty - bill.qty) > 0.001m) {
                //        throw new Exception(string.Format("接收的成品数量【{0}】与委外加工的数量【{1}】必须一致后才能审批通过", hasInQty, bill.qty));
                //    }                    
                //}
                //else if (stepName.Contains("申请人确认领回")) {
                //    var backTime = fc.Get("take_back_time");
                //    DateTime backTimedt;
                //    if (!DateTime.TryParse(backTime, out backTimedt)) {
                //        throw new Exception("请填写正确的领回日期");
                //    }
                //    bill.take_back_time = backTimedt;
                //}
            }else{
                bill.total_price = null;
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;
           
            string formJson = JsonConvert.SerializeObject(bill);
            result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            
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
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";                                 

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

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
                    
                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("[{2}]{0}的{1}申请", bill.product_model, BillTypeName, bill.dep_name),
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new XCSv(sysNo).GetBill();
        }

        public void UpdatePrintStatus()
        {
            if (bill.out_status == null) {
                bill.out_status = "已打印";
            }
            bill.print_time = (bill.print_time ?? "") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ";";

            db.SaveChanges();
        }

    }
}