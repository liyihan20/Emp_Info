using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.Interfaces;
using EmpInfo.QywxWebSrv;
using Newtonsoft.Json;
using EmpInfo.FlowSvr;
using EmpInfo.Util;

namespace EmpInfo.Services
{
    public class HHSv : BillSv, IBeginAuditOtherInfo
    {
        ei_hhApply bill;

        public HHSv() { }
        public HHSv(string sysNo)
        {
            bill = db.ei_hhApply.Single(h => h.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "HH"; }
        }

        public override string BillTypeName
        {
            get { return "换货申请"; }
        }
        public override string AuditViewName()
        {
            return "BeginAuditHHApply";
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var list = new List<ApplyMenuItemModel>();

            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "提交申请" && f.relate_value == userInfo.cardNo).Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    url = "BeginApply?billType=" + BillType,
                    text = "开始申请",
                    iconFont = "fa-pencil",
                    colorClass = "text-danger"
                });
                list.Add(new ApplyMenuItemModel()
                {
                    url = "GetMyApplyList?billType=" + BillType,
                    text = "我申请的",
                    iconFont = "fa-th"
                });
            }
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

            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "查询报表" && f.relate_value == userInfo.cardNo).Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/HHReport"
                });
            }
            return list;
        }

        public override object GetInfoBeforeApply(Models.UserInfo userInfo, Models.UserInfoDetail userInfoDetail)
        {
            var list = db.flow_auditorRelation.Where(f => f.bill_type == BillType).ToList();

            return new HHBeforeApplyModel()
            {
                sys_no = GetNextSysNum(BillType),
                //agencyList = list.Where(l => l.relate_name == "办事处审批人").Select(l => l.relate_text).Distinct().ToList(),
                depNameList = list.Where(l => l.relate_name == "生产主管").Select(l => l.relate_text).Distinct().ToList()
            };
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {  
            bill = JsonConvert.DeserializeObject<ei_hhApply>(fc.Get("head"));
            List<ei_hhApplyEntry> entrys = JsonConvert.DeserializeObject<List<ei_hhApplyEntry>>(fc.Get("entrys"));

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;
            bill.quality_manager_name = GetUserNameByNameAndCardNum(bill.quality_manager_no);
            bill.quality_manager_no = GetUserCardByNameAndCardNum(bill.quality_manager_no);
            bill.notify_clerk_name = GetUserNameByNameAndCardNum(bill.notify_clerk_no);
            bill.notify_clerk_no = GetUserCardByNameAndCardNum(bill.notify_clerk_no);
            bill.charge_customers = db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "提交申请" && f.relate_value == userInfo.cardNo).First().cond1;


            db.ei_hhApply.Add(bill);
            foreach (var entry in entrys) {
                entry.ei_hhApply = bill;
                db.ei_hhApplyEntry.Add(entry);
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(
                JsonConvert.SerializeObject(bill, new JsonSerializerSettings() { ReferenceLoopHandling=ReferenceLoopHandling.Ignore }),
                BillType,
                userInfo.cardNo,
                bill.sys_no,
                bill.return_dep + "的换货申请单",
                entrys.First().moduel + "等" + entrys.Count() + "项换货申请"
                );
            if (result.suc) {
                try {
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
                throw new Exception("原因是：" + result.msg);
            }
        }

        public override object GetBill()
        {
            var m = new HHCheckApplyModel();
            m.head = bill;
            m.entrys = bill.ei_hhApplyEntry.ToList();
            m.rs = bill.ei_hhReturnDetail.ToList();

            return m;
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new HHSv(sysNo).GetBill();
        }

        public override Models.SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));

            //验证
            if (isPass) {
                var entrys = bill.ei_hhApplyEntry.ToList();
                if (stepName.Contains("生产主管")) {
                    if (entrys.Where(e => e.real_fill_qty == null).Count() > 0) {
                        return new SimpleResultModel(false, "存在未填写的实补数量，填写完整后才能继续流转");
                    }
                }
                else if (stepName.Contains("物流")) {
                    if (entrys.Where(e => e.send_qty == null).Count() > 0) {
                        return new SimpleResultModel(false, "存在未填写的实发数量，填写完整后才能继续流转");
                    }
                    var returnList = bill.ei_hhReturnDetail.ToList();
                    if (returnList.Count() < 1) {
                        return new SimpleResultModel(false, "必须填写退货明细，才能继续流转");
                    }
                    if (entrys.Sum(e => e.send_qty) != returnList.Sum(r => r.return_qty)) {
                        return new SimpleResultModel(false, "实发数量总数与退货明细数量总数不一致，不能继续流转");
                    }
                    foreach (var moduel in returnList.Select(r => r.moduel).Distinct().ToList()) {
                        if (returnList.Where(r => r.moduel == moduel).Sum(r => r.return_qty) > entrys.Where(e => e.moduel == moduel || e.c_moduel == moduel).Sum(e => e.send_qty)) {
                            return new SimpleResultModel(false, "退货明细数量不能大于实发数量，型号：" + moduel);
                        }
                    }
                    foreach (var en in entrys) {
                        if (en.send_qty > returnList.Where(r => r.moduel == en.moduel || r.moduel == en.c_moduel).Sum(r => r.return_qty)) {
                            return new SimpleResultModel(false, "实发数量不能大于退货数量明细，型号：" + en.moduel);
                        }
                    }
                    //foreach (var moduel in entrys.Select(e => e.moduel).Distinct().ToList()) {
                    //    if (entrys.Where(e => e.moduel == moduel).Sum(e => e.send_qty) != returnList.Where(r => r.moduel == moduel).Sum(r => r.return_qty)) {
                    //        return new SimpleResultModel(false, "实发数量与退货数量不一致，不能继续流转，型号：" + moduel);
                    //    }
                    //}
                    
                }
            }
            var setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill, setting);
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

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num)
                        );

                    SendQywxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "审批通过" : "审批不通过"),
                        new List<string>() { bill.applier_num }
                        );
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    //到达品质经理这一步时，需抄送给营业员,到达物流这一步时，需抄送给计划经理,2020-10-16 取消抄送给计划经理，计划经理改为需要审核
                    if (result.stepName.Contains("品质经理")) {
                        List<string> ccUsers = new List<string>() { bill.notify_clerk_no };                        
                        SendQywxMessageToCC(
                            BillTypeName,
                            bill.sys_no,
                            bill.applier_name,
                            ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                            string.Format("客户：{0}；事业部：{1}；规格型号：{2}等{3}项", bill.customer_name, bill.return_dep, bill.ei_hhApplyEntry.First().moduel, bill.ei_hhApplyEntry.Count()),
                            ccUsers
                        );
                    }

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
                        string.Format("客户：{0}；事业部：{1}；规格型号：{2}等{3}项", bill.customer_name,bill.return_dep,bill.ei_hhApplyEntry.First().moduel,bill.ei_hhApplyEntry.Count()),
                        nextAuditors.ToList()
                        );

                }
            }
        }

        public List<string> GetOrderModuel(string company, string orderNo)
        {
            return db.Database.SqlQuery<string>("exec GetOrderModuel @company = {0},@order_no = {1}", company, orderNo).ToList();
        }

        public ei_hhApplyEntry GetEntry(int id)
        {
            return db.ei_hhApplyEntry.Single(h => h.id == id);
        }

        //品质经理录入实退数量、是否已上线
        public void SaveEntryByQuality(int id, int? realReturnQty, string isOnLine)
        {
            var entry = GetEntry(id);
            entry.real_return_qty = realReturnQty;
            entry.is_on_line = isOnLine;
            db.SaveChanges();
        }

        //生产主管录入补货数量
        public void SaveEntryByCharger(int id, int? realFillQty)
        {
            var entry = GetEntry(id);
            if (entry.ei_hhApply.out_time != null) {
                throw new Exception("此放行条已放行，不能再修改实出数量");
            }

            entry.real_fill_qty = realFillQty;
            db.SaveChanges();
        }

        //物流录入实发数量、发货人
        public void SaveEntryByLogistics(int id, int? sendQty, string senderName)
        {
            var entry = GetEntry(id);
            entry.send_qty = sendQty;
            entry.sender_name = senderName;
            db.SaveChanges();
        }

        //保存退货明细
        public ei_hhReturnDetail SaveReturnDetail(ei_hhReturnDetail detail)
        {
            ei_hhReturnDetail result;

            detail.op_time = DateTime.Now;
            if (detail.id == 0) {
                result = db.ei_hhReturnDetail.Add(detail);
            }
            else {
                var det = db.ei_hhReturnDetail.Single(d => d.id == detail.id);
                MyUtils.CopyPropertyValue(detail, det);
                result = det;
            }
            db.SaveChanges();

            return result;
        }

        //删除退货明细
        public void RemoveReturnDetail(int id)
        {
            db.ei_hhReturnDetail.Remove(db.ei_hhReturnDetail.Where(h => h.id == id).FirstOrDefault());
            db.SaveChanges();
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