using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using EmpInfo.Util;
using EmpInfo.Interfaces;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class SPSv : BillSv, IBeginAuditOtherInfo
    {
        ei_spApply bill;

        public SPSv() { }
        public SPSv(string sysNo)
        {
            bill = db.ei_spApply.Single(s => s.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "SP"; }
        }

        public override string BillTypeName
        {
            get { return "寄/收件申请"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var list = base.GetApplyMenuItems(userInfo);
            //var list = new List<ApplyMenuItemModel>();
            //list.Add(new ApplyMenuItemModel()
            //{
            //    url = "../Att/SP/wl_tg.png",
            //    text = "开始申请",
            //    iconFont = "fa-pencil",
            //    colorClass = "text-danger"
            //});
            //list.Add(new ApplyMenuItemModel()
            //{
            //    url = "GetMyApplyList?billType=" + BillType,
            //    text = "我申请的",
            //    iconFont = "fa-th"
            //});
            //list.Add(new ApplyMenuItemModel()
            //{
            //    url = "GetMyAuditingList?billType=" + BillType,
            //    text = "我的待办",
            //    iconFont = "fa-th-list"
            //});
            //list.Add(new ApplyMenuItemModel()
            //{
            //    url = "GetMyAuditedList?billType=" + BillType,
            //    text = "我的已办",
            //    iconFont = "fa-th-large"
            //});

            if (HasGotPower("SPReport", userInfo.id)) {
                list.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/SPReport"
                });
            }
            return list;
        }

        public override string AuditViewName()
        {
            return "BeginAuditSPApply";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            //throw new Exception("收寄件服务暂停，待物流确认后再启用");
            string sysNo = GetNextSysNum(BillType);
            var sp = db.ei_spApply.Where(s => s.applier_num == userInfo.cardNo).OrderByDescending(s => s.id).FirstOrDefault();
            var busDepList = db.flow_auditorRelation.Where(f => f.bill_type == BillType && f.relate_name == "事业部审批").Select(f => f.relate_text).Distinct().ToList();

            var stockAddrList = (from f in db.flow_auditorRelation
                                    join u in db.ei_users on f.relate_value equals u.card_number
                                    where f.bill_type == BillType && f.relate_name == "仓管审批"
                                    select f.relate_text).Distinct().ToList();
            if (sp == null) {
                return new SPBeforeApplyModel()
                {
                    sys_no = sysNo,
                    applier_phone = string.IsNullOrWhiteSpace(userInfoDetail.shortPhone) ? userInfoDetail.phone : userInfoDetail.shortPhone,
                    busDepList = busDepList
                };
            }
            return new SPBeforeApplyModel()
            {
                sys_no = sysNo,
                applier_phone = sp.applier_phone,
                bus_name = sp.bus_name,
                company = sp.company,
                content_type = sp.content_type,
                from_addr = sp.from_addr,
                receiver_name = sp.receiver_name,
                receiver_phone = sp.receiver_phone,
                send_or_receive = sp.send_or_receive,
                to_addr = sp.to_addr,
                aging = sp.aging,
                scope = sp.scope,
                busDepList = busDepList,
                stockAddrList = stockAddrList
            };
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_spApply>(fc.Get("sp"));
            var entry = JsonConvert.DeserializeObject<List<ei_spApplyEntry>>(fc.Get("entry"));

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            int addrFlag = (bill.from_addr.Contains("广东") ? 1 : 0) + (bill.to_addr.Contains("广东") ? 1 : 0);
            if ("省内".Equals(bill.scope)) {
                if (addrFlag != 2) throw new Exception("收寄范围是省内时，寄件和收件地址都必须包含广东");
            }
            else {
                if (addrFlag != 1) throw new Exception("收寄范围是" + bill.scope + "时，寄件和收件地址只能包含一个广东");
            }
            
            

            if (string.IsNullOrEmpty(bill.ex_company)) {
                throw new Exception("请先选择快递公司。如果快递公司不存在，请与物流部周秀花联系");
            }
            if (string.IsNullOrWhiteSpace(bill.ex_no)) {
                throw new Exception("请填写快递单号");
            }
            if (bill.content_type == "文件") {
                bill.package_num = null;
                bill.box_size = null;
                bill.cardboard_num = null;
                bill.cardboard_size = null;
            }
            else {
                if (entry.Count() < 1) {
                    throw new Exception("请至少填写一项产品或原材料明细");
                }
                if (bill.content_type == "原材料") {
                    if (string.IsNullOrEmpty(bill.quality_audior_name)) {
                        throw new Exception("品质审核人或仓管审核人不能为空");
                    }
                    bill.quality_audior_no = GetUserCardByNameAndCardNum(bill.quality_audior_name);
                    if (string.IsNullOrEmpty(bill.stock_addr)) {
                        throw new Exception("请选择仓库地址");
                    }
                }
            }            
            

            FlowSvrSoapClient client = new FlowSvrSoapClient();            
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.bus_name, bill.content_type + bill.send_or_receive + "申请");
            if (result.suc) {
                try {
                    db.ei_spApply.Add(bill);
                    foreach (var e in entry) {
                        e.ei_spApply = bill;
                        db.ei_spApplyEntry.Add(e);
                    }
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

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            if (isPass) {
                string exCompany = fc.Get("ex_company");
                string exType = fc.Get("ex_type");
                string exPriceStr = fc.Get("ex_price");
                string exNo = fc.Get("ex_no");
                if (stepName.Contains("物流")) {
                    if (string.IsNullOrEmpty(exCompany)) {
                        return new SimpleResultModel() { suc = false, msg = "请先选择物流信息" };
                    }
                    if (string.IsNullOrEmpty(exNo)) {
                        return new SimpleResultModel() { suc = false, msg = "请先填写快递编号" };
                    }
                    if (!exCompany.Equals(bill.ex_company)) {
                        bill.ex_log = string.Format("{0}({1})[{2}]:{3}==>{4}({5})[{6}]:{7}", bill.ex_company, bill.ex_type ?? "", bill.ex_no, bill.ex_price ?? 0, exCompany, exType ?? "", exNo, exPriceStr);
                    }
                    bill.ex_no = exNo;
                    bill.ex_company = exCompany;
                    bill.ex_type = exType;
                    if (string.IsNullOrEmpty(exPriceStr)) {
                        bill.ex_price = 0;
                    }
                    else {
                        bill.ex_price = decimal.Parse(exPriceStr);
                    }
                }
                if (stepName.Contains("行政部")) {
                    bill.can_print = true; //可打印放行条并可被扫描
                    bill.out_status = "已打印";
                }
            }
            
            JsonSerializerSettings js = new JsonSerializerSettings();
            js.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill,js);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
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
                    //List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}{3}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"), bill.ex_log == null ? "" : "(快递信息有变更)"),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    //SendWxMessageForCompleted(
                    //    BillTypeName,
                    //    bill.sys_no + (bill.ex_log == null ? "" : "(快递信息有变更)"),
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
                    //    string.Format("{0}的{1}申请", bill.applier_name,bill.send_or_receive),
                    //    db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                    //    );
                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("{0}的{1}申请", bill.applier_name, bill.send_or_receive),
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new SPSv(sysNo).bill;
        }

        /// <summary>
        /// 表中已有数据时，选择物流公司
        /// </summary>
        /// <returns></returns>
        public List<SPExInfoModel> GetExInfo()
        {
            if (bill.content_type == "文件") {
                bill.package_num = 1;
                bill.box_size = "0";
                bill.cardboard_num = 0;
                bill.cardboard_size = "0";
                bill.aging = "";
            }
            var result = db.Database.SqlQuery<SPExInfoModel>("exec GetExpressInfo @Adr = {0},@DocQty = {1},@Size = {2},@CardQty = {3},@CSize = {4},@FQty = {5},@FWeight = {6}",
                bill.send_or_receive == "寄件" ? bill.to_addr : bill.from_addr,
                bill.package_num ?? 1,
                bill.box_size ?? "0",
                bill.cardboard_num ?? 0,
                bill.cardboard_size ?? "0",
                0,
                bill.total_weight ?? 1
                ).Where(s => s.FReed > 0).ToList();

            if (bill.content_type == "文件") {
                //文件的只能选择顺丰标快
                result = result.Where(r => r.FName == "顺丰" && r.FDelivery.Contains("标快")).ToList();
            }else if (bill.total_weight < 10) {
                if (bill.scope == "省内") {
                    result = result.Where(r => r.FName == "顺丰" && r.FDelivery.Contains("标快")).ToList();
                }
                else if (bill.scope == "省外" || bill.scope=="港澳台") {
                    if (bill.aging.Contains("次日达")) {
                        result = result.Where(r => r.FName == "顺丰" && r.FDelivery.Contains("标快")).ToList();
                    }
                    else {
                        result = result.Where(r => r.FName == "顺丰" && r.FDelivery.Contains("特惠")).ToList();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 申请时选择物流公司，此时数据表中还未保存数据
        /// </summary>
        /// <param name="sp"></param>
        /// <returns></returns>
        public List<SPExInfoModel> GetExInfoWithoutBill(ei_spApply sp)
        {
            bill = sp;
            return GetExInfo();
        }
    }
}