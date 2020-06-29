using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Objects;
using System.Linq;

namespace EmpInfo.Services
{
    public class ITSv:BillSv,IBeginAuditOtherInfo,IJsInterface
    {
        ei_itApply bill;

        public ITSv() { }
        public ITSv(string sysNo)
        {
            bill = db.ei_itApply.Single(s => s.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "IT"; }
        }

        public override string BillTypeName
        {
            get { return "电脑维修申请"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditITApply";
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);

            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "维修项目" && f.relate_value == userInfo.cardNo).Count() > 0) {
                menus.AddRange(new List<ApplyMenuItemModel>() { 
                    new ApplyMenuItemModel()
                    {
                        text = "维修项目管理",
                        iconFont = "fa-list",
                        url = "../ApplyExtra/ManageITItems"
                    },
                    new ApplyMenuItemModel()
                    {
                        text = "查询报表",
                        iconFont = "fa-file-text-o",
                        url = "../Report/ITReport"
                    },
                    new ApplyMenuItemModel(){
                        text = "现场已维修报表",
                        iconFont = "fa-file-text-o",
                        url = "../Report/ITFixedRecord"
                    },
                    new ApplyMenuItemModel()
                    {
                        text = "标签打印 & 电脑取回",
                        iconFont = "fa-print",
                        url = "../ApplyExtra/PrintITCode"
                    }
                });
            }
            menus.Add(new ApplyMenuItemModel()
            {
                text = "维修标签扫描",
                iconFont = "fa-barcode 扫描维修标签",
                url = "../WX/JsInterface?actionType=scanQRCode"
            });

