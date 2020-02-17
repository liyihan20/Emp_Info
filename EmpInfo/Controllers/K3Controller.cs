using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Models;
using EmpInfo.Filter;
using Newtonsoft.Json;
using EmpInfo.Util;

namespace EmpInfo.Controllers
{
    [SessionTimeOutFilter]
    public class K3Controller : BaseController
    {

        #region 重置k3密码和解禁

        public ActionResult AccountReset()
        {
            WriteEventLog("K3重开通/重置", "打开处理界面");
            var accountList = (from ac in db.GetK3AccoutList()
                               select new K3AccountModel()
                               {
                                   number = ac.FAcctNumber,
                                   name = ac.FAcctName
                               }).ToList();
            ViewData["k3Accounts"] = accountList;
            ViewData["email"] = userInfoDetail.email;
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult BeginReset(string k3Name, string account, string opType)
        {
            WriteEventLog("K3重开通/重置", string.Format("k3用户名:{0};账套:{1};操作类型:{2}", k3Name, account, opType));
            if (!k3Name.StartsWith(userInfo.name)) {
                WriteEventLog("K3重开通/重置", "你填写的K3登录名与你的真实姓名差别过大，不能处理，请走OA流程");
                return Json(new SimpleResultModel() { suc = false, msg = "你填写的K3登录名与你的真实姓名差别过大，不能处理，请走OA流程" });
            }
            try {
                db.ResetK3Emp(k3Name, userInfo.cardNo, userInfoDetail.phone, account, opType);
            }
            catch (Exception ex) {
                string message = ex.InnerException.Message;
                if (message.Contains("职员信息未完善")) {
                    try {
                        WriteEventLog("K3重开通/重置", "职员信息未完善，尝试完善");
                        db.BindK3Emp(k3Name, userInfo.cardNo, account);
                        db.ResetK3Emp(k3Name, userInfo.cardNo, userInfoDetail.phone, account, opType);
                    }
                    catch (Exception iex) {
                        WriteEventLog("K3重开通/重置", "职员信息未完善，尝试失败：" + iex.InnerException.Message);
                        return Json(new SimpleResultModel() { suc = false, msg = iex.InnerException.Message });
                    }
                }
                else {
                    WriteEventLog("K3重开通/重置", "操作失败："+ex.InnerException.Message);
                    return Json(new SimpleResultModel() { suc = false, msg = ex.InnerException.Message });
                }
            }
            ICAuditEntities db2 = new ICAuditEntities(); //只能new多一个Entity对象，否则会出现在提供程序连接上启动事务时出错的错误，不明白为什么
            ei_k3RestLog k3Log = new ei_k3RestLog();
            k3Log.account = account;
            k3Log.card_number = userInfo.cardNo;
            k3Log.k3_name = k3Name;
            k3Log.op_time = DateTime.Now;
            k3Log.op_type = opType.Equals("1") ? "重新开通" : opType.Equals("2") ? "重置密码" : "重新开通|重置密码";
            db2.ei_k3RestLog.Add(k3Log);
            db2.SaveChanges();

            return Json(new SimpleResultModel() { suc = true });
        }

        #endregion

        #region 光电/半导体/电子 销售出库单一审和生成送货单

        [AuthorityFilter]
        public ActionResult StockBillAudit()
        {
            if (!HasGotPower("StockBillAudit1")) {
                ViewBag.tip = "抱歉，你没有权限访问此功能";
                return View("Error");
            }

            StBillSearchParamModel spm;
            var cookie = Request.Cookies["stBillAcc"];
            if (cookie == null) {
                spm = new StBillSearchParamModel();
                spm.account = "半导体";
                spm.fromDate = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd");
                spm.toDate = DateTime.Now.ToString("yyyy-MM-dd");
            }
            else {
                spm = JsonConvert.DeserializeObject<StBillSearchParamModel>(cookie.Values.Get("spm"));
                spm.account = MyUtils.DecodeToUTF8(spm.account);
            }

            WriteEventLog("出库单一审", "打开主界面");

            ViewData["spm"] = spm;

            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult SearchStockBillList(string account, DateTime fromDate, DateTime toDate)
        {
            //保存cookie
            var spm = new StBillSearchParamModel()
            {
                account = MyUtils.EncodeToUTF8(account),
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd")
            };
            var cookie = new HttpCookie("stBillAcc");
            cookie.Values.Add("spm", JsonConvert.SerializeObject(spm));
            cookie.Expires.AddDays(7);
            Response.AppendCookie(cookie);

            var result = db.GetOutStockBillsForAudit1(account, fromDate, toDate).ToList();

            var list = (from r in result
                        select new StBillResultModel()
                        {
                            stId = r.interId,
                            stNumber = r.billNo,
                            stDate = r.billDate,
                            customerName = r.customerName,
                            customerNumber = r.customerNumber,
                            saleStyle = r.saleType,
                            srNumber = r.saleReqNo
                        }).Distinct(new stBillComparer()).OrderBy(r => r.stId).ToList();
            list.ForEach(l =>
            {
                var entry = from r in result
                            where r.interId == l.stId
                            select new
                            {
                                r.itemName,
                                r.itemModel,
                                r.price,
                                r.qty,
                                r.ammount,
                                r.unitName
                            };
                l.entryJson = JsonConvert.SerializeObject(entry);
            });

            spm.account = account;

            WriteEventLog("出库单一审", account + ":" + fromDate + "~" + toDate);

            ViewData["list"] = list;
            ViewData["spm"] = spm;
            return View("StockBillAudit");

        }

        //StBillResultModel的比较器，如果id相等，即两个类重复
        private class stBillComparer : IEqualityComparer<StBillResultModel>
        {

            public bool Equals(StBillResultModel x, StBillResultModel y)
            {
                return x.stId == x.stId;
            }

            public int GetHashCode(StBillResultModel obj)
            {
                return obj.stId.GetHashCode();
            }
        }

        //开始一审k3销售出库单
        [SessionTimeOutJsonFilter]
        public JsonResult BeginAuditStockBill(string account, int interid,bool isReject = false)
        {
            try {
                var result = db.StockbillAudit1(account, interid, userInfo.name, isReject).First();
                WriteEventLog("出库单一审", (isReject?"反":"") + "一审:"+interid + (result.suc == true ? "成功" : "失败") + ";msg" + result.msg);
                return Json(new SimpleResultModel() { suc = (bool)result.suc, msg = result.msg });
            }
            catch (Exception ex) {
                WriteEventLog("出库单一审", (isReject ? "反" : "") + "一审失败：" + interid + ex.Message);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        //开始生成送货单，电子没有，只有半导体和光电
        [SessionTimeOutJsonFilter]
        public JsonResult GenTrulyDelivery(string account, string billNo, string srNo)
        {
            try {
                var result = db.GenTrulyDeliveryBill(account, billNo, srNo).First();
                WriteEventLog("出库单一审", "生成送货单:"+billNo + (result.suc == 1 ? "成功" : "失败") + ";msg" + result.msg);
                return Json(new SimpleResultModel() { suc = result.suc == 1, msg = result.msg });
            }
            catch (Exception ex) {
                WriteEventLog("出库单一审", "生成送货单：" + billNo + ex.Message);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        //获取本人24小时内的已审核销售出库单
        [SessionTimeOutJsonFilter]
        public JsonResult GetStockBillForReject(string account)
        {
            try {
                var list = db.GetOutStockBillsForAudit1To2(account, userInfo.name).ToList();
                return Json(new { suc = true, list = list });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }
        
        #endregion

    }
}
