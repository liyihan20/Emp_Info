using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using Newtonsoft.Json;
using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;

namespace EmpInfo.Services
{
    public class APSv : BillSv, IBeginAuditOtherInfo
    {
        ei_apApply bill;

        public APSv() { }
        public APSv(string sysNo)
        {
            bill = db.ei_apApply.Single(a => a.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "AP"; }
        }

        public override string BillTypeName
        {
            get { return "辅料订购单"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditAPApply";
        }
        
        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("APReport", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/APReport"
                });
            }

            return menus;
        }

        public override object GetInfoBeforeApply(Models.UserInfo userInfo, Models.UserInfoDetail userInfoDetail)
        {
            APBeforeApplyModel am = new APBeforeApplyModel();
            am.k3AccountList = db.k3_database.Select(d => d.account_name).Distinct().ToList();
            var existedAp = db.ei_apApply.Where(a => a.applier_num == userInfo.cardNo).OrderByDescending(a => a.id).FirstOrDefault();
            if (existedAp != null) {
                existedAp.sys_no = GetNextSysNum(BillType);
                am.ap = existedAp;
            }
            else {
                am.ap = new ei_apApply()
                {
                    sys_no = GetNextSysNum(BillType),
                    applier_phone = userInfoDetail.shortPhone ?? userInfoDetail.phone                    
                };
            }            
            return am;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

            bill = JsonConvert.DeserializeObject<ei_apApply>(fc.Get("head"));
            List<ei_apApplyEntry> es = JsonConvert.DeserializeObject<List<ei_apApplyEntry>>(fc.Get("entrys"));

            bill.apply_time = DateTime.Now;
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.charger_name = GetUserNameByNameAndCardNum(bill.charger_no);
            bill.charger_no = GetUserCardByNameAndCardNum(bill.charger_no);
            bill.controller_name = GetUserNameByNameAndCardNum(bill.controller_no);
            bill.controller_no = GetUserCardByNameAndCardNum(bill.controller_no);
            bill.minister_name = GetUserNameByNameAndCardNum(bill.minister_no);
            bill.minister_no = GetUserCardByNameAndCardNum(bill.minister_no);
            db.ei_apApply.Add(bill);

            int entryNo = 1;
            foreach (var e in es) {
                e.entry_no = entryNo++;
                e.real_qty = e.qty;
                e.ei_apApply = bill;
                db.ei_apApplyEntry.Add(e);
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(
                JsonConvert.SerializeObject(bill, setting),
                BillType,
                userInfo.cardNo,
                bill.sys_no,
                bill.dep_name,
                es.First().item_name + "等" + es.Count() + "项辅料申购"
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
            APCheckApplyModel m = new APCheckApplyModel();
            m.ap = bill;
            m.entrys = bill.ei_apApplyEntry.ToList();

            return m;
        }

        public override Models.SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            if (stepName.Equals("PR处理") && isPass) {
                string poNumber = fc.Get("poNumber");
                if (string.IsNullOrWhiteSpace(poNumber)) throw new Exception("必须填写PO单号");
                bill.po_number = poNumber;
            }

            //审核人一开始没有在权限组里，在这里加入
            var autGroup = db.ei_groups.Where(g => g.name == "辅料订购申请组").FirstOrDefault();
            if (autGroup != null) {
                if (db.ei_groupUser.Where(gu => gu.group_id == autGroup.id && gu.user_id == userInfo.id).Count() < 1) {
                    db.ei_groupUser.Add(new ei_groupUser()
                    {
                        group_id = autGroup.id,
                        user_id = userInfo.id
                    });
                }
            }

            var setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill, setting);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
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

                    if (isSuc) {
                        //通知李梅菊170405019
                        pushUsers.AddRange(db.vw_push_users.Where(v => v.card_number == "170405019" && v.wx_push_flow_info == true).ToList());
                        ccEmails = GetUserEmailByCardNum("170405019");
                    }

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}；规格型号是：{3}等{4}个，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"), bill.ei_apApplyEntry.First().item_modual, bill.ei_apApplyEntry.Count()),
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
                        BillTypeName, bill.sys_no,
                        (isSuc ? "审批通过" : "审批不通过"),
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
                    //    string.Format("{0}等{1}项", bill.ei_apApplyEntry.First().item_modual,bill.ei_apApplyEntry.Count()),
                    //    db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                    //    );
                    SendQywxMessageToNextAuditor(
                            BillTypeName,
                            bill.sys_no,
                            result.step,
                            result.stepName,
                            bill.applier_name,
                            ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                            string.Format("{0}等{1}项", bill.ei_apApplyEntry.First().item_modual,bill.ei_apApplyEntry.Count()),
                            nextAuditors.ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new APSv(sysNo).GetBill();
        }

        public void UpdateQty(int entryNo, decimal newQty,string opName)
        {
            var entry = bill.ei_apApplyEntry.Where(e => e.entry_no == entryNo).FirstOrDefault();
            if (entry == null) throw new Exception("修改失败，原因：此行不存在");

            db.ei_apQtyChangeLog.Add(new ei_apQtyChangeLog()
            {
                sys_no = bill.sys_no,
                entry_no = entryNo,
                old_qty = entry.real_qty,
                new_qy = newQty,
                op_date = DateTime.Now,
                op_name = opName
            });

            entry.real_qty = newQty;
            db.SaveChanges();
        }

        /// <summary>
        /// 在总部K3此物料最近一次的价钱、供应商等
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public GetAPPriceHistory_Result GetItemPriceHistory(string itemNo)
        {
            return db.GetAPPriceHistory(itemNo).FirstOrDefault();            
        }

        /// <summary>
        /// 获取总部K3中此物料在此事业部中一段时间内的数量情况
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="busName"></param>
        /// <param name="beginDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public List<GetAPHistoryQty_Result> GetItemQtyHistory(string itemNumber, string busName, DateTime beginDate, DateTime endDate)
        {
            return db.GetAPHistoryQty(itemNumber, busName, beginDate, endDate).ToList().OrderByDescending(r => r.po_date).ToList();
        }

        /// <summary>
        /// 获取各个事业部的PO信息，导入到辅料申请列表
        /// </summary>
        /// <param name="account"></param>
        /// <param name="poNumber"></param>
        /// <returns></returns>
        public List<POInfoModel> GetPOInfoFromK3(string account, string poNumber)
        {
            return db.Database.SqlQuery<POInfoModel>("exec GetAPPOInfoFromK3 @account = {0},@po_number = {1}", account, poNumber).ToList();            
        }

        public List<GetItemStockQtyFromK3_Result> GetItemStockQtyFromK3(string itemNumber)
        {
            return db.GetItemStockQtyFromK3(itemNumber).ToList();
        }

    }
}