            if (db.ei_flowAuthority.Where(f => f.bill_type == BillType && f.relate_type == "提升优先级" && f.relate_value == userInfo.cardNo).Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                    {
                        text = "提升优先级",
                        iconFont = "fa-level-up",
                        url = "../ApplyExtra/TurnUPITPriority"
                    });
            }
            return menus;
        }

        public override object GetInfoBeforeApply(Models.UserInfo userInfo, Models.UserInfoDetail userInfoDetail)
        {
            var hr = new HRDBSv().GetHREmpDetailInfo(userInfo.cardNo);
            var info = new
            {
                sys_no = GetNextSysNum(BillType),
                applier_name = userInfo.name,
                dep_name = hr.dep_name,
                emp_position = new HRDBSv().GetHREmpInfo(userInfo.cardNo).job_level,
                applier_phone = string.IsNullOrWhiteSpace(userInfoDetail.shortPhone) ? userInfoDetail.phone : userInfoDetail.shortPhone,
            };
            return JsonConvert.SerializeObject(info);
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_itApply>(fc.Get("obj"));
            bill.apply_time = DateTime.Now;
            bill.applier_num = userInfo.cardNo;
            bill.dep_charger_no = GetUserCardByNameAndCardNum(bill.dep_charger_name);
            bill.dep_charger_name = GetUserNameByNameAndCardNum(bill.dep_charger_name);
            bill.emp_position = bill.emp_position ?? "";
            bill.fixed_items = "[]";

            if (bill.dep_name.Contains("总裁办")) {
                bill.priority = 5;
            }
            else if (bill.emp_position.Contains("部长")) {
                bill.priority = 4;
            }
            else if (bill.emp_position.Contains("经理")) {
                bill.priority = 3;
            }
            else {
                bill.priority = 1;

                // 如果故障项目中包含新设备申请这个大类的项目，那么优先级设置为2，会经过信息管理部经理审批环节
                var fItems = JsonConvert.DeserializeObject<List<ItItem>>(bill.faulty_items);
                var newEqType = db.ei_itItems.Where(i => i.item_type == "新设备申请").ToList();
                if (newEqType.Count() > 0) {
                    foreach (var f in fItems) {
                        if (newEqType.Where(n => n.item_name == f.n).Count() > 0) {
                            bill.priority = 2;
                            break;
                        }
                    }
                }
            }

            string faultyItemsString = string.Join("/", JsonConvert.DeserializeObject<List<ItItem>>(bill.faulty_items).Select(i => i.n).ToArray());

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.priority.ToString(), faultyItemsString);
            if (result.suc) {
                try {
                    db.ei_itApply.Add(bill);
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
                throw new Exception("申请提交失败，原因是：" + result.msg);
            }

        }

        public override object GetBill()
        {
            return bill;
        }

        public override Models.SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            if (stepName.Equals("IT部接单")) {
                if (bill.priority < 6) {
                    // 优先级少于6的，接单之前，判断是否有7天未处理完成的单，如有，不允许接，给提示
                    var exceedBills = (from i in db.vw_ITExcel
                                       where i.accept_man_number == userInfo.cardNo
                                       && i.repair_time == null
                                       && i.audit_result != "被拒绝"
                                       && ((i.repair_way == "现场维修" && EntityFunctions.DiffDays(i.print_time, DateTime.Now) >= 7)
                                       || (i.repair_way == "远程维修" && EntityFunctions.DiffDays(i.accept_time, DateTime.Now) >= 7)
                                       )
                                       orderby i.accept_time
                                       select i.sys_no).Take(1).ToList();
                    if (exceedBills.Count() > 0) {
                        throw new Exception("存在超过7天接单后未维修的申请，请先处理后再接单，5秒后将跳转到需处理的页面>" + exceedBills.First());
                    }
                }
                MyUtils.SetFieldValueToModel(fc, bill);
                bill.accept_man_name = userInfo.name;
                bill.accept_man_number = userInfo.cardNo;
                bill.accept_time = DateTime.Now;
            }
            else if (stepName.Equals("搬电脑到IT部")) {
                if (isPass && bill.print_time == null) {
                    return new SimpleResultModel(false, "请在IT部打印维修标签后再操作");
                }
                if (!isPass && bill.print_time != null) {
                    return new SimpleResultModel(false, "已在IT部打印维修标签后不能再收回");
                }
            }
            else if (stepName.Equals("IT部维修处理")) {
                MyUtils.SetFieldValueToModel(fc, bill);
                bill.repair_man = userInfo.name;
                bill.repair_time = DateTime.Now;
            }
            else if (stepName.Equals("服务评价")) {
                MyUtils.SetFieldValueToModel(fc, bill);
                bill.evaluation_time = DateTime.Now;
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, JsonConvert.SerializeObject(bill));
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
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "维修完成" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num)
                        );

                    //SendWxMessageForCompleted(
                    //    BillTypeName,
                    //    bill.sys_no,
                    //    (isSuc ? "维修完成" : "被拒绝"),
                    //    pushUsers
                    //    );
                    SendQywxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "维修完成" : "被拒绝"),
                        new List<string>() { bill.applier_num }
                        );
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    if (result.step == 20 && bill.repair_way == "远程维修") {
                        return;
                    }

                    SendEmailToNextAuditor(
                        bill.sys_no,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1},处理步骤是【{2}】，请尽快登陆系统处理。", bill.sys_no, BillTypeName,result.stepName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    string wxPushContent = "";
                    if (result.stepName == "搬电脑到IT部") {
                        wxPushContent = "请在3天内将故障电脑搬到IT部（地址：4栋2楼），并在IT部打印维修标签粘贴在机箱上，等待维修人员" + bill.accept_man_name + "处理，如超时未处理将会被自动NG。";
                    }
                    else if (result.stepName == "服务评价") {
                        wxPushContent = "电脑维修单已完成，请对此次维修服务进行评价";
                    }
                    else {
                        wxPushContent = string.Format("所在部门：{0}；计算机名/IP地址：{1}/{2}", bill.dep_name, bill.computer_name ?? "", bill.ip_addr ?? "");
                    }
                    //SendWxMessageToNextAuditor(
                    //    BillTypeName,
                    //    bill.sys_no,
                    //    result.step,
                    //    result.stepName,
                    //    bill.applier_name,
                    //    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                    //    wxPushContent,
                    //    db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                    //    );
                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        wxPushContent,
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public override IComparer<FlowAuditListModel> GetAuditListComparer()
        {
            return new BillComparer();
        }

        private class BillComparer : IComparer<FlowAuditListModel>
        {
            public int Compare(FlowAuditListModel x, FlowAuditListModel y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                //这里的title字段要保存优先级别，1~5，数字大的优先级大，排在前面
                if(!x.title.Equals(y.title)){
                    return -string.Compare(x.title,y.title);
                }
                else {
                    if (x.applyTime == null) return 1;
                    if (y.applyTime == null) return -1;
                    return x.applyTime > y.applyTime ? 1 : -1;
                }
            }
        }

        #region IT维修项目管理

        public List<ei_itItems> GetITItems()
        {
            return db.ei_itItems.OrderBy(i => i.item_type).ThenBy(i => i.item_name).ToList();
        }

        public void SaveItItem(ei_itItems item)
        {
            if (item.id == 0) {
                db.ei_itItems.Add(item);
            }
            else {
                var _item = db.ei_itItems.Single(i => i.id == item.id);
                MyUtils.CopyPropertyValue(item, _item);
            }
            db.SaveChanges();
        }

        public void RemoveItItem(int id)
        {
            db.ei_itItems.Remove(db.ei_itItems.SingleOrDefault(i => i.id == id));
            db.SaveChanges();
        }

        #endregion

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            bill = db.ei_itApply.Single(i => i.sys_no == sysNo);
            return new ITAuditOtherInfoModel()
            {
                faultyItems = bill.faulty_items,
                loginName = bill.login_name,
                loginPassword = bill.login_password
            };            
        }

        public void UpdatePrintTime()
        {
            bill.print_time = DateTime.Now;
            db.SaveChanges();
        }

        public void FetchComputer(string cardNumber, string name, string phone)
        {
            bill.fetch_time = DateTime.Now;
            bill.fetcher_name = name;
            bill.fetcher_no = cardNumber;
            bill.fetcher_phone = phone;
            db.SaveChanges();
        }

        /// <summary>
        /// 健林可以提高某人的优先级
        /// </summary>
        /// <param name="sysNo"></param>
        public void UpdatePriority(string sysNo)
        {
            bill = db.ei_itApply.Where(i => i.sys_no == sysNo).FirstOrDefault();
            if (bill != null) {
                if (bill.priority == 6) {
                    throw new Exception("优先级已提升过，不能重复操作");
                }
                bill.priority = 6;
                db.SaveChanges();
            }
            else {
                throw new Exception("申请单不存在");
            }
        }

        public ei_itApply SearchItApply(string searchContent)
        {
            return db.ei_itApply.Where(i => i.applier_num == searchContent || i.sys_no == searchContent).OrderByDescending(i => i.id).FirstOrDefault();
        }

        /// <summary>
        /// 扫码之后如何处理
        /// </summary>
        /// <param name="result">二维码内容</param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public RedirectModel HandleJsInterface(string result,UserInfo userInfo)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var currentStep = flow.GetCurrentStep(result);
            if (currentStep.auditors.Contains(userInfo.cardNo)) {
                return new RedirectModel() { controllerName = "Apply", actionName = "BeginAuditApply", routetValues = new { sysNo = result, step = currentStep.step } };
            }
            else {
                return new RedirectModel() { controllerName = "Apply", actionName = "CheckApply", routetValues = new { sysNo = result } };
            }
        }
        
    }
}